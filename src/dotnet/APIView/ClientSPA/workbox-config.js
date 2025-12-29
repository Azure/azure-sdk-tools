const version = process.env.BUILD_BUILDID || 'dev';
const globPatterns = [
  'favicon.ico',
  'index.html',
  'manifest.webmanifest',
  '*.css',
  '*.js',
  'assets/**/*.{svg,cur,jpg,jpeg,png,apng,webp,avif,gif,otf,ttf,woff,woff2,json}'
];

module.exports = {
  globDirectory: '../APIViewWeb/wwwroot/spa/browser',
  globPatterns: globPatterns,
  swDest: '../APIViewWeb/wwwroot/spa/sw.js',
  skipWaiting: true,
  clientsClaim: true,
  cleanupOutdatedCaches: true,
  manifestTransforms: [
      async (manifestEntries) => {
        return {
          manifest: manifestEntries.map(entry => ({
            ...entry,
            url: entry.url + `?v=${version}`
          })),
          warnings: [],
        };
      }
    ]
};
