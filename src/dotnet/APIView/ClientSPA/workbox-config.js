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
  globDirectory: '../APIViewWeb/wwwroot/spa',
  globPatterns: globPatterns,
  swDest: '../APIViewWeb/wwwroot/spa/sw.js',
  skipWaiting: true,
  clientsClaim: true,
  cleanupOutdatedCaches: true,
  modifyURLPrefix: {
    '': `?v=${version}`
  }
};