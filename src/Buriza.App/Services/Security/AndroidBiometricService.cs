#if ANDROID
using Android.Content;
using Android.OS;
using Android.Security.Keystore;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using AndroidX.Security.Crypto;
using Java.Util.Concurrent;
using Buriza.Core.Interfaces.Security;

namespace Buriza.App.Services.Security;

/// <summary>
/// Android biometric service using BiometricPrompt and EncryptedSharedPreferences.
///
/// Security model:
/// - Uses BiometricStrong authenticator (Class 3 biometrics - highest security)
/// - EncryptedSharedPreferences with AES-256-GCM encryption
/// - MasterKey stored in Android Keystore (hardware-backed when available)
/// - Biometric authentication required before accessing encrypted data
///
/// References:
/// - https://developer.android.com/training/sign-in/biometric-auth
/// - https://developer.android.com/topic/security/data
/// - https://stytch.com/blog/android-keystore-pitfalls-and-best-practices/
/// </summary>
public class AndroidBiometricService : IBiometricService
{
    private const string PrefsFileName = "buriza_biometric_prefs";

    // Cache the encrypted prefs instance to avoid repeated initialization
    private ISharedPreferences? _encryptedPrefs;
    private readonly object _prefsLock = new();

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        Context? context = Platform.CurrentActivity ?? Platform.AppContext;
        BiometricManager manager = BiometricManager.From(context);
        int result = manager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);
        return Task.FromResult(result == BiometricManager.BiometricSuccess);
    }

    public Task<BiometricType?> GetBiometricTypeAsync(CancellationToken ct = default)
    {
        Context? context = Platform.CurrentActivity ?? Platform.AppContext;
        BiometricManager manager = BiometricManager.From(context);
        int result = manager.CanAuthenticate(BiometricManager.Authenticators.BiometricStrong);

        if (result != BiometricManager.BiometricSuccess)
            return Task.FromResult<BiometricType?>(null);

        // Android doesn't expose specific biometric type through BiometricManager
        // We check PackageManager for available features
        Android.Content.PM.PackageManager? pm = context.PackageManager;
        if (pm != null)
        {
            bool hasFace = pm.HasSystemFeature(Android.Content.PM.PackageManager.FeatureFace);
            bool hasFingerprint = pm.HasSystemFeature(Android.Content.PM.PackageManager.FeatureFingerprint);

            if (hasFace && !hasFingerprint)
                return Task.FromResult<BiometricType?>(BiometricType.FaceRecognition);
        }

        // Default to fingerprint as it's most common
        return Task.FromResult<BiometricType?>(BiometricType.Fingerprint);
    }

    public Task<BiometricResult> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        TaskCompletionSource<BiometricResult> tcs = new();

        AndroidX.Fragment.App.FragmentActivity? activity = Platform.CurrentActivity as AndroidX.Fragment.App.FragmentActivity;
        if (activity == null)
        {
            tcs.SetResult(BiometricResult.Failed(BiometricError.NotAvailable, "No activity available"));
            return tcs.Task;
        }

        BiometricPrompt.PromptInfo promptInfo = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Buriza Wallet")
            .SetSubtitle(reason)
            .SetNegativeButtonText("Cancel")
            .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricStrong)
            .Build();

        IExecutor executor = ContextCompat.GetMainExecutor(activity);

        BiometricPrompt biometricPrompt = new(activity, executor, new BiometricCallback(tcs));
        biometricPrompt.Authenticate(promptInfo);

        ct.Register(() =>
        {
            biometricPrompt.CancelAuthentication();
            tcs.TrySetResult(BiometricResult.Failed(BiometricError.Cancelled, "Authentication cancelled"));
        });

        return tcs.Task;
    }

    public async Task StoreSecureAsync(string key, byte[] data, CancellationToken ct = default)
    {
        // First authenticate with biometrics
        BiometricResult authResult = await AuthenticateAsync("Authenticate to save credentials", ct);
        if (!authResult.Success)
            throw new InvalidOperationException($"Biometric authentication required: {authResult.ErrorMessage}");

        // Store in encrypted shared preferences
        ISharedPreferences prefs = GetEncryptedPreferences();
        ISharedPreferencesEditor? editor = prefs.Edit();
        editor?.PutString(key, Convert.ToBase64String(data));
        editor?.Apply();
    }

    public async Task<byte[]?> RetrieveSecureAsync(string key, string reason, CancellationToken ct = default)
    {
        // First authenticate with biometrics
        BiometricResult authResult = await AuthenticateAsync(reason, ct);
        if (!authResult.Success)
            return null;

        // Retrieve from encrypted shared preferences
        ISharedPreferences prefs = GetEncryptedPreferences();
        string? base64Data = prefs.GetString(key, null);

        if (string.IsNullOrEmpty(base64Data))
            return null;

        return Convert.FromBase64String(base64Data);
    }

    public Task RemoveSecureAsync(string key, CancellationToken ct = default)
    {
        ISharedPreferences prefs = GetEncryptedPreferences();
        ISharedPreferencesEditor? editor = prefs.Edit();
        editor?.Remove(key);
        editor?.Apply();
        return Task.CompletedTask;
    }

    public Task<bool> HasSecureDataAsync(string key, CancellationToken ct = default)
    {
        ISharedPreferences prefs = GetEncryptedPreferences();
        return Task.FromResult(prefs.Contains(key));
    }

    private ISharedPreferences GetEncryptedPreferences()
    {
        if (_encryptedPrefs != null)
            return _encryptedPrefs;

        lock (_prefsLock)
        {
            if (_encryptedPrefs != null)
                return _encryptedPrefs;

            Context context = Platform.AppContext;

            // Create MasterKey with AES-256-GCM scheme
            // The key is stored in Android Keystore (hardware-backed when available via StrongBox)
            // SetUserAuthenticationRequired + SetUserAuthenticationValidityDurationSeconds(-1) requires
            // biometric auth for every cryptographic operation.
            // SetInvalidatedByBiometricEnrollment ensures the key is invalidated if new biometrics are enrolled.
            KeyGenParameterSpec spec = new KeyGenParameterSpec.Builder(
                    MasterKey.DefaultMasterKeyAlias,
                    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .SetKeySize(256)
                .SetUserAuthenticationRequired(true)
                .SetUserAuthenticationValidityDurationSeconds(-1) // Require auth for every use
                .SetInvalidatedByBiometricEnrollment(true) // Invalidate if biometrics change
                .Build();

            MasterKey masterKey = new MasterKey.Builder(context)
                .SetKeyGenParameterSpec(spec)
                .Build();

            // Create EncryptedSharedPreferences
            // - Keys are encrypted with AES-256-SIV (deterministic encryption for key lookup)
            // - Values are encrypted with AES-256-GCM (authenticated encryption)
            _encryptedPrefs = EncryptedSharedPreferences.Create(
                context,
                PrefsFileName,
                masterKey,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.Aes256Siv,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.Aes256Gcm);

            return _encryptedPrefs;
        }
    }

    private class BiometricCallback(TaskCompletionSource<BiometricResult> tcs) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
        {
            tcs.TrySetResult(BiometricResult.Succeeded());
        }

        public override void OnAuthenticationFailed()
        {
            // Don't complete task - user can try again
            // This is called when biometric doesn't match but user can retry
        }

        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
        {
            BiometricError error = errorCode switch
            {
                BiometricPrompt.ErrorCanceled or BiometricPrompt.ErrorUserCanceled or BiometricPrompt.ErrorNegativeButton
                    => BiometricError.Cancelled,
                BiometricPrompt.ErrorLockout => BiometricError.LockedOut,
                BiometricPrompt.ErrorLockoutPermanent => BiometricError.LockedOut,
                BiometricPrompt.ErrorNoBiometrics => BiometricError.NotEnrolled,
                BiometricPrompt.ErrorHwNotPresent or BiometricPrompt.ErrorHwUnavailable => BiometricError.NotAvailable,
                BiometricPrompt.ErrorNoSpace => BiometricError.Unknown,
                BiometricPrompt.ErrorTimeout => BiometricError.Failed,
                BiometricPrompt.ErrorVendor => BiometricError.Unknown,
                _ => BiometricError.Unknown
            };

            tcs.TrySetResult(BiometricResult.Failed(error, errString?.ToString()));
        }
    }
}
#endif
