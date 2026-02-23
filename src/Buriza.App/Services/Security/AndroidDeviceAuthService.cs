#if ANDROID
using Android.App;
using Android.Content;
using Android.Security.Keystore;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Java.Security;
using Java.Util.Concurrent;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace Buriza.App.Services.Security;

/// <summary>
/// Android device-auth service using BiometricPrompt with CryptoObject binding.
///
/// Security model:
/// - BiometricPrompt with CryptoObject binds a Keystore-backed AES-GCM Cipher
///   directly to the biometric authentication result (per-use, no time window)
/// - The Cipher can only complete DoFinal inside OnAuthenticationSucceeded callback
/// - Key is invalidated if biometric enrollment changes (via Keystore binding)
/// - AuthenticateAsync uses BiometricStrong | DeviceCredential (no crypto needed)
/// - StoreSecureAsync/RetrieveSecureAsync use BiometricStrong only (CryptoObject requirement)
///
/// Storage format: Base64( 1-byte-IV-length || IV || AES-GCM-ciphertext-with-tag )
///
/// References:
/// - https://developer.android.com/training/sign-in/biometric-auth#crypto
/// - https://developer.android.com/reference/android/security/keystore/KeyGenParameterSpec
/// </summary>
public class AndroidDeviceAuthService : IDeviceAuthService
{
    private const string CryptoKeyAlias = "buriza_crypto_key";
    private const string PinCryptoKeyAlias = "buriza_pin_crypto_key";
    private const string DataPrefName = "buriza_secure_data";

    private ISharedPreferences? _dataPrefs;

    /// <summary>
    /// Probes device hardware for supported auth types (fingerprint, face, iris, PIN/passcode).
    /// Uses PackageManager feature flags for biometric granularity (API 29+).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Device capabilities including which biometric sensors and credential types are available.</returns>
    public async Task<DeviceCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        Context context = Platform.CurrentActivity ?? Platform.AppContext;
        KeyguardManager? keyguardManager = context.GetSystemService(Context.KeyguardService) as KeyguardManager;
        bool supportsPin = keyguardManager?.IsDeviceSecure ?? false;

        BiometricManager manager = BiometricManager.From(context);
        int biometricsResult = manager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);
        bool supportsBiometrics = biometricsResult == BiometricManager.BiometricSuccess;

        List<DeviceAuthType> types = [];
        if (supportsPin)
            types.Add(DeviceAuthType.PinOrPasscode);

        if (supportsBiometrics)
        {
            Android.Content.PM.PackageManager? pm = context.PackageManager;
            if (pm != null)
            {
                bool hasFingerprint = pm.HasSystemFeature(Android.Content.PM.PackageManager.FeatureFingerprint);
                bool hasFace = OperatingSystem.IsAndroidVersionAtLeast(29)
                    && pm.HasSystemFeature(Android.Content.PM.PackageManager.FeatureFace);
                bool hasIris = OperatingSystem.IsAndroidVersionAtLeast(29)
                    && pm.HasSystemFeature(Android.Content.PM.PackageManager.FeatureIris);

                if (hasFingerprint)
                    types.Add(DeviceAuthType.Fingerprint);
                if (hasFace)
                    types.Add(DeviceAuthType.Face);
                if (hasIris)
                    types.Add(DeviceAuthType.Iris);
            }
        }

        return new DeviceCapabilities(
            IsSupported: supportsPin || supportsBiometrics,
            SupportsBiometrics: supportsBiometrics,
            SupportsPin: supportsPin,
            AvailableTypes: types);
    }

    /// <summary>
    /// Encrypts and stores data bound to device hardware via Keystore-backed AES-256-GCM.
    /// Data is encrypted through a CryptoObject-bound Cipher so the key material never leaves
    /// the secure hardware (TEE/StrongBox) and can only be used after biometric verification.
    /// This binds the data to hardware — without successful authentication, decryption is
    /// physically impossible even on a rooted device.
    /// </summary>
    /// <param name="key">SharedPreferences key to store the encrypted blob under.</param>
    /// <param name="data">Plaintext bytes to encrypt (e.g., mnemonic seed).</param>
    /// <param name="type">Auth type — Biometric uses BiometricStrong, Pin uses DeviceCredential.</param>
    /// <param name="ct">Cancellation token — cancels the biometric prompt.</param>
    public Task StoreSecureAsync(string key, byte[] data, AuthenticationType type, CancellationToken ct = default)
    {
        TaskCompletionSource<object?> tcs = new();

        if (Platform.CurrentActivity is not AndroidX.Fragment.App.FragmentActivity activity)
            throw new InvalidOperationException("No activity available");

        bool isPin = type == AuthenticationType.Pin;

        if (isPin)
            EnsurePinCryptoKey();
        else
            EnsureCryptoKey();

        Cipher cipher = isPin
            ? GetCipher(PinCryptoKeyAlias, CipherMode.EncryptMode)
            : GetCipher(CryptoKeyAlias, CipherMode.EncryptMode);

        BiometricPrompt.PromptInfo.Builder builder = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Buriza Wallet")
            .SetSubtitle("Authenticate to save credentials");

        if (isPin)
        {
            builder.SetAllowedAuthenticators(BiometricManager.Authenticators.DeviceCredential);
        }
        else
        {
            builder.SetNegativeButtonText("Cancel");
            builder.SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong);
        }

        BiometricPrompt.PromptInfo promptInfo = builder.Build();

        IExecutor? executor = ContextCompat.GetMainExecutor(activity);
        if (executor == null)
            throw new InvalidOperationException("Could not get main executor");

        CryptoStoreCallback callback = new(tcs, data, key, GetDataPreferences());
        BiometricPrompt biometricPrompt = new(activity, executor, callback);
        biometricPrompt.Authenticate(promptInfo, new BiometricPrompt.CryptoObject(cipher));

        ct.Register(() =>
        {
            biometricPrompt.CancelAuthentication();
            tcs.TrySetCanceled();
        });

        return tcs.Task;
    }

    /// <summary>
    /// Retrieves and decrypts hardware-bound data. Loads the ciphertext from SharedPreferences,
    /// parses the IV, then prompts BiometricPrompt with a decrypt-mode CryptoObject.
    /// Decryption only succeeds if the user authenticates — the Keystore hardware enforces this.
    /// </summary>
    /// <param name="key">SharedPreferences key where the encrypted blob is stored.</param>
    /// <param name="reason">Subtitle text shown in the BiometricPrompt dialog.</param>
    /// <param name="type">Auth type — determines which Keystore alias and prompt mode to use.</param>
    /// <param name="ct">Cancellation token — cancels the biometric prompt.</param>
    /// <returns>Decrypted plaintext bytes, or null if data not found or authentication fails.</returns>
    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, AuthenticationType type, CancellationToken ct = default)
    {
        TaskCompletionSource<byte[]?> tcs = new();

        if (Platform.CurrentActivity is not AndroidX.Fragment.App.FragmentActivity activity)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        (byte[] iv, byte[] ciphertext)? parsed = LoadEncryptedBlob(key);
        if (parsed is null)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        (byte[] iv, byte[] ciphertext) = parsed.Value;

        bool isPin = type == AuthenticationType.Pin;
        string alias = isPin ? PinCryptoKeyAlias : CryptoKeyAlias;

        if (isPin)
            EnsurePinCryptoKey();
        else
            EnsureCryptoKey();

        Cipher cipher = GetCipher(alias, CipherMode.DecryptMode, iv);

        BiometricPrompt.PromptInfo.Builder builder = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Buriza Wallet")
            .SetSubtitle(reason);

        if (isPin)
        {
            builder.SetAllowedAuthenticators(BiometricManager.Authenticators.DeviceCredential);
        }
        else
        {
            builder.SetNegativeButtonText("Cancel");
            builder.SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong);
        }

        BiometricPrompt.PromptInfo promptInfo = builder.Build();

        IExecutor? executor = ContextCompat.GetMainExecutor(activity);
        if (executor == null)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        CryptoRetrieveCallback callback = new(tcs, ciphertext);
        BiometricPrompt biometricPrompt = new(activity, executor, callback);
        biometricPrompt.Authenticate(promptInfo, new BiometricPrompt.CryptoObject(cipher));

        ct.Register(() =>
        {
            biometricPrompt.CancelAuthentication();
            tcs.TrySetResult(null);
        });

        return tcs.Task;
    }

    private (byte[] iv, byte[] ciphertext)? LoadEncryptedBlob(string key)
    {
        ISharedPreferences prefs = GetDataPreferences();
        string? stored = prefs.GetString(key, null);
        if (string.IsNullOrEmpty(stored))
            return null;

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(stored);
        }
        catch (FormatException)
        {
            return null;
        }

        if (raw.Length < 2)
            return null;

        int ivLength = raw[0];
        if (raw.Length < 1 + ivLength)
            return null;

        byte[] iv = raw.AsSpan(1, ivLength).ToArray();
        byte[] ciphertext = raw.AsSpan(1 + ivLength).ToArray();
        return (iv, ciphertext);
    }

    /// <summary>Removes the encrypted blob from SharedPreferences. Does not delete the Keystore key.</summary>
    /// <param name="key">SharedPreferences key to remove.</param>
    /// <param name="type">Auth type (unused — removal doesn't require authentication).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task RemoveSecureAsync(string key, AuthenticationType type, CancellationToken ct = default)
    {
        ISharedPreferences prefs = GetDataPreferences();
        ISharedPreferencesEditor? editor = prefs.Edit();
        editor?.Remove(key);
        editor?.Apply();
        return Task.CompletedTask;
    }

    #region Keystore Crypto Key

    /// <summary>Ensures the biometric Keystore key exists. Invalidated when biometric enrollment changes.</summary>
    private static void EnsureCryptoKey()
        => EnsureKeystoreKey(CryptoKeyAlias, (int)KeyPropertiesAuthType.BiometricStrong);

    /// <summary>Ensures the PIN/passcode Keystore key exists. Not invalidated on enrollment change.</summary>
    private static void EnsurePinCryptoKey()
        => EnsureKeystoreKey(PinCryptoKeyAlias, (int)KeyPropertiesAuthType.DeviceCredential);

    /// <summary>
    /// Creates an AES-256-GCM key in Android Keystore if it doesn't already exist.
    /// Tries StrongBox (dedicated hardware chip) first, falls back to TEE (on-chip secure zone).
    /// Biometric keys are automatically invalidated on enrollment change; credential keys are not.
    /// </summary>
    /// <param name="alias">Keystore alias identifying the key.</param>
    /// <param name="authType">Keystore auth type flag (BiometricStrong or DeviceCredential).</param>
    private static void EnsureKeystoreKey(string alias, int authType)
    {
        KeyStore keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new InvalidOperationException("AndroidKeyStore not available");
        keyStore.Load(null);

        if (keyStore.ContainsAlias(alias))
            return;

        if (TryCreateKeystoreKey(alias, authType, strongBoxBacked: true))
            return;

        if (TryCreateKeystoreKey(alias, authType, strongBoxBacked: false))
            return;

        throw new InvalidOperationException($"Failed to create Android Keystore key: {alias}");
    }

    private static bool TryCreateKeystoreKey(string alias, int authType, bool strongBoxBacked)
    {
        try
        {
            KeyGenerator keyGenerator = KeyGenerator.GetInstance(
                KeyProperties.KeyAlgorithmAes,
                "AndroidKeyStore")
                ?? throw new InvalidOperationException("KeyGenerator not available");

            KeyGenParameterSpec.Builder builder = new(
                alias,
                KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);

            builder.SetBlockModes(KeyProperties.BlockModeGcm);
            builder.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone);
            builder.SetKeySize(256);
            builder.SetUserAuthenticationRequired(true);
            builder.SetUserAuthenticationParameters(0, authType);
            builder.SetInvalidatedByBiometricEnrollment(authType == (int)KeyPropertiesAuthType.BiometricStrong);

            if (strongBoxBacked)
                builder.SetIsStrongBoxBacked(true);

            keyGenerator.Init(builder.Build());
            keyGenerator.GenerateKey();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Loads the Keystore key by alias and initializes an AES/GCM/NoPadding Cipher.
    /// Encrypt mode generates a fresh IV; decrypt mode requires the original IV.
    /// </summary>
    /// <param name="alias">Keystore alias of the AES key.</param>
    /// <param name="mode">Encrypt or Decrypt.</param>
    /// <param name="iv">IV bytes required for decryption. Null for encryption (Cipher generates its own).</param>
    /// <returns>An initialized Cipher ready for DoFinal inside a CryptoObject callback.</returns>
    private static Cipher GetCipher(string alias, CipherMode mode, byte[]? iv = null)
    {
        KeyStore keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new InvalidOperationException("AndroidKeyStore not available");
        keyStore.Load(null);

        IKey secretKey = (keyStore.GetEntry(alias, null) as KeyStore.SecretKeyEntry)?.SecretKey
            ?? throw new InvalidOperationException($"Crypto key not found in Keystore: {alias}");

        Cipher cipher = Cipher.GetInstance("AES/GCM/NoPadding")
            ?? throw new InvalidOperationException("AES/GCM/NoPadding cipher not available");

        if (mode == CipherMode.DecryptMode && iv != null)
            cipher.Init(CipherMode.DecryptMode, secretKey, new GCMParameterSpec(128, iv));
        else
            cipher.Init(CipherMode.EncryptMode, secretKey);

        return cipher;
    }

    #endregion

    private ISharedPreferences GetDataPreferences()
    {
        return _dataPrefs ??= Platform.AppContext.GetSharedPreferences(DataPrefName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Failed to get data preferences");
    }

    #region Biometric Callbacks

    /// <summary>
    /// Callback for StoreSecureAsync — encrypts data with the CryptoObject Cipher on success.
    /// </summary>
    private class CryptoStoreCallback(
        TaskCompletionSource<object?> tcs,
        byte[] plaintext,
        string key,
        ISharedPreferences prefs) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            try
            {
                Cipher cipher = result.CryptoObject?.Cipher
                    ?? throw new InvalidOperationException("CryptoObject cipher is null");

                byte[]? ciphertext = cipher.DoFinal(plaintext);
                if (ciphertext == null)
                    throw new InvalidOperationException("Encryption returned null");

                byte[]? iv = cipher.GetIV();
                if (iv == null || iv.Length == 0 || iv.Length > 255)
                    throw new InvalidOperationException("Invalid IV from cipher");

                // Format: 1-byte IV length || IV || ciphertext (includes GCM tag)
                byte[] combined = new byte[1 + iv.Length + ciphertext.Length];
                combined[0] = (byte)iv.Length;
                iv.CopyTo(combined.AsSpan(1));
                ciphertext.CopyTo(combined.AsSpan(1 + iv.Length));

                ISharedPreferencesEditor? editor = prefs.Edit();
                editor?.PutString(key, Convert.ToBase64String(combined));
                editor?.Apply();

                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(new InvalidOperationException("Encryption failed after authentication", ex));
            }
        }

        // Intentionally empty: per-attempt notification, user can retry.
        // Terminal failures go through OnAuthenticationError.
        public override void OnAuthenticationFailed() { }

        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
        {
            DeviceAuthResult error = MapError(errorCode, errString);
            tcs.TrySetException(new InvalidOperationException($"Device authentication required: {error.ErrorMessage}"));
        }
    }

    /// <summary>
    /// Callback for RetrieveSecureAsync — decrypts data with the CryptoObject Cipher on success.
    /// </summary>
    private class CryptoRetrieveCallback(
        TaskCompletionSource<byte[]?> tcs,
        byte[] ciphertext) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            try
            {
                Cipher cipher = result.CryptoObject?.Cipher
                    ?? throw new InvalidOperationException("CryptoObject cipher is null");

                byte[]? plaintext = cipher.DoFinal(ciphertext);
                tcs.TrySetResult(plaintext);
            }
            catch (Java.Security.GeneralSecurityException)
            {
                tcs.TrySetResult(null);
            }
            catch (Exception)
            {
                tcs.TrySetResult(null);
            }
        }

        // Intentionally empty: per-attempt notification, user can retry.
        // Terminal failures go through OnAuthenticationError.
        public override void OnAuthenticationFailed() { }

        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
            => tcs.TrySetResult(null);
    }

    private static DeviceAuthResult MapError(int errorCode, Java.Lang.ICharSequence? errString)
    {
        DeviceAuthError error = errorCode switch
        {
            BiometricPrompt.ErrorCanceled or BiometricPrompt.ErrorUserCanceled or BiometricPrompt.ErrorNegativeButton
                => DeviceAuthError.Cancelled,
            BiometricPrompt.ErrorLockout or BiometricPrompt.ErrorLockoutPermanent => DeviceAuthError.LockedOut,
            BiometricPrompt.ErrorNoBiometrics => DeviceAuthError.NotEnrolled,
            BiometricPrompt.ErrorHwNotPresent or BiometricPrompt.ErrorHwUnavailable => DeviceAuthError.NotAvailable,
            BiometricPrompt.ErrorTimeout => DeviceAuthError.Failure,
            _ => DeviceAuthError.Failure
        };
        return DeviceAuthResult.Failed(error, errString?.ToString() ?? "Authentication failed");
    }

    #endregion
}
#endif
