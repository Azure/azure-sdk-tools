<!-- cspell:words postprovision -->

# Bot Configuration Source

Per-env YAML/JSON files uploaded to the shared storage account's `bot-configs`
blob container by `hooks/postprovision.ts` (via
[`hooks/lib/upload-bot-configs.ts`](../hooks/lib/upload-bot-configs.ts)).

The agent-server reads `channel.yaml`, caches it for five minutes by default,
and exposes the authenticated `/config/channel` endpoint. The Logic App uses
that endpoint to resolve the tenant for each Teams channel.

## Layout

```text
config/
├── dev/
│   ├── channel.yaml
│   └── tenant.yaml
├── preview/
│   ├── channel.yaml
│   └── tenant.yaml
└── prod/
    ├── channel.yaml
    └── tenant.yaml
```

Subdirectories are preserved as blob paths (e.g. `dev/labelingProjects/<id>/analyzer.json`
uploads to `bot-configs/labelingProjects/<id>/analyzer.json`).

## Why per-env

`channel.yaml` contains Teams channel IDs and a `${SERVER_BASE_URL}` placeholder.
The postprovision hook expands that placeholder from the selected environment's
Bicep outputs before upload. Missing placeholders fail provisioning rather than
uploading an invalid configuration.

## Adding a new env

1. Add the environment to `infra/environments/environment-suite.yaml`.
2. Create `config/<env>/channel.yaml` and `tenant.yaml` from an existing
    environment.
3. Keep `${SERVER_BASE_URL}` in `channel.yaml`; do not copy another
    environment's resolved URL.
4. Make channel and tenant IDs match the environment-suite values.
5. Run `npm run validate-env-suite -- --environment <env>` from `deployment/`.
6. Run the normal `azd provision` path; postprovision uploads the files after
    infrastructure outputs are available.

## Bypass

Set `BOT_CONFIGS_SOURCE_DIR=<abs-path>` before `azd provision` to use a different
source root for an operator-controlled run. `npm run validate-env-suite`
requires `channel.yaml` and `tenant.yaml` for each selected environment. The
upload helper treats a missing or empty source directory as a no-op when it is
invoked independently of that validated provisioning path.
