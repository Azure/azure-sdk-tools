# bot-configs source tree

Per-env YAML/JSON files uploaded to the shared storage account's `bot-configs`
blob container by `hooks/postprovision.ts` (via
[`hooks/lib/upload-bot-configs.ts`](../hooks/lib/upload-bot-configs.ts)).

## Layout

```
config/
├── dev/
│   ├── channel.yaml
│   └── tenant.yaml
├── preview/
│   └── (add per-env files here)
└── prod/
    └── (add per-env files here)
```

Subdirectories are preserved as blob paths (e.g. `dev/labelingProjects/<id>/analyzer.json`
uploads to `bot-configs/labelingProjects/<id>/analyzer.json`).

## Why per-env

`channel.yaml` embeds the backend/agent web-app `endpoint` URL and the Teams
channel IDs the bot is registered against. Both differ per environment:

- dev endpoint → `azuresdkqabot-dev-serve-agent-*.westus2-01.azurewebsites.net`
- prod endpoint → the prod backend web app
- channel IDs may also differ (dev registrations point at test channels only).

## Adding a new env

1. Create `config/<env>/` and drop the channel/tenant files (start from `config/dev/*.yaml`).
2. Update `endpoint:` in `channel.yaml` to the target env's backend URL.
3. Run `azd provision --environment <env>` (or `az deployment sub create` in CI)
   — postprovision uploads the files after infra is in place.

## Bypass

Set `BOT_CONFIGS_SOURCE_DIR=<abs-path>` before `azd provision` to load from a
different directory, or leave `config/<env>/` empty to skip the upload step
entirely (useful for provisions that only touch infra).
