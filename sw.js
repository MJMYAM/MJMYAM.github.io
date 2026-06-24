// إصدار جديد يجبر مسح كل الكاش القديم
const CACHE="wc2026-v22";
self.addEventListener("install", e=>{ self.skipWaiting(); });
self.addEventListener("activate", e=>{
  e.waitUntil(
    caches.keys().then(ks=>Promise.all(ks.map(k=>caches.delete(k)))) // امسح كل الكاش
    .then(()=>self.clients.claim())
  );
});
self.addEventListener("fetch", e=>{
  const url=e.request.url;
  // كل شي من الشبكة دائمًا (لا كاش لـ index) — يضمن أحدث نسخة دائمًا
  if(url.includes("espn.com")||url.includes("anthropic.com")||url.includes("flagcdn")||url.includes("espncdn")){
    e.respondWith(fetch(e.request).catch(()=>caches.match(e.request)));
    return;
  }
  e.respondWith(fetch(e.request).catch(()=>caches.match(e.request)));
});
