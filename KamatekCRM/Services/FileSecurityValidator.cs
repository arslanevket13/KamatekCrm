using System;
using System.IO;
using System.Linq;

namespace KamatekCrm.Services
{
    /// <summary>
    /// Dosya yükleme güvenliği: Magic Bytes (Dosya İmzası), Maksimum Boyut ve Path Traversal doğrulaması.
    /// </summary>
    public static class FileSecurityValidator
    {
        public const long MaxImageSizeBytes = 10 * 1024 * 1024;      // 10 MB
        public const long MaxAttachmentSizeBytes = 25 * 1024 * 1024; // 25 MB

        // Yürütülebilir ve tehlikeli dosya başlıkları (MZ: PE/EXE/DLL, Script vb.)
        private static readonly byte[] ExecutableHeader = new byte[] { 0x4D, 0x5A }; // "MZ"

        // Desteklenen resim sihirli baytları (Magic Bytes)
        private static readonly byte[] JpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
        private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        private static readonly byte[] GifHeader = new byte[] { 0x47, 0x49, 0x46, 0x38 }; // "GIF8"
        private static readonly byte[] BmpHeader = new byte[] { 0x42, 0x4D };             // "BM"
        private static readonly byte[] RiffHeader = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // "RIFF" (WebP için)
        private static readonly byte[] WebpHeader = new byte[] { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

        // Desteklenen belge sihirli baytları
        private static readonly byte[] PdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        private static readonly byte[] ZipOrOfficeHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // "PK.." (ZIP, DOCX, XLSX)
        private static readonly byte[] OleOfficeHeader = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // Legacy DOC/XLS

        /// <summary>
        /// Resim dosyasının boyut ve magic bytes doğruluğunu kontrol eder.
        /// </summary>
        public static void ValidateImage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Doğrulanacak resim dosyası bulunamadı.", filePath);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxImageSizeBytes)
                throw new InvalidOperationException($"Resim dosyası maksimum 10 MB olabilir. Mevcut boyut: {fileInfo.Length / (1024 * 1024):N2} MB");

            using var stream = File.OpenRead(filePath);
            ValidateImageStream(stream, Path.GetExtension(filePath));
        }

        /// <summary>
        /// Akış (Stream) üzerinden resim doğrular.
        /// </summary>
        public static void ValidateImageStream(Stream stream, string extension)
        {
            if (stream == null || !stream.CanRead)
                throw new ArgumentException("Okunabilir bir veri akışı gereklidir.", nameof(stream));

            long streamLength = 0;
            try
            {
                streamLength = stream.Length;
                if (streamLength > MaxImageSizeBytes)
                    throw new InvalidOperationException($"Resim verisi maksimum 10 MB olabilir.");
            }
            catch (NotSupportedException)
            {
                // Canlı stream'ler length desteklemeyebilir
            }

            byte[] header = ReadHeaderBytes(stream, 16);

            if (IsExecutable(header))
                throw new InvalidOperationException("Güvenlik Uyarısı: Çalıştırılabilir dosya türleri yüklenemez!");

            string ext = extension?.ToLowerInvariant().Trim() ?? string.Empty;

            bool isValid = ext switch
            {
                ".jpg" or ".jpeg" => MatchesHeader(header, JpegHeader),
                ".png" => MatchesHeader(header, PngHeader),
                ".gif" => MatchesHeader(header, GifHeader),
                ".bmp" => MatchesHeader(header, BmpHeader),
                ".webp" => MatchesHeader(header, RiffHeader) && MatchesHeaderOffset(header, WebpHeader, 8),
                _ => MatchesHeader(header, JpegHeader) ||
                     MatchesHeader(header, PngHeader) ||
                     MatchesHeader(header, GifHeader) ||
                     MatchesHeader(header, BmpHeader) ||
                     (MatchesHeader(header, RiffHeader) && MatchesHeaderOffset(header, WebpHeader, 8))
            };

            if (!isValid)
                throw new InvalidOperationException("Geçersiz veya bozuk resim formatı. Dosya imzası beklenen resim türüyle eşleşmiyor.");
        }

        /// <summary>
        /// Arşiv dosya ekinin boyut ve imza doğruluğunu kontrol eder.
        /// </summary>
        public static void ValidateAttachment(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Doğrulanacak ek dosyası bulunamadı.", filePath);

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxAttachmentSizeBytes)
                throw new InvalidOperationException($"Dosya boyutu maksimum 25 MB olabilir. Mevcut boyut: {fileInfo.Length / (1024 * 1024):N2} MB");

            using var stream = File.OpenRead(filePath);
            ValidateAttachmentStream(stream, Path.GetExtension(filePath));
        }

        /// <summary>
        /// Akış üzerinden ek dosya doğrular.
        /// </summary>
        public static void ValidateAttachmentStream(Stream stream, string extension)
        {
            if (stream == null || !stream.CanRead)
                throw new ArgumentException("Okunabilir bir veri akışı gereklidir.", nameof(stream));

            long streamLength = 0;
            try
            {
                streamLength = stream.Length;
                if (streamLength > MaxAttachmentSizeBytes)
                    throw new InvalidOperationException("Ek dosya verisi maksimum 25 MB olabilir.");
            }
            catch (NotSupportedException)
            {
            }

            byte[] header = ReadHeaderBytes(stream, 16);

            if (IsExecutable(header))
                throw new InvalidOperationException("Güvenlik Uyarısı: Çalıştırılabilir dosya türleri sisteme yüklenemez!");

            string ext = extension?.ToLowerInvariant().Trim() ?? string.Empty;

            bool isValid = ext switch
            {
                ".pdf" => MatchesHeader(header, PdfHeader),
                ".jpg" or ".jpeg" => MatchesHeader(header, JpegHeader),
                ".png" => MatchesHeader(header, PngHeader),
                ".gif" => MatchesHeader(header, GifHeader),
                ".bmp" => MatchesHeader(header, BmpHeader),
                ".webp" => MatchesHeader(header, RiffHeader) && MatchesHeaderOffset(header, WebpHeader, 8),
                ".docx" or ".xlsx" or ".zip" => MatchesHeader(header, ZipOrOfficeHeader),
                ".doc" or ".xls" => MatchesHeader(header, OleOfficeHeader) || MatchesHeader(header, ZipOrOfficeHeader),
                ".txt" or ".csv" => !header.Take(Math.Min(header.Length, 16)).Contains((byte)0x00), // Düz metin null byte içermez
                _ => true // Diğer belgeler için en azından executable header olmaması garanti altındadır
            };

            if (!isValid)
                throw new InvalidOperationException($"Geçersiz dosya içeriği. Dosya içeriği belirtilen '{ext}' uzantısı ile uyuşmuyor.");
        }

        /// <summary>
        /// Path traversal risklerine karşı güvenli rastgele dosya adı üretir.
        /// </summary>
        public static string GenerateSecureFileName(string originalFileName)
        {
            var cleanFileName = Path.GetFileName(originalFileName);
            var extension = Path.GetExtension(cleanFileName).ToLowerInvariant();
            
            // Tehlikeli uzantıları engelle
            string[] dangerousExtensions = { ".exe", ".dll", ".bat", ".cmd", ".vbs", ".ps1", ".sh", ".com", ".scr" };
            if (dangerousExtensions.Contains(extension))
                throw new InvalidOperationException($"'{extension}' uzantılı dosyaların yüklenmesi yasaktır.");

            return $"{Guid.NewGuid():N}{extension}";
        }

        private static bool IsExecutable(byte[] header)
        {
            return MatchesHeader(header, ExecutableHeader);
        }

        private static byte[] ReadHeaderBytes(Stream stream, int count)
        {
            byte[] header = new byte[count];
            long originalPosition = 0;
            bool canSeek = stream.CanSeek;

            if (canSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            int read = stream.Read(header, 0, count);

            if (canSeek)
            {
                stream.Position = originalPosition;
            }

            if (read < count)
            {
                Array.Resize(ref header, read);
            }

            return header;
        }

        private static bool MatchesHeader(byte[] buffer, byte[] expected)
        {
            if (buffer.Length < expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
            {
                if (buffer[i] != expected[i]) return false;
            }
            return true;
        }

        private static bool MatchesHeaderOffset(byte[] buffer, byte[] expected, int offset)
        {
            if (buffer.Length < offset + expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
            {
                if (buffer[offset + i] != expected[i]) return false;
            }
            return true;
        }
    }
}
