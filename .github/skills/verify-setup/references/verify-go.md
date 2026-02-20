# Go SDK Requirements

- Note that `goimport -h` will exit 1 even if it exists, opt to check for executable.

## Required Checks

| Requirement | Check Command | Min Version | Purpose | Auto Install | Installation Instructions |
|-------------|---------------|-------------|---------|--------------|--------------------------|
| Go | `go version` | - | Go compiler | false | **Linux:** `sudo snap install go --classic`<br>**Windows/macOS:** Download and install the latest Go version from https://go.dev/dl/ |
| goimports | `goimports -h` | - | Code formatting tool | true | `go install golang.org/x/tools/cmd/goimports@latest` |
| golangci-lint | `golangci-lint --version` | - | Linter | false | https://golangci-lint.run/docs/welcome/install/ |
| generator | `generator -v` | 0.4.3 | SDK generator tool | true | `go install github.com/Azure/azure-sdk-for-go/eng/tools/generator@latest` |
