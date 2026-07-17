# npm installs in Azure SDK repos must use the internal CFS feed

When you run `npm install` or `npm ci` for Azure SDK repositories on a Microsoft-managed device, use the internal CFS-backed npm feed instead of the public npm registry.

## Recommended command

For `azure-rest-api-specs`, run:

```bash
npm ci --registry=https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-js/npm/registry/
```

The same registry override also applies to other npm-based installs in Azure SDK repos:

```bash
npm install --registry=https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-js/npm/registry/
```

## When this workaround is needed

If `azure-rest-api-specs` dependency restore fails because packagefeedproxy quarantined a package that was published recently, use the internal CFS feed command above. This is the current repo-specific workaround when the public npm path is blocked or when first-party packages have not yet cleared quarantine.

Longer term, Microsoft Security is updating packagefeedproxy so first-party packages can flow without the same 7-day quarantine delay.

## Why this is required

Microsoft is rolling out two automatic protections on company-managed devices:

1. npm client minimum release age: `min-release-age=7`. `npm install` skips package versions published in the last 7 days and resolves to the newest version that is at least 7 days old. Versions already pinned in a committed `package-lock.json` are not affected.
2. Network protection blocks direct access to `registry.npmjs.org`, `registry.yarnpkg.com`, and `registry.npmmirror.com`, so npm, pnpm, and yarn cannot fetch packages from those public registries directly.

Routing installs through the internal CFS feed ensures packages are Microsoft-published, have already passed the 7-day quarantine window, or were manually vetted before use.

## Security rationale

This guidance is meant to reduce npm supply-chain risk. Many malicious packages are detected within days of publication, so a short quarantine window filters out a large class of attacks before developers consume them. Recent examples include the chalk/debug maintainer phishing campaign, the Shai-Hulud worm, and the Miasma compromise of `@redhat-cloud-services`.

## Source of truth

The source-of-truth feed URL is:

`https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-js/npm/registry/`
