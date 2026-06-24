const CACHE="wc2026-v10";
const ASSETS=["./","./index.html","./icon-192.png","./icon-512.png","./manifest.json"];
self.addEventListener("install", e=>{
  self.skipWaiting();  // فعّل النسخة الجديدة فورًا بدون انتظار
  e.waitUntil(caches.open(CACHE).then(c=>c.addAll(ASSETS)));
});
self.addEventListener("activate", e=>{
  e.waitUntil(
    caches.keys().then(ks=>Promise.all(ks.filter(k=>k!==CACHE).map(k=>caches.delete(k))))
    .then(()=>self.clients.claim())
  );
});
self.addEventListener("fetch", e=>{
  const url=e.request.url;
  // index.html دائمًا من الشبكة أولاً (Network First) عشان يجيب آخر نسخة
  if(url.includes("index.html") || url.endsWith("github.io/") || url.endsWith("github.io")){
    e.respondWith(
      fetch(e.request).then(resp=>{
        const cp=resp.clone(); caches.open(CACHE).then(c=>c.put(e.request,cp));
        return resp;
      }).catch(()=>caches.match(e.request).then(r=>r||caches.match("./index.html")))
    );
    return;
  }
  // ESPN وغيرها: شبكة دائمًا
  if(url.includes("espn.com")||url.includes("anthropic.com")||url.includes("flagcdn")||url.includes("espncdn")){
    e.respondWith(fetch(e.request).catch(()=>caches.match(e.request)));
    return;
  }
  // باقي الأصول: كاش أولاً
  e.respondWith(caches.match(e.request).then(r=>r||fetch(e.request)));
});
