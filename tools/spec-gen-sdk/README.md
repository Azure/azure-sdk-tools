# Spec-gen-sdk

Spec-gen-sdk coordinates SDK generation from a local specification repository and
a local SDK repository, invoking the language automation configured in the SDK
repository's `swagger_to_sdk_config.json`.

## Generate input contract

The configured `generateScript` receives paths to `generateInput.json` and
`generateOutput.json`. The input contract is defined by
[`GenerateInputSchema.json`](https://github.com/Azure/azure-sdk-tools/blob/main/tools/spec-gen-sdk/src/types/GenerateInputSchema.json).

Callers can optionally pass `--typespec-emitter <package-name>` to spec-gen-sdk.
The value is forwarded unchanged in `generateInput.json` as `typespecEmitter`:

```json
{
  "typespecEmitter": "@azure-tools/typespec-ts-provisioning"
}
```

This is an excerpt, not a complete generate input. The package name is an example;
there is no default emitter. When the option is omitted, the serialized input
omits `typespecEmitter`, preserving existing language automation behavior.
Programmatic callers can supply the same optional field in `SdkAutoOptions`.

When a non-empty emitter is selected, spec-gen-sdk requires that emitter to have
an enabled (truthy) entry under `options` (or legacy `emitters` when `options` is
absent) in `tspconfig.yaml` and map to the requested SDK repository in the
specification repository configuration. Otherwise, it logs a configuration
warning and returns `notEnabled` without invoking language automation. An
explicit emitter request requires `tspConfigPath` and does not fall back to
OpenAPI generation through `readmePath`. Omitted or empty emitter selections
preserve the existing repository-level enablement and fallback behavior.

The field does not install the emitter or modify `tspconfig.yaml`. Language
automation must support the field to use it; provisioning pipeline configuration
and language-specific generation support are separate changes.

## Development

Use Node.js 22.13.0 or later. From this directory, run `npm ci` to install
dependencies, `npx tsc --noEmit` to type-check, `npm run lint` to lint, and
`npm test` to run the tests.
