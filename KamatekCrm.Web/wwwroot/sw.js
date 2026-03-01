// KamatekCRM Service Worker — Network-First, Offline Fallback
const CACHE_NAME = 'kamatek-crm-v1';
const OFFLINE_URL = '/offline';

// Pre-cache critical assets
const PRECACHE_URLS = [
    '/css/site.css',
    '/js/htmx-config.js',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css',
    'https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css',
    'https://cdn.jsdelivr.net/npm/htmx.org@2.0.4/dist/htmx.min.js'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => cache.addAll(PRECACHE_URLS))
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

// Network-first strategy: try network, fallback to cache
self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') return;

    event.respondWith(
        fetch(event.request)
            .then(response => {
                // Cache successful responses
                if (response.ok) {
                    const clone = response.clone();
                    caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
                }
                return response;
            })
            .catch(() => {
                return caches.match(event.request).then(cached => {
                    if (cached) return cached;
                    // If HTML request and no cache, show offline page
                    if (event.request.headers.get('Accept')?.includes('text/html')) {
                        return new Response(`
              <!DOCTYPE html>
              <html lang="tr" data-bs-theme="dark">
              <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Çevrimdışı — KamatekCRM</title>
              <style>body{background:#0B0E14;color:#F1F5F9;font-family:Inter,sans-serif;display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0}
              .offline{text-align:center;padding:2rem}.icon{font-size:4rem;margin-bottom:1rem}
              .btn{background:#3B82F6;color:#fff;border:none;padding:12px 24px;border-radius:8px;font-size:1rem;cursor:pointer;margin-top:1rem}</style></head>
              <body><div class="offline"><div class="icon">📡</div><h2>Çevrimdışısınız</h2>
              <p>İnternet bağlantınızı kontrol edin.</p>
              <button class="btn" onclick="location.reload()">Tekrar Dene</button></div></body></html>
            `, { headers: { 'Content-Type': 'text/html; charset=utf-8' } });
                    }
                    return new Response('', { status: 503 });
                });
            })
    );
});
