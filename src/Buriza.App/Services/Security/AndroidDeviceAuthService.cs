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
    private const string DataPrefName = "buriza_secure_data";

    private ISharedPreferences? _dataPrefs;

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        Context? context = Platform.CurrentActivity ?? Platform.AppContext;
        BiometricManager manager = BiometricManager.From(context);
        int result = manager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong | BiometricManager.Authenticators.DeviceCredential);
        return Task.FromResult(result == BiometricManager.BiometricSuccess);
    }

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

    public Task<DeviceAuthResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        TaskCompletionSource<DeviceAuthResult> tcs = new();

        if (Platform.CurrentActivity is not AndroidX.Fragment.App.FragmentActivity activity)
        {
            tcs.SetResult(DeviceAuthResult.Failed(DeviceAuthError.NotAvailable, "No activity available"));
            return tcs.Task;
        }

        BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Buriza Wallet")
            .SetSubtitle(reason)
            .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong | BiometricManager.Authenticators.DeviceCredential)
            .Build();

        IExecutor? executor = ContextCompat.GetMainExecutor(activity);
        if (executor == null)
        {
            tcs.SetResult(DeviceAuthResult.Failed(DeviceAuthError.NotAvailable, "Could not get main executor"));
            return tcs.Task;
        }

        BiometricPrompt biometricPrompt = new(activity, executor, new AuthOnlyCallback(tcs));
        biometricPrompt.Authenticate(promptInfo);

        ct.Register(() =>
        {
            biometricPrompt.CancelAuthentication();
            tcs.TrySetResult(DeviceAuthResult.Failed(DeviceAuthError.Cancelled, "Authentication cancelled"));
        });

        return tcs.Task;
    }

    public Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
    {
        TaskCompletionSource<object?> tcs = new();

        if (Platform.CurrentActivity is not AndroidX.Fragment.App.FragmentActivity activity)
            throw new InvalidOperationException("No activity available");

        EnsureCryptoKey();
        Cipher cipher = GetCipher(CipherMode.EncryptMode);

        BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Buriza Wallet")
            .SetSubtitle("Authenticate to save credentials")
            .SetNegativeButtonText("Cancel")
            .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong)
            .Build();

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

    public Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
    {
        TaskCompletionSource<byte[]?> tcs = new();

        if (Platform.CurrentActivity is not AndroidX.Fragment.App.FragmentActivity activity)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        ISharedPreferences prefs = GetDataPreferences();
        string? stored = prefs.GetString(key, null);
        if (string.IsNullOrEmpty(stored))
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(stored);
        }
        catch (FormatException)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        if (raw.Length < 2)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        int ivLength = raw[0];
        if (raw.Length < 1 + ivLength)
        {
            tcs.SetResult(null);
            return tcs.Task;
        }

        byte[] iv = raw.AsSpan(1, ivLength).ToArray();
        byte[] ciphertext = raw.AsSpan(1 + ivLength).ToArray();

        EnsureCryptoKey();
        Cipher cipher = GetCipher(CipherMode.DecryptMode, iv);

        BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Buriza Wallet")
            .SetSubtitle(reason)
            .SetNegativeButtonText("Cancel")
            .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong)
            .Build();

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

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        ISharedPreferences prefs = GetDataPreferences();
        ISharedPreferencesEditor? editor = prefs.Edit();
        editor?.Remove(key);
        editor?.Apply();
        return Task.CompletedTask;
    }

    #region Keystore Crypto Key

    private static void EnsureCryptoKey()
    {
        KeyStore keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new InvalidOperationException("AndroidKeyStore not available");
        keyStore.Load(null);

        if (keyStore.ContainsAlias(CryptoKeyAlias))
            return;

        if (TryCreateCryptoKey(strongBoxBacked: true))
            return;

        if (TryCreateCryptoKey(strongBoxBacked: false))
            return;

        throw new InvalidOperationException("Failed to create Android Keystore crypto key");
    }

    private static bool TryCreateCryptoKey(bool strongBoxBacked)
    {
        try
        {
            KeyGenerator keyGenerator = KeyGenerator.GetInstance(
                KeyProperties.KeyAlgorithmAes,
                "AndroidKeyStore")
                ?? throw new InvalidOperationException("KeyGenerator not available");

            KeyGenParameterSpec.Builder builder = new(
                CryptoKeyAlias,
                KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);

            builder.SetBlockModes(KeyProperties.BlockModeGcm);
            builder.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone);
            builder.SetKeySize(256);
            builder.SetUserAuthenticationRequired(true);
            builder.SetUserAuthenticationParameters(0, (int)KeyPropertiesAuthType.BiometricStrong);
            builder.SetInvalidatedByBiometricEnrollment(true);

            if (strongBoxBacked)
                builder.SetIsStrongBoxBacked(true);

            keyGenerator.Init(builder.Build());
            keyGenerator.GenerateKey();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Cipher GetCipher(CipherMode mode, byte[]? iv = null)
    {
        KeyStore keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new InvalidOperationException("AndroidKeyStore not available");
        keyStore.Load(null);

        IKey secretKey = (keyStore.GetEntry(CryptoKeyAlias, null) as KeyStore.SecretKeyEntry)?.SecretKey
            ?? throw new InvalidOperationException("Crypto key not found in Keystore");

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
    /// Callback for AuthenticateAsync — no crypto operation, just auth result.
    /// </summary>
    private class AuthOnlyCallback(TaskCompletionSource<DeviceAuthResult> tcs) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
            => tcs.TrySetResult(DeviceAuthResult.Succeeded());

        // Intentionally empty: OnAuthenticationFailed fires per-attempt (e.g., fingerprint mismatch).
        // The user can retry. Android guarantees OnAuthenticationError is called on terminal failure
        // (lockout, cancel, timeout), which resolves the tcs.
        public override void OnAuthenticationFailed() { }

        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
            => tcs.TrySetResult(MapError(errorCode, errString));
    }

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
