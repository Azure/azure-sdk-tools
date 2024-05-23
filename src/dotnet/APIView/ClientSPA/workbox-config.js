module.exports = {
    globDirectory: '../APIViewWeb/wwwroot/spa',
    globPatterns: [
      'favicon.ico',
      'index.html',
      'manifest.webmanifest',
      '*.css',
      '*.js',
      'assets/**/*.{svg,cur,jpg,jpeg,png,apng,webp,avif,gif,otf,ttf,woff,woff2,json}'
    ],
    swDest: '../APIViewWeb/wwwroot/spa/sw.js',
    runtimeCaching: [
      {
        urlPattern: /^https:\/\/apiviewstagingtest\.com\/api\/reviews\/.*\/content\/.*$/,
        handler: 'CacheFirst',
        options: {
          cacheName: 'revisioncontent',
          expiration: {
            maxEntries: 50,
            maxAgeSeconds: 24 * 60 * 60, // 1 day
          },
        },
      },
      {
        urlPattern: /^https:\/\/apiviewuxtest\.com\/api\/reviews\/.*\/content\/.*$/,
        handler: 'CacheFirst',
        options: {
          cacheName: 'revisioncontent',
          expiration: {
            maxEntries: 50,
            maxAgeSeconds: 24 * 60 * 60, // 1 day
          },
        },
      },
      {
        urlPattern: /^https:\/\/apiview\.com\/api\/reviews\/.*\/content\/.*$/,
        handler: 'CacheFirst',
        options: {
          cacheName: 'revisioncontent',
          expiration: {
            maxEntries: 50,
            maxAgeSeconds: 24 * 60 * 60, // 1 day
          },
        },
      },
    ],
    skipWaiting: true,
    clientsClaim: true,
  };