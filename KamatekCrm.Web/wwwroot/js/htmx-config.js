/* ═══════════════════════════════════════════════════════════
   KamatekCRM — HTMX Configuration + Toast Notification System
   CSP uyumlu: eval() kullanmaz.
   ═══════════════════════════════════════════════════════════ */

// ─── 1. ANTIFORGERY TOKEN ───
// HTMX her istek öncesi bu event'i ateşler.
document.addEventListener('htmx:configRequest', function (evt) {
    var tokenInput = document.getElementById('xsrf-token');
    if (!tokenInput) {
        tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    }
    if (tokenInput) {
        evt.detail.headers['X-XSRF-TOKEN'] = tokenInput.value;
    }
});

// ─── 2. HTMX ERROR RESPONSE SWAP FIX ───
// ⚠️ ROOT CAUSE #1: HTMX varsayılan olarak non-2xx yanıtlarda swap yapmaz.
//    Swap yapılmadığında HX-Trigger header'ı da işlenmez → toast tetiklenmez!
//
// Çözüm: htmx:beforeSwap event'i ile 4xx/5xx yanıtlarda da swap'a izin ver.
//    Bu sayede:
//    - Error HTML snippet'i #error-container'a swap edilir ✓
//    - HX-Trigger header'ındaki showToast event'i tetiklenir ✓
//
document.addEventListener('htmx:beforeSwap', function (evt) {
    var status = evt.detail.xhr.status;

    // 4xx Client errors: swap yap (login hataları, validation vb.)
    if (status >= 400 && status < 500) {
        evt.detail.shouldSwap = true;
        evt.detail.isError = false;
    }
    // 5xx Server errors: swap yap (bağlantı hatası mesajları vb.)
    else if (status >= 500) {
        evt.detail.shouldSwap = true;
        evt.detail.isError = false;
    }
});

// ─── 3. UNAUTHORIZED REDIRECT ───
// ⚠️ ROOT CAUSE #2 (ÖNCEKİ HALİ): htmx:responseError'da 401'de anında redirect
//    yapıyorduk → toast DOM'a eklenmeden sayfa değişiyordu!
//
// Düzeltme: Sadece login sayfası DIŞINDA 401 alınırsa redirect yap.
//    Login sayfasında 401 beklenen bir durum (yanlış şifre).
//    beforeSwap zaten swap'a izin verdiği için toast + error div gösterilir.
//
document.addEventListener('htmx:responseError', function (evt) {
    if (evt.detail.xhr.status === 401) {
        // Login sayfasındaysak redirect yapma — toast ve error div yeterli
        if (window.location.pathname === '/login') {
            return;
        }
        // Diğer sayfalarda (session expired) login'e yönlendir
        window.location.href = '/login';
    }
});

// ─── 4. TOAST NOTIFICATION SYSTEM ───
// HX-Trigger header'dan gelen "showToast" event'ini dinler.
//
// HTMX akışı:
//   1. Sunucu HX-Trigger: {"showToast": {"message":"...","type":"error"}} döner
//   2. HTMX JSON'u parse eder
//   3. "showToast" adında CustomEvent oluşturur, detail = {message, type}
//   4. document.body üzerinde dispatch eder
//   5. Bu listener yakalar ve Bootstrap Toast render eder
//
document.body.addEventListener('showToast', function (evt) {
    var detail = evt.detail;
    if (!detail || !detail.message) return;

    var type = detail.type || 'info';

    // Bootstrap renk sınıfı
    var bgClass = {
        'success': 'text-bg-success',
        'error': 'text-bg-danger',
        'warning': 'text-bg-warning',
        'info': 'text-bg-info'
    }[type] || 'text-bg-info';

    // Bootstrap icon
    var icon = {
        'success': 'bi-check-circle-fill',
        'error': 'bi-exclamation-triangle-fill',
        'warning': 'bi-exclamation-circle-fill',
        'info': 'bi-info-circle-fill'
    }[type] || 'bi-info-circle-fill';

    // Toast element oluştur
    var toastEl = document.createElement('div');
    toastEl.className = 'toast align-items-center border-0 ' + bgClass;
    toastEl.setAttribute('role', 'alert');
    toastEl.setAttribute('aria-live', 'assertive');
    toastEl.setAttribute('aria-atomic', 'true');
    toastEl.innerHTML =
        '<div class="d-flex">' +
        '<div class="toast-body">' +
        '<i class="bi ' + icon + ' me-2"></i>' +
        detail.message +
        '</div>' +
        '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Kapat"></button>' +
        '</div>';

    // Container'a ekle
    var container = document.getElementById('toast-container');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'toast-container position-fixed top-0 end-0 p-3';
        container.style.zIndex = '9999';
        document.body.appendChild(container);
    }
    container.appendChild(toastEl);

    // Bootstrap Toast API
    var toast = new bootstrap.Toast(toastEl, {
        autohide: true,
        delay: 4000,
        animation: true
    });
    toast.show();

    // Temizle
    toastEl.addEventListener('hidden.bs.toast', function () {
        toastEl.remove();
    });
});
