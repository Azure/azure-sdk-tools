import 'reflect-metadata';
import { platformBrowserDynamic } from '@angular/platform-browser-dynamic';

import { AppModule } from './app/app.module';

(window as any).MonacoEnvironment = {
  getWorkerUrl: (moduleId: string, label: string) => {
      if (label === 'json') {
          return './assets/monaco/esm/vs/language/json/json.worker.js';
      }
      if (label === 'css' || label === 'scss' || label === 'less') {
          return './assets/monaco/esm/vs/language/css/css.worker.js';
      }
      if (label === 'html' || label === 'handlebars' || label === 'razor') {
          return './assets/monaco/esm/vs/language/html/html.worker.js';
      }
      if (label === 'typescript' || label === 'javascript') {
          return './assets/monaco/esm/vs/language/typescript/ts.worker.js';
      }
      return './assets/monaco/esm/vs/editor/editor.worker.js';
  },
};

platformBrowserDynamic().bootstrapModule(AppModule)
  .catch(err => console.error(err));


if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('sw.js');
  });
}
