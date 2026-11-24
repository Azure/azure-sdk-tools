#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
[[ "$(uname -s)" = Linux && "$(uname -m)" = x86_64 ]] || { echo 'Build on Linux x64' >&2; exit 1; }
OUTPUT=${1:?Output ZIP path required}
OUTPUT=$(realpath -m "$OUTPUT")
[[ ! -e "$OUTPUT" ]] || { echo 'Output ZIP already exists' >&2; exit 1; }
mkdir -p "$(dirname "$OUTPUT")"
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
# Pin and authenticate the distribution against the reviewed upstream digest.
NODE_VERSION=24.13.0
NODE_SHA256=e798599612f4bb71333a3397ab0d095fd62214e115aea45aa858a145fc72d67e
curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 --max-time 300 \
  "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz" -o "$STAGE/node.tar.xz"
echo "$NODE_SHA256  $STAGE/node.tar.xz" | sha256sum --check --status
tar -xJf "$STAGE/node.tar.xz" -C "$STAGE"
RUNTIME="$STAGE/node-v${NODE_VERSION}-linux-x64"
export PATH="$RUNTIME/bin:$PATH"
[[ "$(node --version)" = "v$NODE_VERSION" ]]
npm run clean
npm run build
mkdir -p "$STAGE/release/runtime/bin" "$STAGE/release/dist"
cp -R dist/src dist/config "$STAGE/release/dist/"
cp package.json package-lock.json "$STAGE/release/"
cp "$RUNTIME/bin/node" "$STAGE/release/runtime/bin/node"
cp "$RUNTIME/LICENSE" "$STAGE/release/runtime/LICENSE"
# Separate install, never prune or copy the build/test dependency tree or npmrc.
npm ci --omit=dev --ignore-scripts --prefix "$STAGE/release" --no-audit --no-fund
cp webjob/run.sh webjob/settings.job "$STAGE/release/"
sed -i 's/\r$//' "$STAGE/release/run.sh"
chmod 755 "$STAGE/release/run.sh" "$STAGE/release/runtime/bin/node"
# Do not include npm authentication files or unnecessary executable links.
rm -rf "$STAGE/release/node_modules/.bin"
rm -f "$STAGE/release/.npmrc"
(cd "$STAGE/release" && zip -q -r "$STAGE/webjob.zip" .)
unzip -tq "$STAGE/webjob.zip"
mv "$STAGE/webjob.zip" "$OUTPUT"