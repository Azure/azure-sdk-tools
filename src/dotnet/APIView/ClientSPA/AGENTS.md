# ClientSPA — Agent Guidelines

## Overview

Angular 20 frontend SPA. Builds to `../APIViewWeb/wwwroot/spa` for hosting by the ASP.NET backend.

See [CONTRIBUTING.md](CONTRIBUTING.md) for setup and contribution details.

## Tech Stack

- **Angular 20** with standalone components
- **PrimeNG** for UI components, **Bootstrap** for layout
- **Monaco Editor** via `ngx-monaco-editor-v2` for code display
- **SignalR** (`@microsoft/signalr`) for real-time updates
- **Markdown** rendering with remark/rehype pipeline

## Commands

```bash
npm start              # Dev server with SSL
npm run build          # Production build → ../APIViewWeb/wwwroot/spa
npm run test:unit      # Vitest
npm run test:e2e       # Playwright (see ui-tests/)
npm run test:coverage  # Coverage report
```

## Conventions

- Components, services, models, pipes, guards live under `src/app/`.
- Global styles in `styles.scss` and `ng-prime-overrides.scss`.
- Environment configs in `src/environments/`.
- `.editorconfig` is present — respect its formatting rules.
- E2E tests use Playwright and live in `ui-tests/`. See [ui-tests/README.md](ui-tests/README.md).
- **After making changes, evaluate whether `CONTRIBUTING.md` needs updates** to stay consistent with the code.
- **Also evaluate whether any files in `../docs/` need updates** to reflect architectural or behavioral changes.
