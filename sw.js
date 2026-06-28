// إصدار يجبر مسح الكاش القديم + يجيب الصفحة دائمًا من الشبكة بدون كاش المتصفح
const CACHE="wc2026-v37";
self.addEventListener("install", e=>{ self.skipWaiting(); });
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
    // أحدث نسخة دائمًا — تجاوز كاش المتصفح تمامًا
    e.respondWith(fetch(req,{cache:"no-store"}).catch(()=>caches.match(req).then(r=>r||caches.match("/index.html"))));
    return;
  }
  e.respondWith(fetch(req).catch(()=>caches.match(req)));
});
