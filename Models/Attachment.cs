using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KamatekCrm.Enums;

namespace KamatekCrm.Models
{
    /// <summary>
    /// Dosya Eki Entity - Dijital Arşiv
    /// Her türlü kayda dosya/fotoğraf eklemek için kullanılır.
    /// </summary>
    public class Attachment
    {
        /// <summary>
        /// Ek ID (Primary Key)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Bağlı olduğu entity türü
        /// </summary>
        [Required]
        public AttachmentEntityType EntityType { get; set; }

        /// <summary>
        /// Bağlı olduğu entity ID
        /// </summary>
        [Required]
        public int EntityId { get; set; }

        /// <summary>
        /// Dosya yolu (KamatekArchive klasöründe)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Orijinal dosya adı
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Dosya boyutu (bytes)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// MIME türü (image/jpeg, application/pdf vb.)
        /// </summary>
        [MaxLength(100)]
        public string? ContentType { get; set; }

        /// <summary>
        /// Yükleme tarihi
        /// </summary>
        [Required]
        public DateTime UploadDate { get; set; } = DateTime.Now;

        /// <summary>
        /// Yükleyen kullanıcı
        /// </summary>
        [MaxLength(100)]
        public string? UploadedBy { get; set; }

        /// <summary>
        /// Açıklama/Not
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// Dosya uzantısı (computed)
        /// </summary>
        [NotMapped]
        public string FileExtension => System.IO.Path.GetExtension(FileName).ToLowerInvariant();

        /// <summary>
        /// Resim dosyası mı?
        /// </summary>
        [NotMapped]
        public bool IsImage => FileExtension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp";

        /// <summary>
        /// PDF dosyası mı?
        /// </summary>
        [NotMapped]
        public bool IsPdf => FileExtension == ".pdf";
    }
}
