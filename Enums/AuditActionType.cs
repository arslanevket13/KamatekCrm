namespace KamatekCrm.Enums
{
    /// <summary>
    /// Audit log işlem tipleri
    /// </summary>
    public enum AuditActionType
    {
        /// <summary>
        /// Kullanıcı girişi
        /// </summary>
        Login,

        /// <summary>
        /// Kullanıcı çıkışı
        /// </summary>
        Logout,

        /// <summary>
        /// Yeni kayıt oluşturma
        /// </summary>
        Create,

        /// <summary>
        /// Kayıt güncelleme
        /// </summary>
        Update,

        /// <summary>
        /// Kayıt silme
        /// </summary>
        Delete,

        /// <summary>
        /// Şifre değişikliği
        /// </summary>
        PasswordChange,

        /// <summary>
        /// Şifre sıfırlama
        /// </summary>
        PasswordReset,

        /// <summary>
        /// Görüntüleme
        /// </summary>
        View,

        /// <summary>
        /// Dışa aktarma
        /// </summary>
        Export,

        /// <summary>
        /// İçe aktarma
        /// </summary>
        Import
    }
}
