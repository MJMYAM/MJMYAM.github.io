const CACHE="wc2026-v2";
const ASSETS=["./","./index.html","./icon-192.png","./icon-512.png","./manifest.json"];
self.addEventListener("install", e=>{
  e.waitUntil(caches.open(CACHE).then(c=>c.addAll(ASSETS)).then(()=>self.skipWaiting()));
});
self.addEventListener("activate", e=>{
  e.waitUntil(caches.keys().then(ks=>Promise.all(ks.filter(k=>k!==CACHE).map(k=>caches.delete(k)))).then(()=>self.clients.claim()));
});
self.addEventListener("fetch", e=>{
  const url=e.request.url;
  // بيانات ESPN الحية: شبكة دائمًا (لا نكاشها)
  if(url.includes("espn.com") || url.includes("api.anthropic.com") || url.includes("flagcdn") || url.includes("espncdn")){
    e.respondWith(fetch(e.request).catch(()=>caches.match(e.request)));
    return;
  }
  // باقي الملفات: كاش أولاً ثم شبكة
  e.respondWith(
    caches.match(e.request).then(r=> r || fetch(e.request).then(resp=>{
      if(e.request.method==="GET" && resp.status===200){
        const cp=resp.clone(); caches.open(CACHE).then(c=>c.put(e.request,cp));
      }
      return resp;
    }).catch(()=>caches.match("./index.html")))
  );
});
