#if ANDROID
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Security.Keystore;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using Buriza.Core.Interfaces.Security;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Security;
using Java.Util.Concurrent;
using Xamarin.Google.Crypto.Tink;
using Xamarin.Google.Crypto.Tink.Aead;
using Xamarin.Google.Crypto.Tink.Integration.Android;
using Java.Security;
using Javax.Crypto;

namespace Buriza.App.Services.Security;

/// <summary>
/// Android device-auth service using BiometricPrompt and Google Tink for encryption.
///
/// Security model:
/// - Uses BiometricStrong authenticator (Class 3 biometrics - highest security)
/// - Data is encrypted with AES-256-GCM using Google Tink AEAD
/// - Tink keyset is stored encrypted in SharedPreferences, master key in Android Keystore
/// - Device authentication gates access to encrypted data
/// - Key is invalidated if biometric enrollment changes (via Keystore binding)
///
/// This follows Google's recommended approach for secure storage post-EncryptedSharedPreferences
/// deprecation: DataStore + Tink pattern.
///
/// References:
/// - https://developer.android.com/training/sign-in/biometric-auth
/// - https://developers.google.com/tink
/// - https://www.droidcon.com/2025/12/16/goodbye-encryptedsharedpreferences-a-2026-migration-guide/
/// </summary>
public class AndroidDeviceAuthService : IDeviceAuthService
{
    private const string KeysetName = "buriza_tink_keyset";
    private const string KeysetPrefName = "buriza_tink_prefs";
    private const string MasterKeyAlias = "buriza_master_key";
    private const string MasterKeyUri = "android-keystore://buriza_master_key";
    private const string DataPrefName = "buriza_secure_data";

    private readonly object _aeadLock = new();
    private IAead? _aead;
    private ISharedPreferences? _dataPrefs;

    static AndroidDeviceAuthService()
    {
        // Register Tink AEAD configuration
        AeadConfig.Register();
    }

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

        BiometricPrompt biometricPrompt = new(activity, executor, new BiometricCallback(tcs));
        biometricPrompt.Authenticate(promptInfo);

        ct.Register(() =>
        {
            biometricPrompt.CancelAuthentication();
            tcs.TrySetResult(DeviceAuthResult.Failed(DeviceAuthError.Cancelled, "Authentication cancelled"));
        });

        return tcs.Task;
    }

    public async Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
    {
        // First authenticate with device security
        DeviceAuthResult authResult = await AuthenticateAsync("Authenticate to save credentials", ct);
        if (!authResult.Success)
            throw new InvalidOperationException($"Device authentication required: {authResult.ErrorMessage}");

        // Encrypt using Tink AEAD
        IAead aead = GetAead();
        byte[]? ciphertext = aead.Encrypt(data, System.Text.Encoding.UTF8.GetBytes(key));
        if (ciphertext == null)
            throw new InvalidOperationException("Encryption failed");

        // Store encrypted data
        ISharedPreferences prefs = GetDataPreferences();
        ISharedPreferencesEditor? editor = prefs.Edit();
        editor?.PutString(key, Convert.ToBase64String(ciphertext));
        editor?.Apply();
    }

    public async Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
    {
        // First authenticate with device security
        DeviceAuthResult authResult = await AuthenticateAsync(reason, ct);
        if (!authResult.Success)
            return null;

        // Retrieve encrypted data
        ISharedPreferences prefs = GetDataPreferences();
        string? base64Ciphertext = prefs.GetString(key, null);

        if (string.IsNullOrEmpty(base64Ciphertext))
            return null;

        try
        {
            byte[] ciphertext = Convert.FromBase64String(base64Ciphertext);

            // Decrypt using Tink AEAD
            IAead aead = GetAead();
            byte[]? plaintext = aead.Decrypt(ciphertext, System.Text.Encoding.UTF8.GetBytes(key));
            return plaintext;
        }
        catch (Java.Security.GeneralSecurityException)
        {
            // Decryption failed - key may have changed, data corrupted, or key invalidated
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        ISharedPreferences prefs = GetDataPreferences();
        ISharedPreferencesEditor? editor = prefs.Edit();
        editor?.Remove(key);
        editor?.Apply();
        return Task.CompletedTask;
    }

    #region Tink AEAD Setup

    /// <summary>
    /// Gets or creates the Tink AEAD primitive for encryption/decryption.
    /// The keyset is stored encrypted in SharedPreferences, with the master key in Android Keystore.
    /// </summary>
    private IAead GetAead()
    {
        if (_aead != null)
            return _aead;

        lock (_aeadLock)
        {
            if (_aead != null)
                return _aead;

            Context context = Platform.AppContext;

            EnsureMasterKey();

            // Build AndroidKeysetManager - this manages the Tink keyset
            // The keyset is encrypted and stored in SharedPreferences
            // The encryption key for the keyset is stored in Android Keystore
            AndroidKeysetManager keysetManager = new AndroidKeysetManager.Builder()
                .WithKeyTemplate(KeyTemplates.Get("AES256_GCM"))!
                .WithSharedPref(context, KeysetName, KeysetPrefName)!
                .WithMasterKeyUri(MasterKeyUri)!
                .Build()
                ?? throw new InvalidOperationException("Failed to create keyset manager");

            KeysetHandle keysetHandle = keysetManager.KeysetHandle
                ?? throw new InvalidOperationException("Failed to get keyset handle");

            _aead = keysetHandle.GetPrimitive(RegistryConfiguration.Get(), Java.Lang.Class.FromType(typeof(IAead)))?.JavaCast<IAead>()
                ?? throw new InvalidOperationException("Failed to create AEAD primitive");

            return _aead;
        }
    }

    private static void EnsureMasterKey()
    {
        try
        {
            KeyStore keyStore = KeyStore.GetInstance("AndroidKeyStore")
                ?? throw new InvalidOperationException("AndroidKeyStore not available");
            keyStore.Load(null);
            if (keyStore.ContainsAlias(MasterKeyAlias))
                return;

            // Prefer StrongBox when available, then gracefully fallback
            if (TryCreateMasterKey(strongBoxBacked: true))
                return;

            if (TryCreateMasterKey(strongBoxBacked: false))
                return;

            throw new InvalidOperationException("Failed to create Android Keystore master key");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create Android Keystore master key", ex);
        }
    }

    private static bool TryCreateMasterKey(bool strongBoxBacked)
    {
        try
        {
            KeyGenerator keyGenerator = KeyGenerator.GetInstance(
                KeyProperties.KeyAlgorithmAes,
                "AndroidKeyStore")
                ?? throw new InvalidOperationException("KeyGenerator not available for AES in AndroidKeyStore");

            KeyGenParameterSpec.Builder builder = new(
                MasterKeyAlias,
                KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt);

            builder.SetBlockModes(KeyProperties.BlockModeGcm);
            builder.SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone);
            builder.SetKeySize(256);
            builder.SetUserAuthenticationRequired(true);
            builder.SetUserAuthenticationParameters(15,
                (int)(KeyPropertiesAuthType.BiometricStrong | KeyPropertiesAuthType.DeviceCredential));
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

    #endregion

    private ISharedPreferences GetDataPreferences()
    {
        return _dataPrefs ??= Platform.AppContext.GetSharedPreferences(DataPrefName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("Failed to get data preferences");
    }

    private class BiometricCallback(TaskCompletionSource<DeviceAuthResult> tcs) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
            => tcs.TrySetResult(DeviceAuthResult.Succeeded());

        public override void OnAuthenticationFailed() { }

        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
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
            tcs.TrySetResult(DeviceAuthResult.Failed(error, errString?.ToString() ?? "Authentication failed"));
        }
    }
}
#endif
