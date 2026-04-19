using Microsoft.AspNetCore.Antiforgery;
using KamatekCrm.Web.Services;

namespace KamatekCrm.Web.Shared;

/// <summary>
/// Tüm HTML sayfalarını C# raw string interpolation ile üreten statik sınıf.
/// Blazor yerine saf HTML + HTMX + Bootstrap 5 kullanır. PWA + Mobile-First.
/// </summary>
public static class HtmlTemplates
{
    // ══════════════════════════════════════════════
    //  ANA LAYOUT — PWA + Mobile-First
    // ══════════════════════════════════════════════

    public static string Layout(string title, string bodyContent, string? userName = null, string? antiforgeryToken = null)
    {
        var sidebarHtml = userName is not null ? Sidebar(userName) : "";
        var mainClass = userName is not null ? "main-with-sidebar" : "main-full";
        var tokenInput = antiforgeryToken is not null
            ? $"""<input type="hidden" id="xsrf-token" name="__RequestVerificationToken" value="{antiforgeryToken}" />"""
            : "";

        var mobileHeader = userName is not null ? """
            <header class="mobile-header">
                <button class="hamburger-btn" onclick="toggleSidebar()"><i class="bi bi-list"></i></button>
                <div class="brand"><i class="bi bi-shield-lock-fill"></i><span>KamatekCRM</span></div>
                <a href="/technician/profile" style="color:var(--text-secondary);font-size:1.3rem;text-decoration:none;"><i class="bi bi-person-circle"></i></a>
            </header>
            <div class="sidebar-overlay" id="sidebarOverlay" onclick="toggleSidebar()"></div>
        """ : "";

        var bottomNav = userName is not null ? """
            <nav class="bottom-nav"><div class="bottom-nav-inner">
                <a href="/dashboard" class="bottom-nav-item"><i class="bi bi-speedometer2"></i><span>Panel</span></a>
                <a href="/jobs" class="bottom-nav-item"><i class="bi bi-list-task"></i><span>İşler</span></a>
                <a href="/technician/dashboard" class="bottom-nav-item"><i class="bi bi-wrench"></i><span>Teknisyen</span></a>
                <a href="/customers" class="bottom-nav-item"><i class="bi bi-people"></i><span>Müşteri</span></a>
                <a href="/technician/profile" class="bottom-nav-item"><i class="bi bi-person"></i><span>Profil</span></a>
            </div></nav>
        """ : "";

        return $$"""
        <!DOCTYPE html>
        <html lang="tr" data-bs-theme="dark">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
            <meta name="description" content="KamatekCRM - Saha Teknisyen Paneli" />
            <meta name="theme-color" content="#3B82F6" />
            <meta name="apple-mobile-web-app-capable" content="yes" />
            <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
            <title>{{title}} — KamatekCRM</title>
            <link rel="manifest" href="/manifest.json" />
            <link rel="icon" type="image/svg+xml" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Crect width='32' height='32' rx='8' fill='%233b82f6'/%3E%3Ctext x='16' y='23' text-anchor='middle' fill='white' font-size='20' font-family='sans-serif' font-weight='bold'%3EK%3C/text%3E%3C/svg%3E" />
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet" />
            <link href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" rel="stylesheet" />
            <link rel="preconnect" href="https://fonts.googleapis.com" />
            <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
            <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
            <link href="/css/site.css" rel="stylesheet" />
        </head>
        <body>
            {{tokenInput}}
            {{mobileHeader}}
            <div class="app-wrapper">
                {{sidebarHtml}}
                <main class="{{mainClass}}">
                    {{bodyContent}}
                </main>
            </div>
            {{bottomNav}}
            <div id="toast-container" class="toast-container position-fixed top-0 end-0 p-3" style="z-index: 9999;"></div>
            <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
            <script src="https://cdn.jsdelivr.net/npm/htmx.org@2.0.4/dist/htmx.min.js"></script>
            <script src="/js/htmx-config.js"></script>
            <script>
            function toggleSidebar(){var s=document.querySelector('.sidebar');var o=document.getElementById('sidebarOverlay');if(s)s.classList.toggle('open');if(o)o.classList.toggle('show');}
            (function(){var p=location.pathname;document.querySelectorAll('.sidebar-link,.bottom-nav-item').forEach(function(a){if(p===a.getAttribute('href')||p.startsWith(a.getAttribute('href')+'/'))a.classList.add('active');});})();
            if('serviceWorker' in navigator)navigator.serviceWorker.register('/sw.js').catch(function(){});
            </script>
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
                <a href="/dashboard" class="sidebar-link"><i class="bi bi-speedometer2"></i><span>Dashboard</span></a>
                <a href="/technician/dashboard" class="sidebar-link"><i class="bi bi-wrench"></i><span>Teknisyen</span></a>
                <a href="/jobs" class="sidebar-link"><i class="bi bi-list-task"></i><span>İş Emirleri</span></a>
                <a href="/customers" class="sidebar-link"><i class="bi bi-people"></i><span>Müşteriler</span></a>
                <a href="/products" class="sidebar-link"><i class="bi bi-box-seam"></i><span>Ürünler</span></a>
                <a href="/sales" class="sidebar-link"><i class="bi bi-cart"></i><span>Satış</span></a>
                <a href="/sales/history" class="sidebar-link"><i class="bi bi-clock-history"></i><span>Satış Geçmişi</span></a>
                <a href="/technician/schedule" class="sidebar-link"><i class="bi bi-calendar"></i><span>Program</span></a>
                <a href="/technician/route" class="sidebar-link"><i class="bi bi-map"></i><span>Rota Planı</span></a>
                <a href="/technician/profile" class="sidebar-link"><i class="bi bi-person"></i><span>Profil</span></a>
            </nav>
            <div class="sidebar-footer">
                <div class="sidebar-user"><i class="bi bi-person-circle"></i><span>{{userName}}</span></div>
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
                    <div class="login-logo"><i class="bi bi-shield-lock-fill"></i></div>
                    <h1>KamatekCRM</h1>
                    <p class="text-muted">Saha Teknisyen Paneli</p>
                </div>
                <div id="error-container">{{errorHtml}}</div>
                <form hx-post="/login" hx-target="#error-container" hx-swap="innerHTML" hx-indicator="#login-spinner" autocomplete="on">
                    {{tokenField}}
                    <div class="mb-3">
                        <label for="username" class="form-label"><i class="bi bi-person me-1"></i>Kullanıcı Adı</label>
                        <input type="text" class="form-control form-control-lg" id="username" name="username" placeholder="Kullanıcı adınızı girin" required autofocus />
                    </div>
                    <div class="mb-4">
                        <label for="password" class="form-label"><i class="bi bi-lock me-1"></i>Şifre</label>
                        <input type="password" class="form-control form-control-lg" id="password" name="password" placeholder="Şifrenizi girin" required />
                    </div>
                    <button type="submit" class="btn btn-primary btn-lg w-100 login-btn">
                        <span id="login-spinner" class="htmx-indicator"><span class="spinner-border spinner-border-sm me-2" role="status"></span></span>
                        <i class="bi bi-box-arrow-in-right me-2"></i>Giriş Yap
                    </button>
                </form>
                <div class="login-footer"><small class="text-muted">Kamatek Güvenlik Sistemleri © 2026</small></div>
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

        var now = DateTime.UtcNow;
        var greeting = now.Hour switch
        {
            < 12 => "Günaydın",
            < 18 => "İyi günler",
            _ => "İyi akşamlar"
        };

        var dashboardContent = $$"""
        <div class="dashboard-container">
            <div class="welcome-section">
                <h2 class="welcome-title">{{greeting}}, <strong>{{userName}}</strong> 👋</h2>
                <div class="d-flex align-items-center gap-2 flex-wrap">
                    {{roleDisplay}}
                    <span class="text-muted">|</span>
                    <span class="text-muted"><i class="bi bi-calendar3 me-1"></i>{{now:dd MMMM yyyy, dddd}}</span>
                </div>
            </div>

            <div class="row g-4 mt-2">
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-blue">
                        <div class="kpi-icon"><i class="bi bi-list-task"></i></div>
                        <div class="kpi-body"><span class="kpi-value">—</span><span class="kpi-label">Aktif Görev</span></div>
                    </div>
                </div>
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-green">
                        <div class="kpi-icon"><i class="bi bi-check-circle"></i></div>
                        <div class="kpi-body"><span class="kpi-value">—</span><span class="kpi-label">Tamamlanan</span></div>
                    </div>
                </div>
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-orange">
                        <div class="kpi-icon"><i class="bi bi-clock-history"></i></div>
                        <div class="kpi-body"><span class="kpi-value">—</span><span class="kpi-label">Bekleyen</span></div>
                    </div>
                </div>
                <div class="col-md-6 col-xl-3">
                    <div class="kpi-card kpi-purple">
                        <div class="kpi-icon"><i class="bi bi-geo-alt"></i></div>
                        <div class="kpi-body"><span class="kpi-value">—</span><span class="kpi-label">Saha Ziyareti</span></div>
                    </div>
                </div>
            </div>

            <div class="row g-4 mt-2">
                <div class="col-12">
                    <div class="content-card">
                        <h5 class="card-section-title"><i class="bi bi-lightning-fill text-warning me-2"></i>Hızlı İşlemler</h5>
                        <div class="d-flex flex-wrap gap-2 mt-3">
                            <a href="/jobs/new" class="btn btn-outline-primary"><i class="bi bi-plus-circle me-1"></i>Yeni Görev</a>
                            <a href="/technician/dashboard" class="btn btn-outline-success"><i class="bi bi-wrench me-1"></i>Teknisyen Panel</a>
                            <a href="/technician/schedule" class="btn btn-outline-info"><i class="bi bi-calendar me-1"></i>Günlük Program</a>
                        </div>
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
                <div class="mb-4"><i class="bi bi-exclamation-triangle-fill text-danger" style="font-size: 3rem;"></i></div>
                <h2>{{title}}</h2>
                <p class="text-muted">{{message}}</p>
                <a href="/login" class="btn btn-primary mt-3"><i class="bi bi-arrow-left me-1"></i>Giriş Sayfasına Dön</a>
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
                <td><a href="/customers/{c.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-eye"></i></a></td>
            </tr>
            """));

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <h2><i class="bi bi-people me-2"></i>Müşteriler</h2>
                <a href="/customers/new" class="btn btn-primary"><i class="bi bi-plus-lg me-1"></i>Yeni Müşteri</a>
            </div>
        </div>
        <div class="content-card">
            <form class="mb-3" hx-get="/customers" hx-target="tbody">
                <input type="text" name="search" class="form-control" placeholder="🔍 Müşteri ara..." value="{search ?? ""}">
            </form>
            <div class="table-responsive">
                <table class="table table-hover">
                    <thead><tr><th>Ad Soyad</th><th>Telefon</th><th>E-posta</th><th>Durum</th><th>İşlemler</th></tr></thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            <div class="text-muted mt-2" style="font-size:0.8rem">Toplam: {total} müşteri — Sayfa {page}</div>
        </div>
        """;
        return Layout("Müşteriler", content, userName, token);
    }

    public static string CustomerForm(CustomerListItem? customer, string? token)
    {
        var title = customer != null ? "Müşteri Düzenle" : "Yeni Müşteri";
        var content = $"""
        <div class="page-header"><h2><i class="bi bi-person-plus me-2"></i>{title}</h2></div>
        <div class="content-card">
            <form hx-post="/customers" hx-swap="outerHTML">
                <input type="hidden" name="Id" value="{customer?.Id ?? 0}">
                <div class="mb-3"><label class="form-label">Ad Soyad</label><input type="text" class="form-control" name="FullName" value="{customer?.FullName ?? ""}" required></div>
                <div class="mb-3"><label class="form-label">Telefon</label><input type="tel" class="form-control" name="PhoneNumber" value="{customer?.PhoneNumber ?? ""}"></div>
                <div class="mb-3"><label class="form-label">E-posta</label><input type="email" class="form-control" name="Email" value="{customer?.Email ?? ""}"></div>
                <div class="d-flex gap-2"><button type="submit" class="btn btn-primary"><i class="bi bi-check-lg me-1"></i>Kaydet</button><a href="/customers" class="btn btn-outline-secondary">İptal</a></div>
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
                <td><code>{p.SKU}</code></td>
                <td>{p.SalePrice:C2}</td>
                <td><span class="badge {(p.TotalStockQuantity > 0 ? "bg-success" : "bg-danger")}">{p.TotalStockQuantity}</span></td>
                <td><a href="/products/{p.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-eye"></i></a></td>
            </tr>
            """));

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <h2><i class="bi bi-box-seam me-2"></i>Ürünler</h2>
                <a href="/products/new" class="btn btn-primary"><i class="bi bi-plus-lg me-1"></i>Yeni Ürün</a>
            </div>
        </div>
        <div class="content-card">
            <div class="table-responsive">
                <table class="table table-hover">
                    <thead><tr><th>Ürün</th><th>SKU</th><th>Fiyat</th><th>Stok</th><th>İşlemler</th></tr></thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
        </div>
        """;
        return Layout("Ürünler", content, userName, token);
    }

    public static string ProductForm(ProductListItem? product, string? token)
    {
        var title = product != null ? "Ürün Düzenle" : "Yeni Ürün";
        var content = $"""
        <div class="page-header"><h2><i class="bi bi-box-seam me-2"></i>{title}</h2></div>
        <div class="content-card">
            <form hx-post="/products" hx-swap="outerHTML">
                <input type="hidden" name="Id" value="{product?.Id ?? 0}">
                <div class="mb-3"><label class="form-label">Ürün Adı</label><input type="text" class="form-control" name="ProductName" value="{product?.ProductName ?? ""}" required></div>
                <div class="mb-3"><label class="form-label">SKU</label><input type="text" class="form-control" name="SKU" value="{product?.SKU ?? ""}"></div>
                <div class="mb-3"><label class="form-label">Satış Fiyatı</label><input type="number" class="form-control" name="SalePrice" value="{product?.SalePrice}" step="0.01"></div>
                <div class="d-flex gap-2"><button type="submit" class="btn btn-primary"><i class="bi bi-check-lg me-1"></i>Kaydet</button><a href="/products" class="btn btn-outline-secondary">İptal</a></div>
            </form>
        </div>
        """;
        return Layout(title, content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  İŞ EMİRLERİ
    // ═══════════════════════════════════════════════════════════════

    public static string JobsPage(List<JobListItem> jobs, int total, int page, string userName, string? token, string? status = null, string? search = null)
    {
        var statusBadge = (string s) => s switch
        {
            "Tamamlandı" => "bg-success",
            "Devam Ediyor" => "bg-primary",
            "Bekliyor" => "bg-warning",
            "İptal" => "bg-danger",
            _ => "bg-secondary"
        };

        var rows = string.Join("", jobs.Select(j => $"""
            <tr>
                <td><strong>{j.Title}</strong></td>
                <td>{j.CustomerName ?? "-"}</td>
                <td><span class="badge {statusBadge(j.Status)}">{j.Status}</span></td>
                <td>{j.ScheduledDate?.ToString("dd.MM.yyyy") ?? "-"}</td>
                <td>
                    <a href="/jobs/{j.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-eye"></i></a>
                </td>
            </tr>
            """));

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <h2><i class="bi bi-list-task me-2"></i>İş Emirleri</h2>
                <a href="/jobs/new" class="btn btn-primary"><i class="bi bi-plus-lg me-1"></i>Yeni İş</a>
            </div>
        </div>
        <div class="content-card">
            <form class="mb-3 d-flex gap-2 flex-wrap" hx-get="/jobs" hx-target="tbody" hx-push-url="true">
                <input type="text" name="search" class="form-control" placeholder="🔍 İş emri ara..." value="{search ?? ""}" style="max-width:300px">
                <select name="status" class="form-select" style="max-width:180px" onchange="this.form.requestSubmit()">
                    <option value="">Tüm Durumlar</option>
                    <option value="Bekliyor" {(status == "Bekliyor" ? "selected" : "")}>Bekliyor</option>
                    <option value="Devam Ediyor" {(status == "Devam Ediyor" ? "selected" : "")}>Devam Ediyor</option>
                    <option value="Tamamlandı" {(status == "Tamamlandı" ? "selected" : "")}>Tamamlandı</option>
                </select>
            </form>
            <div class="table-responsive">
                <table class="table table-hover">
                    <thead><tr><th>Başlık</th><th>Müşteri</th><th>Durum</th><th>Tarih</th><th>İşlemler</th></tr></thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
            <div class="text-muted mt-2" style="font-size:0.8rem">Toplam: {total} iş emri — Sayfa {page}</div>
        </div>
        """;
        return Layout("İş Emirleri", content, userName, token);
    }

    public static string JobForm(JobListItem? job, string? token)
    {
        var isEdit = job != null;
        var title = isEdit ? "İş Düzenle" : "Yeni İş Emri";
        var action = isEdit ? $"hx-put=\"/jobs/{job!.Id}\"" : "hx-post=\"/jobs\"";

        var content = $"""
        <div class="page-header"><h2><i class="bi bi-{(isEdit ? "pencil" : "plus-circle")} me-2"></i>{title}</h2></div>
        <div class="content-card">
            <form {action} hx-swap="outerHTML">
                <input type="hidden" name="Id" value="{job?.Id ?? 0}">
                <div class="mb-3"><label class="form-label">Başlık</label><input type="text" class="form-control" name="Title" value="{job?.Title ?? ""}" required></div>
                <div class="mb-3"><label class="form-label">Açıklama</label><textarea class="form-control" name="Description" rows="3">{job?.Description ?? ""}</textarea></div>
                <div class="row">
                    <div class="col-md-6 mb-3">
                        <label class="form-label">Durum</label>
                        <select class="form-select" name="Status">
                            <option value="Bekliyor" {(job?.Status == "Bekliyor" ? "selected" : "")}>Bekliyor</option>
                            <option value="Devam Ediyor" {(job?.Status == "Devam Ediyor" ? "selected" : "")}>Devam Ediyor</option>
                            <option value="Tamamlandı" {(job?.Status == "Tamamlandı" ? "selected" : "")}>Tamamlandı</option>
                        </select>
                    </div>
                    <div class="col-md-6 mb-3">
                        <label class="form-label">Planlanan Tarih</label>
                        <input type="date" class="form-control" name="ScheduledDate" value="{job?.ScheduledDate?.ToString("yyyy-MM-dd") ?? ""}">
                    </div>
                </div>
                <div class="d-flex gap-2"><button type="submit" class="btn btn-primary"><i class="bi bi-check-lg me-1"></i>Kaydet</button><a href="/jobs" class="btn btn-outline-secondary">İptal</a></div>
            </form>
        </div>
        """;
        return Layout(title, content, antiforgeryToken: token);
    }

    public static string JobDetailPage(JobListItem? job, string userName, string? token)
    {
        if (job == null)
        {
            var notFoundContent = """
            <div class="content-card">
                <div class="alert alert-warning">İş bulunamadı.</div>
                <a href="/jobs" class="btn btn-secondary">İş Listesine Dön</a>
            </div>
            """;
            return Layout("İş Bulunamadı", notFoundContent, userName, token);
        }

        var statusBadge = job.Status switch
        {
            "Pending" or "Bekliyor" => "<span class=\"badge bg-warning\">Bekliyor</span>",
            "InProgress" or "Devam Ediyor" => "<span class=\"badge bg-primary\">Devam Ediyor</span>",
            "Completed" or "Tamamlandı" => "<span class=\"badge bg-success\">Tamamlandı</span>",
            "Cancelled" or "İptal" => "<span class=\"badge bg-danger\">İptal</span>",
            _ => $"<span class=\"badge bg-secondary\">{job.Status}</span>"
        };

        var content = $"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center">
                <h2><i class="bi bi-info-circle me-2"></i>{job.Title}</h2>
                <a href="/jobs" class="btn btn-secondary"><i class="bi bi-arrow-left"></i> Geri</a>
            </div>
        </div>
        <div class="row">
            <div class="col-md-8">
                <div class="content-card mb-3">
                    <h5>İş Detayları</h5>
                    <p><strong>Durum:</strong> {statusBadge}</p>
                    <p><strong>Müşteri:</strong> {job.CustomerName ?? "-"}</p>
                    <p><strong>Açıklama:</strong></p>
                    <p>{job.Description ?? "Açıklama yok"}</p>
                </div>
            </div>
            <div class="col-md-4">
                <div class="content-card mb-3">
                    <h5>İşlemler</h5>
                    <div class="d-grid gap-2">
                        <form hx-post="/technician/job/{job.Id}/start" hx-swap="outerHTML">
                            <button type="submit" class="btn btn-primary w-100"><i class="bi bi-play"></i> İşe Başla</button>
                        </form>
                        <form hx-post="/technician/job/{job.Id}/complete" hx-swap="outerHTML">
                            <button type="submit" class="btn btn-success w-100"><i class="bi bi-check-lg"></i> Tamamla</button>
                        </form>
                    </div>
                </div>
            </div>
        </div>
        """;
        return Layout("İş Detay", content, userName, token);
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
        <div class="page-header"><h2><i class="bi bi-cart me-2"></i>Satış</h2></div>
        <div class="content-card">
            <form hx-post="/sales" hx-swap="outerHTML">
                <div class="mb-3"><label class="form-label">Ürün</label><select class="form-select" name="ProductId">{productOptions}</select></div>
                <div class="mb-3"><label class="form-label">Adet</label><input type="number" class="form-control" name="Quantity" value="1" min="1"></div>
                <button type="submit" class="btn btn-primary"><i class="bi bi-cart-check me-1"></i>Satış Yap</button>
            </form>
        </div>
        """;
        return Layout("Satış", content, userName, token);
    }

    public static string SalesPage(string? token)
    {
        var content = """
        <div class="page-header"><h2><i class="bi bi-cart me-2"></i>Satış</h2></div>
        <div class="content-card"><p class="text-muted">Satış sayfası yükleniyor...</p></div>
        """;
        return Layout("Satış", content, antiforgeryToken: token);
    }

    public static string SalesHistoryPage(List<object> sales, int total, int page, string userName, string? token)
    {
        var content = $"""
        <div class="page-header"><h2><i class="bi bi-clock-history me-2"></i>Satış Geçmişi</h2></div>
        <div class="content-card"><p class="text-muted"><i class="bi bi-info-circle me-1"></i>Satış geçmişi yakında aktif edilecektir.</p></div>
        """;
        return Layout("Satış Geçmişi", content, userName, token);
    }

    public static string SalesHistoryPage(string? token)
    {
        var content = """
        <div class="page-header"><h2><i class="bi bi-clock-history me-2"></i>Satış Geçmişi</h2></div>
        <div class="content-card"><p class="text-muted"><i class="bi bi-info-circle me-1"></i>Satış geçmişi yakında aktif edilecektir.</p></div>
        """;
        return Layout("Satış Geçmişi", content, antiforgeryToken: token);
    }

    public static string JobDetail(string? token)
    {
        var content = """
        <div class="page-header"><h2><i class="bi bi-info-circle me-2"></i>İş Detayı</h2></div>
        <div class="content-card"><p class="text-muted">İş detayları yükleniyor...</p></div>
        """;
        return Layout("İş Detay", content, antiforgeryToken: token);
    }

    // ═══════════════════════════════════════════════════════════════
    //  TEKNİSYEN PANELİ — Enhanced
    // ═══════════════════════════════════════════════════════════════

    public static string TechnicianDashboard(string userName, string role, List<JobListItem> todayJobs, List<JobListItem> pendingJobs, DashboardStats stats, string? token)
    {
        var now = DateTime.UtcNow;
        var greeting = now.Hour switch { < 12 => "Günaydın", < 18 => "İyi günler", _ => "İyi akşamlar" };

        var todayJobsHtml = todayJobs.Count > 0
            ? string.Join("", todayJobs.Select(j => $"""
                <div class="card mb-2">
                    <div class="card-body d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="mb-1">{j.Title}</h6>
                            <small class="text-muted"><i class="bi bi-person me-1"></i>{j.CustomerName ?? "-"}</small>
                        </div>
                        <span class="badge bg-{(j.Status == "Tamamlandı" ? "success" : j.Status == "Devam Ediyor" ? "primary" : "warning")}">{j.Status}</span>
                    </div>
                </div>
                """))
            : """<div class="text-center py-4"><i class="bi bi-calendar-check text-muted" style="font-size:2rem"></i><p class="text-muted mt-2">Bugün planlanmış iş yok</p></div>""";

        var pendingJobsHtml = pendingJobs.Count > 0
            ? string.Join("", pendingJobs.Select(j => $"""
                <div class="card mb-2">
                    <div class="card-body d-flex justify-content-between align-items-center">
                        <div>
                            <h6 class="mb-1">{j.Title}</h6>
                            <small class="text-muted"><i class="bi bi-person me-1"></i>{j.CustomerName ?? "-"}</small>
                        </div>
                        <a href="/jobs/{j.Id}" class="btn btn-sm btn-outline-primary"><i class="bi bi-arrow-right"></i></a>
                    </div>
                </div>
                """))
            : """<div class="text-center py-4"><i class="bi bi-check-circle text-success" style="font-size:2rem"></i><p class="text-muted mt-2">Bekleyen iş yok</p></div>""";

        var content = $$"""
        <div class="dashboard-container">
            <div class="welcome-section">
                <h2 class="welcome-title">{{greeting}}, <strong>{{userName}}</strong> 🔧</h2>
                <div class="d-flex align-items-center gap-2 flex-wrap">
                    <span class="badge bg-info"><i class="bi bi-wrench me-1"></i>Teknisyen</span>
                    <span class="text-muted">|</span>
                    <span class="text-muted"><i class="bi bi-calendar3 me-1"></i>{{now:dd MMMM yyyy, dddd}}</span>
                </div>
            </div>

            <div class="row g-3 mt-2">
                <div class="col-6 col-xl-3">
                    <div class="kpi-card kpi-blue">
                        <div class="kpi-icon"><i class="bi bi-list-task"></i></div>
                        <div class="kpi-body"><span class="kpi-value">{{stats.ActiveJobs}}</span><span class="kpi-label">Aktif İşler</span></div>
                    </div>
                </div>
                <div class="col-6 col-xl-3">
                    <div class="kpi-card kpi-green">
                        <div class="kpi-icon"><i class="bi bi-check-circle"></i></div>
                        <div class="kpi-body"><span class="kpi-value">{{stats.CompletedToday}}</span><span class="kpi-label">Bugün Tamamlanan</span></div>
                    </div>
                </div>
                <div class="col-6 col-xl-3">
                    <div class="kpi-card kpi-orange">
                        <div class="kpi-icon"><i class="bi bi-clock-history"></i></div>
                        <div class="kpi-body"><span class="kpi-value">{{stats.PendingJobs}}</span><span class="kpi-label">Bekleyen</span></div>
                    </div>
                </div>
                <div class="col-6 col-xl-3">
                    <div class="kpi-card kpi-purple">
                        <div class="kpi-icon"><i class="bi bi-geo-alt"></i></div>
                        <div class="kpi-body"><span class="kpi-value">{{stats.FieldVisits}}</span><span class="kpi-label">Saha Ziyareti</span></div>
                    </div>
                </div>
            </div>

            <div class="row g-4 mt-2">
                <div class="col-lg-6">
                    <div class="content-card">
                        <h5 class="card-section-title"><i class="bi bi-calendar-event text-primary me-2"></i>Bugünkü İşler</h5>
                        {{todayJobsHtml}}
                    </div>
                </div>
                <div class="col-lg-6">
                    <div class="content-card">
                        <h5 class="card-section-title"><i class="bi bi-hourglass-split text-warning me-2"></i>Bekleyen İşler</h5>
                        {{pendingJobsHtml}}
                    </div>
                </div>
            </div>

            <div class="row g-4 mt-1">
                <div class="col-12">
                    <div class="content-card">
                        <h5 class="card-section-title"><i class="bi bi-lightning-fill text-warning me-2"></i>Hızlı İşlemler</h5>
                        <div class="d-flex flex-wrap gap-2 mt-2">
                            <a href="/jobs/new" class="btn btn-outline-primary"><i class="bi bi-plus-circle me-1"></i>Yeni İş Emri</a>
                            <a href="/technician/schedule" class="btn btn-outline-info"><i class="bi bi-calendar me-1"></i>Günlük Program</a>
                            <a href="/customers" class="btn btn-outline-success"><i class="bi bi-people me-1"></i>Müşteriler</a>
                        </div>
                    </div>
                </div>
            </div>
        </div>
        """;
        return Layout("Teknisyen Panel", content, userName, token);
    }

    public static string SchedulePage(List<JobListItem> jobs, DateTime date, string userName, string? token)
    {
        var prevDate = date.AddDays(-1).ToString("yyyy-MM-dd");
        var nextDate = date.AddDays(1).ToString("yyyy-MM-dd");
        var isToday = date.Date == DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var timeSlots = new[] { "08:00", "09:00", "10:00", "11:00", "12:00", "13:00", "14:00", "15:00", "16:00", "17:00" };
        var timelineHtml = string.Join("", timeSlots.Select(slot => {
            var slotJobs = jobs.Where(j => j.ScheduledDate?.ToString("HH:mm") == slot).ToList();
            var jobCards = slotJobs.Count > 0
                ? string.Join("", slotJobs.Select(j => $"""
                    <div class="d-flex align-items-center gap-2 p-2 rounded" style="background:var(--bg-hover)">
                        <span class="badge bg-{(j.Status == "Tamamlandı" ? "success" : j.Status == "Devam Ediyor" ? "primary" : "warning")}">{j.Status}</span>
                        <div><strong>{j.Title}</strong><br><small class="text-muted">{j.CustomerName ?? "-"}</small></div>
                    </div>
                    """))
                : "<span class=\"text-muted\" style=\"font-size:0.8rem\">—</span>";
            return $"""
                <div class="d-flex gap-3 py-2" style="border-bottom:1px solid var(--border)">
                    <div style="width:60px;flex-shrink:0;color:var(--text-muted);font-weight:600;font-size:0.85rem">{slot}</div>
                    <div style="flex:1">{jobCards}</div>
                </div>
            """;
        }));

        var unscheduledJobs = jobs.Where(j => j.ScheduledDate == null || !timeSlots.Contains(j.ScheduledDate?.ToString("HH:mm"))).ToList();
        var unscheduledHtml = unscheduledJobs.Count > 0
            ? string.Join("", unscheduledJobs.Select(j => $"""
                <div class="card mb-2">
                    <div class="card-body d-flex justify-content-between align-items-center">
                        <div><h6 class="mb-0">{j.Title}</h6><small class="text-muted">{j.CustomerName ?? "-"}</small></div>
                        <span class="badge bg-secondary">{j.Status}</span>
                    </div>
                </div>
                """))
            : "";

        var content = $$"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <h2><i class="bi bi-calendar me-2"></i>Günlük Program</h2>
                <div class="d-flex align-items-center gap-2">
                    <a href="/technician/schedule?date={{prevDate}}" class="btn btn-sm btn-outline-secondary"><i class="bi bi-chevron-left"></i></a>
                    <span class="fw-semibold">{{date:dd MMMM yyyy, dddd}}</span>
                    <a href="/technician/schedule?date={{nextDate}}" class="btn btn-sm btn-outline-secondary"><i class="bi bi-chevron-right"></i></a>
                </div>
            </div>
            {{(isToday ? "<span class=\"badge bg-primary mt-2\">Bugün</span>" : "")}}
        </div>
        <div class="content-card">
            <h5 class="card-section-title"><i class="bi bi-clock text-primary me-2"></i>Zaman Çizelgesi</h5>
            {{timelineHtml}}
        </div>
        {{(unscheduledHtml.Length > 0 ? $"<div class=\"content-card\"><h5 class=\"card-section-title\"><i class=\"bi bi-list-ul text-warning me-2\"></i>Saati Belirtilmemiş</h5>{unscheduledHtml}</div>" : "")}}
        <div class="text-muted mt-2" style="font-size:0.8rem"><i class="bi bi-info-circle me-1"></i>Toplam: {{jobs.Count}} iş planlanmış</div>
        """;
        return Layout("Program", content, userName, token);
    }

    public static string TechnicianProfile(string userName, string role, string username, string? token)
    {
        var roleDisplay = role switch
        {
            "Admin" => """<span class="badge bg-danger"><i class="bi bi-star-fill me-1"></i>Yönetici</span>""",
            "Technician" => """<span class="badge bg-info"><i class="bi bi-wrench me-1"></i>Teknisyen</span>""",
            _ => $"""<span class="badge bg-secondary">{role}</span>"""
        };

        var content = $$"""
        <div class="page-header"><h2><i class="bi bi-person me-2"></i>Profil</h2></div>
        <div class="content-card">
            <div class="text-center mb-4">
                <div style="width:80px;height:80px;border-radius:50%;background:linear-gradient(135deg,var(--accent-blue),var(--accent-cyan));display:inline-flex;align-items:center;justify-content:center;font-size:2rem;color:white;margin-bottom:12px">
                    <i class="bi bi-person-fill"></i>
                </div>
                <h4 class="mb-1">{{userName}}</h4>
                {{roleDisplay}}
            </div>
            <div class="row g-3">
                <div class="col-md-6">
                    <div class="p-3 rounded" style="background:var(--bg-hover)">
                        <small class="text-muted d-block mb-1">Kullanıcı Adı</small>
                        <strong>{{username}}</strong>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="p-3 rounded" style="background:var(--bg-hover)">
                        <small class="text-muted d-block mb-1">Rol</small>
                        <strong>{{role}}</strong>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="p-3 rounded" style="background:var(--bg-hover)">
                        <small class="text-muted d-block mb-1">Son Giriş</small>
                        <strong>Şimdi aktif</strong>
                    </div>
                </div>
                <div class="col-md-6">
                    <div class="p-3 rounded" style="background:var(--bg-hover)">
                        <small class="text-muted d-block mb-1">Uygulama</small>
                        <strong>KamatekCRM v13.1</strong>
                    </div>
                </div>
            </div>
        </div>
        <div class="content-card">
            <h5 class="card-section-title"><i class="bi bi-gear text-primary me-2"></i>Ayarlar</h5>
            <div class="d-flex flex-wrap gap-2 mt-2">
                <button class="btn btn-outline-primary" disabled><i class="bi bi-key me-1"></i>Şifre Değiştir</button>
                <button class="btn btn-outline-info" disabled><i class="bi bi-bell me-1"></i>Bildirim Ayarları</button>
            </div>
            <p class="text-muted mt-3 mb-0" style="font-size:0.8rem"><i class="bi bi-info-circle me-1"></i>Ayar özellikleri yakında aktif edilecektir.</p>
        </div>
        """;
        return Layout("Profil", content, userName, token);
    }

    // ══════════════════════════════════════════════════════════════
    //  ROTA PLANLAMA SAYFASI — Leaflet + HTMX
    // ══════════════════════════════════════════════════════════════

    public static string RoutePlanningPage(List<KamatekCrm.Web.Features.Route.RoutePointDto> points, DateTime date, string userName, int userId, string? token)
    {
        var prevDate = date.AddDays(-1).ToString("yyyy-MM-dd");
        var nextDate = date.AddDays(1).ToString("yyyy-MM-dd");
        var isToday = date.Date == DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

        var totalPoints = points.Count;
        var completedPoints = points.Count(p => p.IsVisited);
        var progressPercent = totalPoints > 0 ? (int)(completedPoints * 100.0 / totalPoints) : 0;

        var markersJson = totalPoints > 0
            ? string.Join(",", points.Select(p =>
                $"{{\"lat\":{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"\"lng\":{p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                $"\"title\":\"{EscapeJson(p.JobTitle ?? p.Address)}\"," +
                $"\"customer\":\"{EscapeJson(p.CustomerName ?? "")}\"," +
                $"\"order\":{p.OrderIndex}," +
                $"\"visited\":{(p.IsVisited ? "true" : "false")}}}"))
            : "";

        var routeCardsHtml = totalPoints > 0
            ? string.Join("", points.OrderBy(p => p.OrderIndex).Select(p =>
            {
                var statusClass = p.IsVisited ? "bg-success" : "bg-warning";
                var statusText = p.IsVisited ? "Ziyaret Edildi" : "Bekliyor";
                var statusIcon = p.IsVisited ? "check-circle" : "clock";
                var phoneLink = !string.IsNullOrEmpty(p.CustomerPhone)
                    ? $"""<a href="tel:{p.CustomerPhone}" class="btn btn-sm btn-outline-success"><i class="bi bi-telephone"></i></a>"""
                    : "";
                var visitBtnHtml = !p.IsVisited
                    ? $"""<button class="btn btn-sm btn-success ms-1" hx-post="/technician/route/visit/{p.Id}" hx-target="#status-{p.Id}" hx-swap="innerHTML" title="Ziyaret Edildi"><i class="bi bi-check-lg"></i></button>"""
                    : "";
                var navBtn = p.Latitude != 0 && p.Longitude != 0
                    ? $"""<a href="https://www.google.com/maps/dir/?api=1&destination={p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" target="_blank" class="btn btn-sm btn-outline-primary" title="Navigasyon"><i class="bi bi-cursor"></i></a>"""
                    : "";

                return $"""
                <div class="card mb-2" style="border-left:4px solid var(--bs-{(p.IsVisited ? "success" : "warning")})">
                    <div class="card-body py-2 px-3">
                        <div class="d-flex justify-content-between align-items-start">
                            <div class="d-flex align-items-start gap-2">
                                <div class="rounded-circle d-flex align-items-center justify-content-center text-white fw-bold" style="width:28px;height:28px;min-width:28px;font-size:0.8rem;background:{(p.IsVisited ? "var(--bs-success)" : "var(--bs-primary)")}">{p.OrderIndex}</div>
                                <div>
                                    <h6 class="mb-0" style="font-size:0.9rem">{p.JobTitle ?? p.Address}</h6>
                                    <small class="text-muted"><i class="bi bi-person me-1"></i>{p.CustomerName ?? "-"}</small>
                                    {(p.Address != "" ? $"<br><small class=\"text-muted\"><i class=\"bi bi-geo-alt me-1\"></i>{p.Address}</small>" : "")}
                                </div>
                            </div>
                            <div class="d-flex align-items-center gap-1">
                                {phoneLink}
                                {navBtn}
                                {visitBtnHtml}
                            </div>
                        </div>
                        <div class="mt-1" id="status-{p.Id}">
                            <span class="badge {statusClass}"><i class="bi bi-{statusIcon} me-1"></i>{statusText}</span>
                        </div>
                    </div>
                </div>
                """;
            }))
            : """<div class="text-center py-5"><i class="bi bi-map text-muted" style="font-size:3rem"></i><p class="text-muted mt-2">Bu tarih için planlanmış rota yok</p></div>""";

        var centerLat = totalPoints > 0 ? points.Average(p => p.Latitude).ToString(System.Globalization.CultureInfo.InvariantCulture) : "39.7766";
        var centerLng = totalPoints > 0 ? points.Average(p => p.Longitude).ToString(System.Globalization.CultureInfo.InvariantCulture) : "30.5206";

        var content = $$"""
        <div class="page-header">
            <div class="d-flex justify-content-between align-items-center flex-wrap gap-2">
                <h2><i class="bi bi-map me-2"></i>Rota Planı</h2>
                <div class="d-flex align-items-center gap-2">
                    <a href="/technician/route?date={{prevDate}}" class="btn btn-sm btn-outline-secondary"><i class="bi bi-chevron-left"></i></a>
                    <span class="fw-semibold">{{date:dd MMMM yyyy, dddd}}</span>
                    <a href="/technician/route?date={{nextDate}}" class="btn btn-sm btn-outline-secondary"><i class="bi bi-chevron-right"></i></a>
                </div>
            </div>
            {{(isToday ? "<span class=\"badge bg-primary mt-2\">Bugün</span>" : "")}}
        </div>

        <div class="row g-3 mb-3">
            <div class="col-4">
                <div class="kpi-card kpi-blue">
                    <div class="kpi-icon"><i class="bi bi-geo-alt"></i></div>
                    <div class="kpi-body"><span class="kpi-value">{{totalPoints}}</span><span class="kpi-label">Toplam</span></div>
                </div>
            </div>
            <div class="col-4">
                <div class="kpi-card kpi-green">
                    <div class="kpi-icon"><i class="bi bi-check-circle"></i></div>
                    <div class="kpi-body"><span class="kpi-value">{{completedPoints}}</span><span class="kpi-label">Tamamlanan</span></div>
                </div>
            </div>
            <div class="col-4">
                <div class="kpi-card kpi-orange">
                    <div class="kpi-icon"><i class="bi bi-percent"></i></div>
                    <div class="kpi-body"><span class="kpi-value">%{{progressPercent}}</span><span class="kpi-label">İlerleme</span></div>
                </div>
            </div>
        </div>

        {{(totalPoints > 0 ? $"<div class=\"progress mb-3\" style=\"height:6px\"><div class=\"progress-bar bg-success\" style=\"width:{progressPercent}%\"></div></div>" : "")}}

        <div class="content-card mb-3 p-0" style="overflow:hidden;border-radius:12px">
            <div id="route-map" style="height:350px;width:100%"></div>
        </div>

        <div class="content-card">
            <h5 class="card-section-title"><i class="bi bi-signpost-2 text-primary me-2"></i>Rota Durakları ({{totalPoints}})</h5>
            {{routeCardsHtml}}
        </div>

        <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"/>
        <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
        <style>
            .route-marker{background:none;border:none;}
            .route-pin{width:28px;height:28px;border-radius:50%;display:flex;align-items:center;justify-content:center;
                color:#fff;font-weight:700;font-size:12px;box-shadow:0 2px 6px rgba(0,0,0,.3);border:2px solid #fff;}
            .pin-pending{background:linear-gradient(135deg,#f59e0b,#d97706);}
            .pin-visited{background:linear-gradient(135deg,#10b981,#059669);}
        </style>
        <script>
        document.addEventListener('DOMContentLoaded',function(){
            var map=L.map('route-map').setView([{{centerLat}},{{centerLng}}],13);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{maxZoom:19,attribution:'© OSM'}).addTo(map);
            var markers=[{{markersJson}}];
            var latlngs=[];
            markers.forEach(function(m){
                var cls=m.visited?'pin-visited':'pin-pending';
                var icon=L.divIcon({className:'route-marker',html:'<div class="route-pin '+cls+'">'+m.order+'</div>',iconSize:[28,28],iconAnchor:[14,14]});
                var mk=L.marker([m.lat,m.lng],{icon:icon}).addTo(map);
                mk.bindPopup('<b>#'+m.order+' '+m.title+'</b><br>'+m.customer);
                latlngs.push([m.lat,m.lng]);
            });
            if(latlngs.length>1){
                L.polyline(latlngs,{color:'#3b82f6',weight:3,opacity:0.7,dashArray:'10,6'}).addTo(map);
                map.fitBounds(L.latLngBounds(latlngs).pad(0.15));
            }else if(latlngs.length===1){map.setView(latlngs[0],15);}
        });
        </script>
        """;
        return Layout("Rota Planı", content, userName, token);
    }

    private static string EscapeJson(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}
