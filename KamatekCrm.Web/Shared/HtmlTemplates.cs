using Microsoft.AspNetCore.Antiforgery;
using KamatekCrm.Web.Services;

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
                <a href="/dashboard" class="sidebar-link">
                    <i class="bi bi-speedometer2"></i>
                    <span>Dashboard</span>
                </a>
                <a href="/technician" class="sidebar-link">
                    <i class="bi bi-wrench"></i>
                    <span>Teknisyen</span>
                </a>
                <a href="/jobs" class="sidebar-link">
                    <i class="bi bi-list-task"></i>
                    <span>İş Emirleri</span>
                </a>
                <a href="/customers" class="sidebar-link">
                    <i class="bi bi-people"></i>
                    <span>Müşteriler</span>
                </a>
                <a href="/products" class="sidebar-link">
                    <i class="bi bi-box-seam"></i>
                    <span>Ürünler</span>
                </a>
                <a href="/sales" class="sidebar-link">
                    <i class="bi bi-cart"></i>
                    <span>Satış</span>
                </a>
                <a href="/sales/history" class="sidebar-link">
                    <i class="bi bi-clock-history"></i>
                    <span>Satış Geçmişi</span>
                </a>
                <a href="/technician/schedule" class="sidebar-link">
                    <i class="bi bi-calendar"></i>
                    <span>Program</span>
                </a>
                <a href="/technician/profile" class="sidebar-link">
                    <i class="bi bi-person"></i>
                    <span>Profil</span>
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

    // ═══════════════════════════════════════════════════════════════
    //  MÜŞTERİLER
    // ═══════════════════════════════════════════════════════════════

    public static string CustomersPage(List<CustomerListItem> customers, int total, int page, string userName, string? token, string? search = null)
    {
        var rows = string.Join("", customers.Select(c => $"""
            <tr>
                <td>{c.FullName}</td>
                <td>{c.PhoneNumber ?? "-"}</td>
                <td>{c.Email ?? "-"}</td>
                <td>{(c.IsVip ? "<span class=\"badge bg-warning\">VIP</span>" : "<span class=\"badge bg-secondary\">Normal</span>")}</td>
                <td>
                    <a href="/customers/{c.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-eye"></i></a>
                </td>
            </tr>
            """));

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center">
                <h2><i class="bi bi-people me-2"></i>Müşteriler</h2>
                <a href="/customers/new" class="btn btn-primary"><i class="bi bi-plus-lg"></i>Yeni Müşteri</a>
            </div>
        </div>
        <div class="content-card">
            <form class="mb-3" hx-get="/customers" hx-target="tbody">
                <input type="text" name="search" class="form-control" placeholder="Ara..." value="{search ?? ""}">
            </form>
            <table class="table table-hover">
                <thead>
                    <tr><th>Firma</th><th>Yetkili</th><th>Telefon</th><th>Durum</th><th>İşlemler</th></tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
        </div>
        """;
        return Layout("Müşteriler", content, userName, token);
    }

    public static string CustomerForm(CustomerListItem? customer, string? token)
    {
        var title = customer != null ? "Müşteri Düzenle" : "Yeni Müşteri";
        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-person-plus me-2"></i>{title}</h2>
        </div>
        <div class="content-card">
            <form hx-post="/customers" hx-swap="outerHTML">
                <input type="hidden" name="Id" value="{customer?.Id ?? 0}">
                <div class="mb-3">
                    <label class="form-label">Ad Soyad</label>
                    <input type="text" class="form-control" name="FullName" value="{customer?.FullName ?? ""}" required>
                </div>
                <div class="mb-3">
                    <label class="form-label">Telefon</label>
                    <input type="tel" class="form-control" name="PhoneNumber" value="{customer?.PhoneNumber ?? ""}">
                </div>
                <div class="mb-3">
                    <label class="form-label">E-posta</label>
                    <input type="email" class="form-control" name="Email" value="{customer?.Email ?? ""}">
                </div>
                <button type="submit" class="btn btn-primary">Kaydet</button>
            </form>
        </div>
        """;
        return Layout(title, content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ÜRÜNLER
    // ═══════════════════════════════════════════════════════════════

    public static string ProductsPage(string? token)
    {
        var content = """
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center">
                <h2><i class="bi bi-box-seam me-2"></i>Ürünler</h2>
                <a href="/products/new" class="btn btn-primary"><i class="bi bi-plus-lg"></i>Yeni Ürün</a>
            </div>
        </div>
        <div class="content-card">
            <p class="text-muted">Ürün listesi yükleniyor...</p>
        </div>
        """;
        return Layout("Ürünler", content, antiforgeryToken: token);
    }

    public static string ProductForm(string? token)
    {
        var content = """
        <div class="page-header">
            <h2><i class="bi bi-box-seam me-2"></i>Yeni Ürün</h2>
        </div>
        <div class="content-card">
            <form>
                <div class="mb-3">
                    <label class="form-label">Ürün Adı</label>
                    <input type="text" class="form-control" name="Name" required>
                </div>
                <div class="mb-3">
                    <label class="form-label">SKU</label>
                    <input type="text" class="form-control" name="Sku">
                </div>
                <div class="mb-3">
                    <label class="form-label">Fiyat</label>
                    <input type="number" class="form-control" name="Price">
                </div>
                <button type="submit" class="btn btn-primary">Kaydet</button>
            </form>
        </div>
        """;
        return Layout("Ürün Form", content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  İŞ EMİRLERİ
    // ═══════════════════════════════════════════════════════════════

    public static string JobsPage(List<JobListItem> jobs, int total, int page, string userName, string? token, string? status = null, string? search = null)
    {
        var rows = string.Join("", jobs.Select(j => $"""
            <tr>
                <td>{j.Title}</td>
                <td>{j.CustomerName ?? "-"}</td>
                <td><span class="badge bg-secondary">{j.Status}</span></td>
                <td>{j.ScheduledDate?.ToString("dd.MM.yyyy") ?? "-"}</td>
                <td>
                    <a href="/jobs/{j.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-eye"></i></a>
                </td>
            </tr>
            """));

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center">
                <h2><i class="bi bi-list-task me-2"></i>İş Emirleri</h2>
                <a href="/jobs/new" class="btn btn-primary"><i class="bi bi-plus-lg"></i>Yeni İş</a>
            </div>
        </div>
        <div class="content-card">
            <table class="table table-hover">
                <thead>
                    <tr><th>Başlık</th><th>Müşteri</th><th>Durum</th><th>Tarih</th><th>İşlemler</th></tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
        </div>
        """;
        return Layout("İş Emirleri", content, userName, token);
    }

    public static string JobForm(JobListItem? job, string? token)
    {
        var title = job != null ? "İş Düzenle" : "Yeni İş Emri";
        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-plus-circle me-2"></i>{title}</h2>
        </div>
        <div class="content-card">
            <form>
                <input type="hidden" name="Id" value="{job?.Id ?? 0}">
                <div class="mb-3">
                    <label class="form-label">Başlık</label>
                    <input type="text" class="form-control" name="Title" value="{job?.Title ?? ""}" required>
                </div>
                <div class="mb-3">
                    <label class="form-label">Açıklama</label>
                    <textarea class="form-control" name="Description" rows="3">{job?.Description ?? ""}</textarea>
                </div>
                <button type="submit" class="btn btn-primary">Kaydet</button>
            </form>
        </div>
        """;
        return Layout(title, content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  ÜRÜNLER
    // ═══════════════════════════════════════════════════════════════

    public static string ProductsPage(List<ProductListItem> products, int total, int page, string userName, string? token, string? search = null)
    {
        var rows = string.Join("", products.Select(p => $"""
            <tr>
                <td>{p.ProductName}</td>
                <td>{p.SKU}</td>
                <td>{p.SalePrice:C2}</td>
                <td>{p.TotalStockQuantity}</td>
                <td>
                    <a href="/products/{p.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-eye"></i></a>
                </td>
            </tr>
            """));

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center">
                <h2><i class="bi bi-box-seam me-2"></i>Ürünler</h2>
                <a href="/products/new" class="btn btn-primary"><i class="bi bi-plus-lg"></i>Yeni Ürün</a>
            </div>
        </div>
        <div class="content-card">
            <table class="table table-hover">
                <thead>
                    <tr><th>Ürün</th><th>SKU</th><th>Fiyat</th><th>Stok</th><th>İşlemler</th></tr>
                </thead>
                <tbody>{rows}</tbody>
            </table>
        </div>
        """;
        return Layout("Ürünler", content, userName, token);
    }

    public static string ProductForm(ProductListItem? product, string? token)
    {
        var title = product != null ? "Ürün Düzenle" : "Yeni Ürün";
        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-box-seam me-2"></i>{title}</h2>
        </div>
        <div class="content-card">
            <form>
                <input type="hidden" name="Id" value="{product?.Id ?? 0}">
                <div class="mb-3">
                    <label class="form-label">Ürün Adı</label>
                    <input type="text" class="form-control" name="ProductName" value="{product?.ProductName ?? ""}" required>
                </div>
                <div class="mb-3">
                    <label class="form-label">SKU</label>
                    <input type="text" class="form-control" name="SKU" value="{product?.SKU ?? ""}">
                </div>
                <div class="mb-3">
                    <label class="form-label">Satış Fiyatı</label>
                    <input type="number" class="form-control" name="SalePrice" value="{product?.SalePrice}">
                </div>
                <button type="submit" class="btn btn-primary">Kaydet</button>
            </form>
        </div>
        """;
        return Layout(title, content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SATIŞ
    // ═══════════════════════════════════════════════════════════════

    public static string SalesPage(List<ProductListItem> products, string userName, string? token)
    {
        var productOptions = string.Join("", products.Select(p => $"""
            <option value="{p.Id}" data-price="{p.SalePrice}">{p.ProductName} - {p.SalePrice:C2}</option>
            """));

        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-cart me-2"></i>Satış</h2>
        </div>
        <div class="content-card">
            <form hx-post="/sales" hx-swap="outerHTML">
                <div class="mb-3">
                    <label class="form-label">Ürün</label>
                    <select class="form-select" name="ProductId">{productOptions}</select>
                </div>
                <div class="mb-3">
                    <label class="form-label">Adet</label>
                    <input type="number" class="form-control" name="Quantity" value="1" min="1">
                </div>
                <button type="submit" class="btn btn-primary">Satış Yap</button>
            </form>
        </div>
        """;
        return Layout("Satış", content, userName, token);
    }

    public static string SalesHistoryPage(List<object> sales, int total, int page, string userName, string? token)
    {
        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-clock-history me-2"></i>Satış Geçmişi</h2>
        </div>
        <div class="content-card">
            <p class="text-muted">Satış geçmişi yükleniyor...</p>
        </div>
        """;
        return Layout("Satış Geçmişi", content, userName, token);
    }

    public static string JobDetail(string? token)
    {
        var content = """
        <div class="page-header">
            <h2><i class="bi bi-info-circle me-2"></i>İş Detayı</h2>
        </div>
        <div class="content-card">
            <p class="text-muted">İş detayları yükleniyor...</p>
        </div>
        """;
        return Layout("İş Detay", content, antiforgeryToken: token);
    }

    public static string JobForm(string? token)
    {
        var content = """
        <div class="page-header">
            <h2><i class="bi bi-plus-circle me-2"></i>Yeni İş Emri</h2>
        </div>
        <div class="content-card">
            <form>
                <div class="mb-3">
                    <label class="form-label">Başlık</label>
                    <input type="text" class="form-control" name="Title" required>
                </div>
                <div class="mb-3">
                    <label class="form-label">Müşteri</label>
                    <select class="form-select" name="CustomerId"></select>
                </div>
                <button type="submit" class="btn btn-primary">Kaydet</button>
            </form>
        </div>
        """;
        return Layout("İş Form", content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  SATIŞ
    // ═══════════════════════════════════════════════════════════════

    public static string SalesPage(string? token)
    {
        var content = """
        <div class="page-header">
            <h2><i class="bi bi-cart me-2"></i>Satış</h2>
        </div>
        <div class="content-card">
            <p class="text-muted">Satış sayfası yükleniyor...</p>
        </div>
        """;
        return Layout("Satış", content, antiforgeryToken: token);
    }

    public static string SalesHistoryPage(string? token)
    {
        var content = """
        <div class="page-header">
            <h2><i class="bi bi-clock-history me-2"></i>Satış Geçmişi</h2>
        </div>
        <div class="content-card">
            <p class="text-muted">Satış geçmişi yükleniyor...</p>
        </div>
        """;
        return Layout("Satış Geçmişi", content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  TEKNİSYEN PANELİ
    // ═══════════════════════════════════════════════════════════════

    public static string TechnicianDashboard(string userName, string role, List<JobListItem> todayJobs, List<JobListItem> pendingJobs, DashboardStats stats, string? token)
    {
        var todayJobsHtml = string.Join("", todayJobs.Select(j => $"""
            <div class="card mb-2">
                <div class="card-body">
                    <h6>{j.Title}</h6>
                    <p class="mb-0 text-muted">{j.CustomerName ?? "-"}</p>
                </div>
            </div>
            """));

        var pendingJobsHtml = string.Join("", pendingJobs.Select(j => $"""
            <div class="card mb-2">
                <div class="card-body">
                    <h6>{j.Title}</h6>
                    <p class="mb-0 text-muted">{j.CustomerName ?? "-"}</p>
                </div>
            </div>
            """));

        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-wrench me-2"></i>Teknisyen Panel</h2>
        </div>
        <div class="row">
            <div class="col-md-3">
                <div class="card bg-primary text-white mb-3">
                    <div class="card-body">
                        <h5>{stats.ActiveJobs}</h5>
                        <p class="mb-0">Aktif İşler</p>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card bg-success text-white mb-3">
                    <div class="card-body">
                        <h5>{stats.CompletedToday}</h5>
                        <p class="mb-0">Bugün Tamamlanan</p>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card bg-warning text-dark mb-3">
                    <div class="card-body">
                        <h5>{stats.PendingJobs}</h5>
                        <p class="mb-0">Bekleyen</p>
                    </div>
                </div>
            </div>
            <div class="col-md-3">
                <div class="card bg-info text-white mb-3">
                    <div class="card-body">
                        <h5>{stats.FieldVisits}</h5>
                        <p class="mb-0">Saha Ziyareti</p>
                    </div>
                </div>
            </div>
        </div>
        <div class="row">
            <div class="col-md-6">
                <div class="content-card">
                    <h5>Bugünkü İşler</h5>
                    {todayJobsHtml}
                </div>
            </div>
            <div class="col-md-6">
                <div class="content-card">
                    <h5>Bekleyen İşler</h5>
                    {pendingJobsHtml}
                </div>
            </div>
        </div>
        """;
        return Layout("Teknisyen Panel", content, userName, token);
    }

    public static string SchedulePage(List<JobListItem> jobs, DateTime date, string userName, string? token)
    {
        var jobsHtml = string.Join("", jobs.Select(j => $"""
            <div class="card mb-2">
                <div class="card-body">
                    <h6>{j.Title}</h6>
                    <p class="mb-0 text-muted">{j.CustomerName ?? "-"} - {j.Status}</p>
                </div>
            </div>
            """));

        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-calendar me-2"></i>Program - {date:dd.MM.yyyy}</h2>
        </div>
        <div class="content-card">
            {jobsHtml}
        </div>
        """;
        return Layout("Program", content, userName, token);
    }

    public static string TechnicianProfile(string userName, string role, string username, string? token)
    {
        var content = $"""
        <div class="page-header">
            <h2><i class="bi bi-person me-2"></i>Profil</h2>
        </div>
        <div class="content-card">
            <p><strong>Kullanıcı:</strong> {userName}</p>
            <p><strong>Rol:</strong> {role}</p>
            <p><strong>Kullanıcı Adı:</strong> {username}</p>
        </div>
        """;
        return Layout("Profil", content, userName, token);
    }
}
