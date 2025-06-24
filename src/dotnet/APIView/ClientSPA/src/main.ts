import 'reflect-metadata';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';

window.addEventListener('error', event => {
  console.error('Global error:', event.error || event.message);
});
window.addEventListener('unhandledrejection', event => {
  console.error('Unhandled promise rejection:', event.reason);
});

platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.error(err));


if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/spa/sw.js');
  });
}
