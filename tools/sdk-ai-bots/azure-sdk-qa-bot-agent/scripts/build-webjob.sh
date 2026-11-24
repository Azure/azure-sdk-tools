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
PYTHON_RELEASE=20261123
PYTHON_VERSION=3.12.16
PYTHON_SHA256=c85811d35971d06612585a057db3b71806378df6ea8476275fb16aa5c9fe56a0
ARCHIVE="cpython-${PYTHON_VERSION}%2B${PYTHON_RELEASE}-x86_64-unknown-linux-gnu-install_only_stripped.tar.gz"
curl --fail --silent --show-error --location --proto '=https' --tlsv1.2 --max-time 300 \
  "https://github.com/astral-sh/python-build-standalone/releases/download/${PYTHON_RELEASE}/${ARCHIVE}" -o "$STAGE/python.tar.gz"
echo "$PYTHON_SHA256  $STAGE/python.tar.gz" | sha256sum --check --status
mkdir -p "$STAGE/python-dist"
tar -xzf "$STAGE/python.tar.gz" -C "$STAGE/python-dist"
RUNTIME="$STAGE/python-dist/python"
export PATH="$RUNTIME/bin:$PATH"
[[ "$("$RUNTIME/bin/python3" --version)" = "Python $PYTHON_VERSION" ]]
mkdir -p "$STAGE/release/runtime" "$STAGE/release/site-packages" "$STAGE/release/scripts"
cp -R "$RUNTIME/bin" "$RUNTIME/lib" "$STAGE/release/runtime/"
"$RUNTIME/bin/python3" -m pip install --pre --no-cache-dir --target "$STAGE/release/site-packages" -r requirements.txt
cp -R config models prompts services tools utils skills "$STAGE/release/"
cp _version.py "$STAGE/release/"
cp scripts/run_feedback_jobs.py "$STAGE/release/scripts/"
cp webjob/run.sh webjob/settings.job "$STAGE/release/"
sed -i 's/\r$//' "$STAGE/release/run.sh"
chmod 755 "$STAGE/release/run.sh" "$STAGE/release/runtime/bin/python3"
(cd "$STAGE/release" && zip -q -r "$STAGE/webjob.zip" .)
unzip -tq "$STAGE/webjob.zip"
mv "$STAGE/webjob.zip" "$OUTPUT"
