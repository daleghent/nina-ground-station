using System;
using System.Security.Cryptography;
using System.Text;

namespace DaleGhent.NINA.GroundStation {
    public class Security {
        public static string Encrypt(string secret) {
            if (string.IsNullOrEmpty(secret)) {
                return string.Empty;
            }

            byte[] secretBytes = Encoding.ASCII.GetBytes(secret);
            byte[] cipherBytes = ProtectedData.Protect(secretBytes, null, DataProtectionScope.CurrentUser);

            return Convert.ToBase64String(cipherBytes);
        }

        public static string Decrypt(string cipher) {
            if (string.IsNullOrEmpty(cipher)) {
                return string.Empty;
            }

            try {
                byte[] cipherBytes = Convert.FromBase64String(cipher);
                byte[] passwordBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);

                return Encoding.ASCII.GetString(passwordBytes);
            } catch {
                return string.Empty;
            }
        }
    }
}