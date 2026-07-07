// v38: شبكة أولاً للصفحة (بدون كاش المتصفح) + تفعيل فوري للنسخة الجديدة + مسح الكاش القديم
const CACHE="wc2026-v55";
self.addEventListener("install", e=>{ self.skipWaiting(); });
self.addEventListener("message", e=>{ if(e.data && e.data.type==="SKIP_WAITING") self.skipWaiting(); });
self.addEventListener("activate", e=>{
  e.waitUntil(
    caches.keys().then(ks=>Promise.all(ks.map(k=>caches.delete(k))))
    .then(()=>self.clients.claim())
  );
});
self.addEventListener("fetch", e=>{
  const req=e.request, url=req.url;
  const isDoc = req.mode==="navigate" || req.destination==="document" || url.endsWith("/") || url.endsWith("index.html");
  if(isDoc){
    e.respondWith(fetch(req,{cache:"no-store"}).catch(()=>caches.match(req).then(r=>r||caches.match("/index.html"))));
    return;
  }
  e.respondWith(fetch(req).catch(()=>caches.match(req)));
});
