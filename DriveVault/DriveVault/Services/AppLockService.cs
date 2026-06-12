using DriveVault.Data;
using System;
using System.Security.Cryptography;
using System.Text;

namespace DriveVault.Services
{
    public static class AppLockService
    {
        private const string PasscodeKey = "app_passcode";
        private const string AppLockKey = "app_lock_enabled";
        private const string FailedCountKey = "failed_attempts";
        private const string RecoveryKeyKey = "app_recovery_key";

        // ─── Status ───────────────────────────────────────────────

        public static bool HasPasscode =>
            !string.IsNullOrEmpty(
                DatabaseHelper.GetSetting(PasscodeKey, ""));

        public static bool IsAppLockEnabled =>
            DatabaseHelper.GetSetting(AppLockKey, "false") == "true";

        // ─── Set Passcode ─────────────────────────────────────────

        public static string SetPasscode(string passcode)
        {
            // Hash passcode
            var hashed = HashInput(passcode);
            DatabaseHelper.SaveSetting(PasscodeKey, hashed);

            // ✅ Recovery key generate చేయండి
            var recoveryKey = GenerateRecoveryKey();
            var hashedRecovery = HashInput(recoveryKey);
            DatabaseHelper.SaveSetting(RecoveryKeyKey, hashedRecovery);

            DatabaseHelper.LogActivity(
                "passcode_changed", "", "",
                "App passcode updated", "System");

            // Plain recovery key return చేయండి — user కి show చేయడానికి
            // DB లో hashed version మాత్రమే store అవుతుంది
            return recoveryKey;
        }

        public static void RemovePasscode()
        {
            DatabaseHelper.SaveSetting(PasscodeKey, "");
            DatabaseHelper.SaveSetting(RecoveryKeyKey, "");
            DatabaseHelper.SaveSetting(AppLockKey, "false");
            DatabaseHelper.SaveSetting(FailedCountKey, "0");

            DatabaseHelper.LogActivity(
                "passcode_removed", "", "",
                "App passcode removed", "System");
        }

        public static void SetAppLock(bool enabled)
        {
            DatabaseHelper.SaveSetting(AppLockKey,
                enabled ? "true" : "false");
        }

        // ─── Verify Passcode ──────────────────────────────────────

        public static bool VerifyPasscode(string input)
        {
            var stored = DatabaseHelper.GetSetting(PasscodeKey, "");
            if (string.IsNullOrEmpty(stored)) return true;

            var correct = HashInput(input) == stored;

            if (!correct)
            {
                var count = int.Parse(
                    DatabaseHelper.GetSetting(FailedCountKey, "0"));
                count++;
                DatabaseHelper.SaveSetting(FailedCountKey, count.ToString());

                DatabaseHelper.LogActivity(
                    "unauthorized_attempt", "", "",
                    $"Wrong passcode — attempt #{count}", "System");
            }
            else
            {
                DatabaseHelper.SaveSetting(FailedCountKey, "0");
            }

            return correct;
        }

        // ─── Recovery Key ─────────────────────────────────────────

        public static bool VerifyRecoveryKey(string input)
        {
            var stored = DatabaseHelper.GetSetting(RecoveryKeyKey, "");
            if (string.IsNullOrEmpty(stored)) return false;

            // Input normalize చేయండి — spaces, hyphens ignore
            var normalized = input.Trim()
                .Replace(" ", "")
                .Replace("-", "")
                .ToUpper();

            var correct = HashInput(normalized) == stored;

            if (!correct)
            {
                DatabaseHelper.LogActivity(
                    "unauthorized_attempt", "", "",
                    "Wrong recovery key attempt", "System");
            }

            return correct;
        }

        // ─── Reset via Recovery Key ───────────────────────────────

        public static string ResetPasscodeWithRecovery(
            string recoveryKey, string newPasscode)
        {
            if (!VerifyRecoveryKey(recoveryKey))
                return "invalid_recovery";

            if (string.IsNullOrEmpty(newPasscode) || newPasscode.Length < 4)
                return "invalid_passcode";

            // New passcode + new recovery key
            var newRecoveryKey = SetPasscode(newPasscode);

            DatabaseHelper.LogActivity(
                "passcode_reset", "", "",
                "Passcode reset via recovery key", "System");

            return newRecoveryKey;
        }

        // ─── Private Helpers ──────────────────────────────────────

        private static string GenerateRecoveryKey()
        {
            // Format: DV-XXXX-XXXX-XXXX (16 chars + prefix)
            var bytes = new byte[6];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            var hex = BitConverter.ToString(bytes)
                .Replace("-", "").ToUpper();

            // DV-A3F9-B2C1-D4E8
            return $"DV-{hex[..4]}-{hex[4..8]}-{hex[8..12]}";
        }

        private static string HashInput(string input)
        {
            // Normalize before hash
            var normalized = input.Trim()
                .Replace(" ", "")
                .Replace("-", "")
                .ToUpper();

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(
                normalized + "DriveVault_Salt_2026");
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}