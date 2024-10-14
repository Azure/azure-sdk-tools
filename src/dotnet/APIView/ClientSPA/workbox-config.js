const version = 'v1.0.0';
const staticCacheName = `static-${version}`;
const globPatterns = [
  'favicon.ico',
  'index.html',
  'manifest.webmanifest',
  '*.css',
  '*.js',
  'assets/**/*.{svg,cur,jpg,jpeg,png,apng,webp,avif,gif,otf,ttf,woff,woff2,json}'
];

module.exports = {
  globDirectory: '../APIViewWeb/wwwroot/spa',
  globPatterns: globPatterns,
  swDest: '../APIViewWeb/wwwroot/spa/sw.js',
  skipWaiting: true,
  clientsClaim: true,
  cleanupOutdatedCaches: true,
};

self.addEventListener('install', event => {
  self.skipWaiting();
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames.map(cacheName => caches.delete(cacheName))
      );
    }).then(() => {
      return caches.open(staticCacheName).then(cache => {
        return cache.addAll(globPatterns);
      });
    })
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(self.clients.claim());
});