using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KamatekCrm.Shared.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "Viewer";

        [Required]
        [MaxLength(50)]
        public string Ad { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Soyad { get; set; } = string.Empty;

        [NotMapped]
        public string AdSoyad => $"{Ad} {Soyad}".Trim();

        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastLoginDate { get; set; }

        #region RBAC - Granular Permissions
        public bool CanViewFinance { get; set; } = false;
        public bool CanViewAnalytics { get; set; } = false;
        public bool CanDeleteRecords { get; set; } = false;
        public bool CanApprovePurchase { get; set; } = false;
        public bool CanAccessSettings { get; set; } = false;
        #endregion
    }
}
