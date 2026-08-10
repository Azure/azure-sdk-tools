---
applyTo: "**/package-lock.json"
---

# Updating `package-lock.json`

When updating a dependency (for example to resolve a security advisory), keep the
change as small as possible and follow these rules:

## 1. Prefer updating only `package-lock.json`; leave `package.json` untouched

- If the existing semver range in `package.json` already allows the target
  version, update **only** `package-lock.json`. Regenerate it with
  `npm install --package-lock-only` (run from the directory that contains the
  `package.json`/`package-lock.json`).
- Only modify `package.json` (by adding an `overrides` entry or a direct
  dependency) when it is required to keep `package-lock.json` in sync and valid.
- A common case that *requires* a `package.json` change: a parent package pins a
  transitive dependency to an **exact** version. For example, `@angular/build`
  pins `vite`, `undici`, and `piscina` to exact versions, so a lock-only bump is
  reverted on the next `npm install` and fails `npm ci` (e.g.
  `Invalid: lock file's vite@7.3.5 does not satisfy vite@7.3.2`). In that case an
  `overrides` entry is needed to make the new version "stick".

## 2. When an override is needed, override to `^X.Y.Z` (roll-forward), not exact `X.Y.Z`

- Prefer `^X.Y.Z` so the dependency rolls forward to future minor and patch
  releases, rather than pinning the exact `X.Y.Z`.
- Add the override consistent with the existing structure in that
  `package.json`. In `src/dotnet/APIView/ClientSPA`, overrides for packages
  pinned by `@angular/build` are nested under the `@angular/build` key (next to
  the existing `vitest`/`vite` entries), not at the top level.

## 3. Validate the result

- After any change, run `npm ci` in the affected directory to confirm
  `package-lock.json` is consistent with `package.json`. `npm ci` failing with an
  "Invalid: lock file's ... does not satisfy ..." error means an override (or a
  `package.json` change) is still required.
- Verify the intended package resolves to the target version in
  `package-lock.json`. Note that an override does not necessarily remove every
  reference to the old version: a parent's own dependency list may still record
  its original pinned version even though the resolved package was overridden.
  Make PR descriptions accurate about which references remain.
