#!/bin/sh
set -e

commit_sha="da15b6fd3ef856477bf6f4fb29ba1b7af717770d"
archive="vscode-server-linux-x64.tar.gz"

# Download VS Code Server tarball to tmp directory.
curl -L "https://update.code.visualstudio.com/commit:${commit_sha}/server-linux-x64/stable" -o "/tmp/${archive}"

mkdir -vp ~/.vscode-server/bin/"${commit_sha}"

tar --no-same-owner -xzv --strip-components=1 -C ~/.vscode-server/bin/"${commit_sha}" -f "/tmp/${archive}"

sh /root/.vscode-server/bin/${commit_sha}/bin/code-server --install-extension vscjava.vscode-java-pack
sh /root/.vscode-server/bin/${commit_sha}/bin/code-server --install-extension ms-dotnettools.csharp
sh /root/.vscode-server/bin/${commit_sha}/bin/code-server --install-extension ms-python.python
