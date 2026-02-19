using Microsoft.AspNetCore.Antiforgery;

namespace KamatekCrm.Web.Shared;

/// <summary>
/// Tüm HTML sayfalarını C# raw string interpolation ile üreten statik sınıf.
/// Blazor yerine saf HTML + HTMX + Bootstrap 5 kullanır.
/// </summary>
public static class HtmlTemplates
{
    // ══════════════════════════════════════════════
    //  ANA LAYOUT
    // ══════════════════════════════════════════════

    /// <summary>
    /// Tam HTML5 sayfa şablonu. Bootstrap 5 ve HTMX CDN'den yüklenir.
    /// </summary>
    public static string Layout(string title, string bodyContent, string? userName = null, string? antiforgeryToken = null)
    {
        var sidebarHtml = userName is not null ? Sidebar(userName) : "";
        var mainClass = userName is not null ? "main-with-sidebar" : "main-full";
        var tokenInput = antiforgeryToken is not null
            ? $"""<input type="hidden" id="xsrf-token" name="__RequestVerificationToken" value="{antiforgeryToken}" />"""
            : "";

        return $$"""
        <!DOCTYPE html>
        <html lang="tr" data-bs-theme="dark">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <meta name="description" content="KamatekCRM - Saha Teknisyen Paneli" />
            <title>{{title}} — KamatekCRM</title>

            <!-- Favicon (Base64 Data URI — prevents 404) -->
            <link rel="icon" type="image/svg+xml" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect width='32' height='32' rx='8' fill='%233b82f6'/%3E%3Ctext x='16' y='23' text-anchor='middle' fill='white' font-size='20' font-family='sans-serif' font-weight='bold'%3EK%3C/text%3E%3C/svg%3E" />

            <!-- Bootstrap 5 CSS -->
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css"
                  rel="stylesheet" />

            <!-- Bootstrap Icons -->
            <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css"
                  rel="stylesheet" />

            <!-- Google Fonts: Inter -->
            <link rel="preconnect" href="https://fonts.googleapis.com" />
            <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
            <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap"
                  rel="stylesheet" />

            <!-- Custom CSS -->
            <link href="/css/site.css" rel="stylesheet" />
        </head>
        <body>
            {{tokenInput}}

            <div class="app-wrapper">
                {{sidebarHtml}}
                <main class="{{mainClass}}">
                    {{bodyContent}}
                </main>
            </div>

            <!-- Toast Container (fixed top-right, above everything) -->
            <div id="toast-container" class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 9999;"></div>

            <!-- Bootstrap 5 JS -->
            <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>

            <!-- HTMX -->
            <script src="https://cdn.jsdelivr.net/npm/htmx.org@2.0.4/dist/htmx.min.js"></script>

            <!-- HTMX Antiforgery + Toast Config -->
            <script src="/js/htmx-config.js"></script>
        </body>
        </html>
        """;
    }

    // ══════════════════════════════════════════════
    //  SIDEBAR
    // ══════════════════════════════════════════════

    private static string Sidebar(string userName)
    {
        return $$"""
        <aside class="sidebar">
            <div class="sidebar-brand">
                <i class="bi bi-shield-lock-fill"></i>
                <span>KamatekCRM</span>
            </div>

            <nav class="sidebar-nav">
                <a href="/dashboard" class="sidebar-link active">
                    <i class="bi bi-speedometer2"></i>
                    <span>Dashboard</span>
                </a>
                <a href="#" class="sidebar-link disabled">
                    <i class="bi bi-list-task"></i>
                    <span>Görevler</span>
                </a>
                <a href="#" class="sidebar-link disabled">
                    <i class="bi bi-geo-alt"></i>
                    <span>Sahalar</span>
                </a>
                <a href="#" class="sidebar-link disabled">
                    <i class="bi bi-chat-dots"></i>
                    <span>Mesajlar</span>
                </a>
            </nav>

            <div class="sidebar-footer">
                <div class="sidebar-user">
                    <i class="bi bi-person-circle"></i>
                    <span>{{userName}}</span>
                </div>
                <form hx-post="/logout" hx-swap="none">
                    <button type="submit" class="btn btn-outline-danger btn-sm w-100 mt-2">
                        <i class="bi bi-box-arrow-left me-1"></i>Çıkış Yap
                    </button>
                </form>
            </div>
        </aside>
        """;
    }

    // ══════════════════════════════════════════════
    //  LOGIN SAYFASI
    // ══════════════════════════════════════════════

    public static string LoginPage(string? errorMessage = null, string? antiforgeryToken = null)
    {
        var errorHtml = errorMessage is not null
            ? $"""<div class="alert alert-danger d-flex align-items-center" role="alert"><i class="bi bi-exclamation-triangle-fill me-2"></i>{errorMessage}</div>"""
            : "";

        var tokenField = antiforgeryToken is not null
            ? $"""<input type="hidden" name="__RequestVerificationToken" value="{antiforgeryToken}" />"""
            : "";

        var formContent = $$"""
        <div class="login-wrapper">
            <div class="login-card">
                <div class="login-header">
                    <div class="login-logo">
                        <i class="bi bi-shield-lock-fill"></i>
                    </div>
                    <h1>KamatekCRM</h1>
                    <p class="text-muted">Saha Teknisyen Paneli</p>
                </div>

                <div id="error-container">
                    {{errorHtml}}
                </div>

                <form hx-post="/login"
                      hx-target="#error-container"
                      hx-swap="innerHTML"
                      hx-indicator="#login-spinner"
                      autocomplete="on">
                    {{tokenField}}

                    <div class="mb-3">
                        <label for="username" class="form-label">
                            <i class="bi bi-person me-1"></i>Kullanıcı Adı
                        </label>
                        <input type="text"
                               class="form-control form-control-lg"
                               id="username"
                               name="username"
                               placeholder="Kullanıcı adınızı girin"
                               required
                               autofocus />
                    </div>

                    <div class="mb-4">
                        <label for="password" class="form-label">
                            <i class="bi bi-lock me-1"></i>Şifre
                        </label>
                        <input type="password"
                               class="form-control form-control-lg"
                               id="password"
                               name="password"
                               placeholder="Şifrenizi girin"
                               required />
                    </div>

                    <button type="submit" class="btn btn-primary btn-lg w-100 login-btn">
                        <span id="login-spinner" class="htmx-indicator">
                            <span class="spinner-border spinner-border-sm me-2" role="status"></span>
                        </span>
                        <i class="bi bi-box-arrow-in-right me-2"></i>Giriş Yap
                    </button>
                </form>

                <div class="login-footer">
                    <small class="text-muted">Kamatek Güvenlik Sistemleri © 2026</small>
                </div>
            </div>
        </div>
        """;

        return Layout("Giriş", formContent, antiforgeryToken: antiforgeryToken);
    }

    // ══════════════════════════════════════════════
    //  DASHBOARD SAYFASI
    // ══════════════════════════════════════════════

    public static string DashboardPage(string userName, string role, string? antiforgeryToken = null)
    {
        var roleDisplay = role switch
        {
            "Admin" => """<span class="badge bg-danger"><i class="bi bi-star-fill me-1"></i>Yönetici</span>""",
            "Technician" => """<span class="badge bg-info"><i class="bi bi-wrench me-1"></i>Teknisyen</span>""",
            _ => $"""<span class="badge bg-secondary">{role}</span>"""
        };

        var now = DateTime.Now;
        var greeting = now.Hour switch
        {
            < 12 => "Günaydın",
            < 18 => "İyi günler",
            _ => "İyi akşamlar"
        };

        var dashboardContent = $$"""
        <div class="dashboard-container">
            <!-- Welcome Header -->
            <div class="welcome-section">
                <h2 class="welcome-title">
                    {{greeting}}, <strong>{{userName}}</strong> 👋
                </h2>
                <div class="d-flex align-items-center gap-2">
                    {{roleDisplay}}
                    <span class="text-muted">|</span>
                    <span class="text-muted"><i class="bi bi-calendar3 me-1"></i>{{now:dd MMMM yyyy, dddd}}</span>
                </div>
            </div>

            <!-- KPI Cards -->
            <div class="row g-4 mt-2">
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-blue">
                        <div class="kpi-icon"><i class="bi bi-list-task"></i></div>
                        <div class="kpi-body">
                            <span class="kpi-value">—</span>
                            <span class="kpi-label">Aktif Görev</span>
                        </div>
                    </div>
                </div>
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-green">
                        <div class="kpi-icon"><i class="bi bi-check-circle"></i></div>
                        <div class="kpi-body">
                            <span class="kpi-value">—</span>
                            <span class="kpi-label">Tamamlanan</span>
                        </div>
                    </div>
                </div>
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-orange">
                        <div class="kpi-icon"><i class="bi bi-clock-history"></i></div>
                        <div class="kpi-body">
                            <span class="kpi-value">—</span>
                            <span class="kpi-label">Bekleyen</span>
                        </div>
                    </div>
                </div>
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-purple">
                        <div class="kpi-icon"><i class="bi bi-geo-alt"></i></div>
                        <div class="kpi-body">
                            <span class="kpi-value">—</span>
                            <span class="kpi-label">Saha Ziyareti</span>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Quick Actions -->
            <div class="row g-4 mt-2">
                <div class="col-12">
                    <div class="content-card">
                        <h5 class="card-section-title">
                            <i class="bi bi-lightning-fill text-warning me-2"></i>Hızlı İşlemler
                        </h5>
                        <div class="d-flex flex-wrap gap-2 mt-3">
                            <button class="btn btn-outline-primary" disabled>
                                <i class="bi bi-plus-circle me-1"></i>Yeni Görev
                            </button>
                            <button class="btn btn-outline-success" disabled>
                                <i class="bi bi-camera me-1"></i>Fotoğraf Yükle
                            </button>
                            <button class="btn btn-outline-info" disabled>
                                <i class="bi bi-file-text me-1"></i>Rapor Oluştur
                            </button>
                        </div>
                        <p class="text-muted mt-3 mb-0">
                            <i class="bi bi-info-circle me-1"></i>Bu özellikler yakında aktif edilecektir.
                        </p>
                    </div>
                </div>
            </div>
        </div>
        """;

        return Layout("Dashboard", dashboardContent, userName, antiforgeryToken);
    }

    // ══════════════════════════════════════════════
    //  HATA SAYFASI
    // ══════════════════════════════════════════════

    public static string ErrorPage(string title, string message)
    {
        var errorContent = $$"""
        <div class="login-wrapper">
            <div class="login-card text-center">
                <div class="mb-4">
                    <i class="bi bi-exclamation-triangle-fill text-danger" style="font-size: 3rem;"></i>
                </div>
                <h2>{{title}}</h2>
                <p class="text-muted">{{message}}</p>
                <a href="/login" class="btn btn-primary mt-3">
                    <i class="bi bi-arrow-left me-1"></i>Giriş Sayfasına Dön
                </a>
            </div>
        </div>
        """;

        return Layout("Hata", errorContent);
    }
}
