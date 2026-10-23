using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using Npgsql;

namespace KamatekCrm.Security
{
    /// <summary>
    /// Windows DPAPI (Data Protection API) tabanlı hassas veri şifreleme ve çözme yardımcısı.
    /// Hassas ayarlar diske "ENC:" ön ekiyle Base64 formatında kaydedilir.
    /// Geriye dönük uyumluluk: "ENC:" ile başlamayan eski düz metin veriler olduğu gibi okunur,
    /// ilk kaydetmede otomatik olarak şifrelenir.
    /// </summary>
    public static class DataProtectionHelper
    {
        public const string Prefix = "ENC:";

        /// <summary>
        /// Değerin "ENC:" ile şifrelenmiş olup olmadığını kontrol eder.
        /// </summary>
        public static bool IsEncrypted(string? input)
        {
            return !string.IsNullOrEmpty(input) && input.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Düz metni Windows DPAPI (CurrentUser) ile şifreleyip "ENC:" ön ekiyle Base64 olarak döndürür.
        /// Değer zaten şifreliyse ("ENC:" ile başlıyorsa) yeniden şifrelemez.
        /// </summary>
        public static string Protect(string? plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            if (IsEncrypted(plainText))
                return plainText;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] cipherBytes = ProtectedData.Protect(
                    plainBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                return Prefix + Convert.ToBase64String(cipherBytes);
            }
            catch
            {
                // DPAPI istisnası durumunda veriyi kaybetmemek için düz metni döndürür
                return plainText;
            }
        }

        /// <summary>
        /// "ENC:" ile başlayan şifreli veriyi çözer.
        /// "ENC:" ile başlamayan eski (düz metin) verileri geriye dönük uyumluluk için olduğu gibi döndürür.
        /// </summary>
        public static string Unprotect(string? encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            if (!IsEncrypted(encryptedText))
                return encryptedText; // Eski düz metin formatı — olduğu gibi döndür

            try
            {
                string base64 = encryptedText.Substring(Prefix.Length);
                byte[] cipherBytes = Convert.FromBase64String(base64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                // Çözme başarısızsa (farklı kullanıcı/makine) orijinal girdiyi döndür
                return encryptedText;
            }
        }

        /// <summary>
        /// PostgreSQL bağlantı dizesini inceler:
        /// 1. Tüm dize "ENC:" ile başlıyorsa tamamını çözer.
        /// 2. Dize içindeki Password parametresi "ENC:" içeriyorsa sadece parolayı çözer.
        /// 3. Düz metin ise olduğu gibi döndürür.
        /// </summary>
        public static string UnprotectConnectionString(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            if (IsEncrypted(connectionString))
            {
                return Unprotect(connectionString);
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                if (IsEncrypted(builder.Password))
                {
                    builder.Password = Unprotect(builder.Password);
                    return builder.ConnectionString;
                }
            }
            catch
            {
                // Standart dışı bağlantı dizesi veya çözümleme hatası
            }

            return connectionString;
        }

        /// <summary>
        /// PostgreSQL bağlantı dizesi içindeki Password değerini DPAPI ile şifreler.
        /// Parola zaten şifreliyse tekrar şifrelemez.
        /// </summary>
        public static string ProtectConnectionStringPassword(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrEmpty(builder.Password) && !IsEncrypted(builder.Password))
                {
                    builder.Password = Protect(builder.Password);
                    return builder.ConnectionString;
                }
            }
            catch
            {
                // Ayrıştırılamıyorsa ve şifresizse tamamını koru
                if (!IsEncrypted(connectionString))
                {
                    return Protect(connectionString);
                }
            }

            return connectionString;
        }

        /// <summary>
        /// appsettings.json dosyasındaki düz metin PostgreSQL parolasını kontrol eder
        /// ve "ENC:" ile şifrelenmemişse otomatik olarak şifreleyip dosyaya geri yazar.
        /// </summary>
        public static void MigrateAppSettingsIfNecessary(string appSettingsPath)
        {
            if (!File.Exists(appSettingsPath)) return;

            try
            {
                string jsonString = File.ReadAllText(appSettingsPath);
                var jsonObject = JsonNode.Parse(jsonString);
                if (jsonObject == null) return;

                bool modified = false;

                // DatabaseSettings:ConnectionStrings:PostgreSQL
                var dbSection = jsonObject["DatabaseSettings"]?["ConnectionStrings"];
                string? pgConn = dbSection?["PostgreSQL"]?.GetValue<string>();

                if (!string.IsNullOrWhiteSpace(pgConn))
                {
                    try
                    {
                        var builder = new NpgsqlConnectionStringBuilder(pgConn);
                        if (!string.IsNullOrEmpty(builder.Password) && !IsEncrypted(builder.Password))
                        {
                            builder.Password = Protect(builder.Password);
                            dbSection!["PostgreSQL"] = builder.ConnectionString;
                            modified = true;
                        }
                    }
                    catch { }
                }

                // Root ConnectionStrings:PostgreSQL kontrolü
                var rootConn = jsonObject["ConnectionStrings"]?["PostgreSQL"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(rootConn))
                {
                    try
                    {
                        var builder = new NpgsqlConnectionStringBuilder(rootConn);
                        if (!string.IsNullOrEmpty(builder.Password) && !IsEncrypted(builder.Password))
                        {
                            builder.Password = Protect(builder.Password);
                            jsonObject["ConnectionStrings"]!["PostgreSQL"] = builder.ConnectionString;
                            modified = true;
                        }
                    }
                    catch { }
                }

                if (modified)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    File.WriteAllText(appSettingsPath, jsonObject.ToJsonString(options));
                }
            }
            catch
            {
                // Migration hatası sessizce yutulur, uygulamanın çökmesi engellenir
            }
        }
    }
}
