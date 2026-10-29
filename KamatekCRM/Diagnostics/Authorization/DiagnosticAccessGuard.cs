using System;
using System.Linq;
using KamatekCrm.ApplicationCore.Interfaces;
using KamatekCrm.ApplicationCore.Security;
using KamatekCrm.Configuration;

namespace KamatekCrm.Diagnostics.Authorization
{
    /// <summary>
    /// Geliştirici Merkezi'nin tek yetki kapısı.
    /// <para>
    /// Kural seti (hepsi aynı anda sağlanmalı):
    /// <list type="number">
    /// <item>Tanılama altyapısı etkin (<c>Diagnostics:Enabled</c>).</item>
    /// <item>Kullanıcı oturum açmış.</item>
    /// <item><see cref="ApplicationPermission.AccessDiagnostics"/> izni var.</item>
    /// <item>Kullanıcı adı <c>Diagnostics:DeveloperAccounts</c> listesinde (büyük/küçük harf duyarsız).</item>
    /// </list>
    /// Liste boşsa erişim <b>kapalıdır</b> (fail-closed): yanlışlıkla her Admin'in panele
    /// girmesini engeller.
    /// </para>
    /// Menü görünürlüğü tek başına yeterli değildir; veri servisleri bu sınıfı çağırır.
    /// </summary>
    public sealed class DiagnosticAccessGuard
    {
        private readonly ICurrentUserContext _currentUser;
        private readonly DiagnosticOptions _options;

        public DiagnosticAccessGuard(ICurrentUserContext currentUser, DiagnosticOptions options)
        {
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public bool IsEnabled => _options.Enabled;

        public bool IsAuthenticated => _currentUser.IsAuthenticated;

        public bool IsListedDeveloper
        {
            get
            {
                var accounts = _options.DeveloperAccounts;
                if (accounts is null || accounts.Count == 0)
                {
                    return false;
                }

                var username = _currentUser.Username;
                return !string.IsNullOrWhiteSpace(username) &&
                       accounts.Any(account => string.Equals(account, username, StringComparison.OrdinalIgnoreCase));
            }
        }

        public bool HasDiagnosticPermission =>
            _currentUser.IsAuthenticated &&
            _currentUser.HasPermission(ApplicationPermission.AccessDiagnostics);

        public bool IsAuthorized => IsEnabled && HasDiagnosticPermission && IsListedDeveloper;

        /// <summary>Kullanıcıya gösterilebilecek yetkisizlik gerekçesi.</summary>
        public string DenialReason
        {
            get
            {
                if (!IsEnabled)
                {
                    return "Tanılama altyapısı bu kurulumda kapalı (appsettings → Diagnostics:Enabled=false).";
                }

                if (!IsAuthenticated)
                {
                    return "Geliştirici Merkezi için oturum açılmalıdır.";
                }

                if (_options.DeveloperAccounts is null || _options.DeveloperAccounts.Count == 0)
                {
                    return "Geliştirici hesabı tanımlı değil (appsettings → Diagnostics:DeveloperAccounts).";
                }

                if (!HasDiagnosticPermission || !IsListedDeveloper)
                {
                    return "Bu hesabın Geliştirici Merkezi'ne erişim yetkisi yok.";
                }

                return string.Empty;
            }
        }

        /// <summary>Yetkisiz erişimde <see cref="UnauthorizedAccessException"/> fırlatır.</summary>
        public void EnsureAuthorized()
        {
            if (!IsAuthorized)
            {
                throw new UnauthorizedAccessException(DenialReason);
            }
        }
    }
}
