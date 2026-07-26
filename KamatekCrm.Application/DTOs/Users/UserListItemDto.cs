namespace KamatekCrm.ApplicationCore.DTOs.Users
{
    /// <summary>
    /// Kullanıcı listeleme (DataGrid / tablo) için hafifletilmiş DTO.
    /// </summary>
    public class UserListItemDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string Soyad { get; set; } = string.Empty;
        public string AdSoyad { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsTechnician { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public string? Phone { get; set; }
        public string? ServiceArea { get; set; }
    }
}
