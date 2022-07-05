# How to Use @autorest/gotest

For Azure SDK for Go, we introduced `@autorest/gotest`, an autorest extension to auto generate the examples, tests and samples code for management modules. In this document, we will give you detailed instructions about tool's usage.

## Prerequisites

- [Go 1.18+](https://go.dev/dl/) is required because we leverage Go generic in our SDK.
- Add your default `GOPATH/bin` (default is `~/go/bin`) to your system `PATH` environment.
- Prepare local workspace of [Azure SDK for Go](https://github.com/Azure/azure-sdk-for-go) repository.  (We will use `<sdk-repo-workspace>` for SDK workspace in this document.)
- Prepare local workspace of service swagger files. (We will use a test signalR [swagger](https://github.com/Azure/azure-sdk-tools/tree/main/tools/sdk-testgen/swagger/specification/signalr/resource-manager) as example in this document. The workspace will be `<swagger-repo-workspace>`.)
- [Node 1.14+](https://nodejs.org/en/download/) is required and [Autorest CLI](https://github.com/Azure/autorest/tree/main/packages/apps/autorest) should be installed globally.

## Config swagger

We use `autorest.md` file under SDK module folder root (`<sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/autorest.md`) to config the service swagger file to be used for code generation. Change the file content as follows.

````md
### AutoRest Configuration

> see https://aka.ms/autorest

```yaml
azure-arm: true
require:
- <swagger-repo-workspace>/tools/sdk-testgen/swagger/specification/signalr/resource-manager/readme.md
- <swagger-repo-workspace>/tools/sdk-testgen/swagger/specification/signalr/resource-manager/readme.go.md
license-header: MICROSOFT_MIT_NO_VERSION
module-version: 0.5.0
```
````


## Generate SDK

After you set the swagger config, you need to regenerate the SDK first. (You could use `--generate-sdk` to generate SDK along with example/test/sample generation to skip this step.)

1. Delete all `.go` files under the module folder.
```sh
rm <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/*.go
```
2. Use `@autorest/go` to generate SDK.
```sh
autorest --version=3.8.2 --use=@autorest/go@latest --go --track2 --output-folder=<sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr --file-prefix="zz_generated_" --clear-output-folder=false <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/autorest.md
```
3. Resolve dependency.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go mod tidy
```

## Generate examples

We use [swagger defined examples](https://github.com/Azure/azure-rest-api-specs/blob/main/document/x-ms-examples.md) to generate example code for each operation. All the examples released with the SDK modules will be displayed in `pkg.go.dev` following the operation's API reference.

1. Use `@autorest/gotest` to generate the example code. If you want to generate the SDK code as well, add `--generate-sdk` tag.
```sh
autorest --version=3.8.2 --use=@autorest/go@latest --use=@autorest/gotest@latest --go --track2 --output-folder=<sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr --clear-output-folder=false --go.clear-output-folder=false --testmodeler.generate-sdk-example <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/autorest.md
```
2. Resolve dependency.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go mod tidy
```

## Generate mock test and execute

We use [swagger defined examples](https://github.com/Azure/azure-rest-api-specs/blob/main/document/x-ms-examples.md) to generate mock tests. These tests can be executed with [mock service host](https://github.com/Azure/azure-sdk-tools/tree/main/tools/mock-service-host) to ensure the correctness of SDK.

1.  Use `@autorest/gotest` to generate the mock tests. If you want to generate the SDK code as well, add `--generate-sdk` tag.
```sh
autorest --version=3.8.2 --use=@autorest/go@latest --use=@autorest/gotest@latest --go --track2 --output-folder=<sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr --clear-output-folder=false --go.clear-output-folder=false --testmodeler.generate-mock-test <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/autorest.md
```
2. Resolve dependency.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go mod tidy
```
3. Create workspace for mock service host and install pacakge. You could reference the [readme](https://github.com/Azure/azure-sdk-tools/tree/main/tools/mock-service-host#readme) of mock service host for further usage.
```sh
mkdir <mock-service-host-workspace> && cd <mock-service-host-workspace>
npm install @azure-tools/mock-service-host
```
4. Set mock service host config to use the same swagger with test generation.
```sh
cd <mock-service-host-workspace>
echo "specRetrievalGitUrl=https://github.com/Azure/azure-sdk-tools
validationPathsPattern=tools/sdk-testgen/swagger/specification/signalr/resource-manager/*/**/*.json" > .env
```
5. Start mock service host.
```sh
cd <mock-service-host-workspace>
node node_modules/@azure-tools/mock-service-host/dist/src/main.js
```
6. Execute mock test and gather coverage result.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go test -v -coverprofile coverage.txt
```

## Generate scenario test and execute

We use [API scenarios](https://github.com/Azure/azure-rest-api-specs/tree/main/document/api-scenario) to generate scenario tests. These tests will use live traffic to ensure the consistency between SDK and service. We could use [test proxy](https://github.com/Azure/azure-sdk-tools/tree/main/tools/test-proxy) to record the live traffic and playback later without charging.

1. Use `@autorest/gotest` to generate the scenario tests. If you want to generate the SDK code as well, add `--generate-sdk` tag.
```sh
autorest --version=3.8.2 --use=@autorest/go@latest --use=@autorest/gotest@latest --go --track2 --output-folder=<sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr --clear-output-folder=false --go.clear-output-folder=false --testmodeler.generate-scenario-test <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/autorest.md
```
2. Resolve dependency.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go mod tidy
```
3. Start test proxy.
```sh
cd <sdk-repo-workspace>/eng/common/testproxy
pwsh docker-start-proxy.ps1 start
```
4. Set credential environment variables.
- AZURE_CLIENT_ID
- AZURE_CLIENT_SECRET
- AZURE_TENANT_ID
- AZURE_SUBSCRIPTION_ID
5. Set environment variable `AZURE_RECORD_MODE` to `record` to let test proxy to record traffic and execute live test.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go test -v
```
6. Set environment variable `AZURE_RECORD_MODE` to `playback` to test with the recording result.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr
go test -v
```

## Generate samples

We use [API scenarios](https://github.com/Azure/azure-rest-api-specs/tree/main/document/api-scenario) to generate samples code. These samples will be committed to our [Azure SDK sample repo](https://github.com/azure-samples/azure-sdk-for-go-samples) for user reference.

1.  Use `@autorest/gotest` to generate the samples. One scenario file will generate one sample. Each sample will be a new module in a separate folder and can be executable directly.
```sh
autorest --version=3.8.2 --use=@autorest/go@latest --use=@autorest/gotest@latest --go --track2 --output-folder=<sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr --clear-output-folder=false --go.clear-output-folder=false --testmodeler.generate-sdk-sample <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/autorest.md
```
2. Resolve dependency.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/signalr
go mod tidy
```
3. Execute the main function with proper credential environment variables.
```sh
cd <sdk-repo-workspace>/sdk/resourcemanager/signalr/armsignalr/signalr
go mod tidy
go run main.go
```

## Other generation config

For further configuration of `@autorest/gotest`, you could reference the [readme](https://github.com/Azure/azure-sdk-tools/tree/main/tools/sdk-testgen/packages/autorest.gotest#readme).