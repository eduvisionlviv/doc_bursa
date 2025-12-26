using System;
    using System.Security.Cryptography;
using System.Text;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для безпечного шифрування та дешифрування токенів API
    /// Використовує Windows DPAPI (Data Protection API)
    /// </summary>
    public class EncryptionService
    {
        /// <summary>
        /// Шифрує текст використовуючи DPAPI
        /// </summary>
        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            try
            {
                var data = Encoding.UTF8.GetBytes(plainText);
                var encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                throw new Exception($"Помилка шифрування: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Дешифрує текст використовуючи DPAPI
        /// </summary>
        public string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return encryptedText;

            try
            {
                var data = Convert.FromBase64String(encryptedText);
                var decrypted = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (FormatException)
            {
                // Якщо не є Base64 - вважаємо що не зашифровано
                return encryptedText;
            }
            catch (CryptographicException)
            {
                // Якщо не вдалося дешифрувати - вважаємо що не зашифровано
                return encryptedText;
            }
            catch (Exception ex)
            {
                throw new Exception($"Помилка дешифрування: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Перевіряє чи текст зашифровано
        /// </summary>
        public bool IsEncrypted(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                var data = Convert.FromBase64String(text);
                ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
