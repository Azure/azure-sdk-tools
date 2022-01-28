# What's the Mock Service Host

The Mock Service Host works as a mock server for ARM FrontDoor Service. It supports all management plane APIs that are defined in the azure-rest-api-specs repo (or any subset of it). It could be invoked from different types of clients including AzureCLI/Terraform/SDKs/Postman, etc. and it has the below capabilities:

1. Verify whether the incoming request meets the Swagger definition.
2. Generate a response based on Swagger and return it to the client.
3. Generate Swagger examples based on the request and response.

![overall.png](doc/pic/overall.png)

# The Behaviour of Mock Service Host

The mock service could be run in your local environment. After started, it will listen to the following endpoints by default:

-   https://0.0.0.0:8441, stateful.
-   http://0.0.0.0:8442, stateless http.
-   https://0.0.0.0:8443, stateless https.
-   https://0.0.0.0:8445, always return 500 to simulate service internal error (except resourcegroup operations).

Since currently the HTTPS certificate is created with domain localhost, so the HTTPS endpoints can only be visited though "localhost".

> **_NOTE:_** If these listening ports are already been used by other apps in your local environment, change the default ports according to [the Configuration](#configuration).

## What's stateful/stateless

Stateful:

-   Client could only call GET/DELETE after CREATE succeeded, otherwise it will get the response code of 404.
-   If cascadeEnabled is true:
    -   The sub resource could only be created if parent resource already exists. For instance: the following resources should be created one after another: resourceGroup-->virtual network-->subnet.
    -   Deleting a parent resource will also delete it's all descendants.
-   If cascadeEnabled is false:
    -   Can create sub resource even if parent does not exist.
    -   Deleting a parent resource will not delete it's children.

Stateless:

-   Mock Service Host will return a valid mocked response for each valid call of GET/List.

# Getting Started

## Prerequisites

- [Nodejs](https://nodejs.org/en/download/) 14.15 or above

## Start Mock Service Host

### Clone this project

Use git to clone this repo to your local workspace.

```shell
# cd MY_WORKSPACE
# git clone git@github.com:Azure/azure-sdk-tools.git
# cd MY_WORKSPACE/tools/mock-service-host
```

### Configure mock-service-host running environment to use your local swagger

The Azrue Rest Api Spec repo is a companion repo of mock-service-host. By default, the mock-service-host downloads [the public azure swagger repo](https://github.com/Azure/azure-rest-api-specs) to the cache folder and loads the target Resource provider based on configuration. You can ask it to load all RPs by updating configs in file '.env'.

```
+-- mock-service-host/
|   +-- cache/                   // swagger repos will be downloaded here in the first start-up of mock-service-host
|   +-- .env                     // you can create this file, and add configs in it.
```

Set target swagger files as bellow in .env:

```
specRetrievalGitUrl=https://github.com/Azure/azure-rest-api-specs
specRetrievalGitBranch=main
specRetrievalGitCommitID=6023d2b16a66b70c7a870a003c2e3f6750eacd70
validationPathsPattern=specification/*/resource-manager/*/**/*.json
```

Change specRetrievalGitUrl and specRetrievalGitBranch if you are not using the public swagger main branch. Change specRetrievalGitCommitID if you are not using the branch head. And you can specify a filter rule to enable only your own RP's json files to accelerate the loading speed of mock-service-host. For instance:

```
validationPathsPattern=specification/mediaservices/resource-manager/Microsoft.Media/**/*.json
```

### [Optional] Download \*.pem files for self-signed certificate.
The SSL certificates are required when launching the mock-service-host. This step download the default *.pem certificate files from Azure KeyVault.

```shell
# az login ...      // login azure cli with your credential
# . initiate.sh   // for LINUX or mac
```

or

```bat
> initiate.ps1    // for Windows, need to run in powershell
```

If failed for permission reasons, please contact [CodeGen Core team vsccodegen@microsoft.com](vsccodegen@microsoft.com) for authentication on keyvault used in the scripts. Or you may create self-signed key&cert by your self, the required files can be found in [src/webserver/httpServerConstructor.ts](./src/webserver/httpServerConstructor.ts)

> **_NOTE:_** If you don't do this step, the mock-service-host will try to create new certificates when lauching. The need to ensure [OpenSSL Toolkit](https://www.openssl.org/) has been installed in your environment.

### Start Mock Service Host

```
# npm install && npm run start
```

Common trouble shootings for starting the web server:

-   Make sure all ports used in Mock Service Host haven't been used by other processes.
-   Try to use sudo/"run as administrator" if failed to start listening.

It takes up to two minutes to load all swagger files in the azure-rest-api-specs repo after the mock server started. When loading finished, a log with "validator initialized" is shown in the console.

## Consume Mock Service Host

After the mock server is started, this section describes how to consume the mock server with different client tool/lib.
### Using Azure SDK for GO

#### Install SDK package

Make sure your Go version is 1.16 or above. Azure SDK for GO use [Go modules](https://github.com/golang/go/wiki/Modules) for versioning and dependency management. As an example, to mock with Azure Compute, you would run following command to install `armcompute` module:

```sh
go get github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute
```
You are also recommended to install other packages for authentication and core functionalities :
```sh
go get github.com/Azure/azure-sdk-for-go/sdk/azcore
go get github.com/Azure/azure-sdk-for-go/sdk/azidentity
```
> **_NOTE:_** If your are not familiar with the usage of Azure SDK for GO, you can refer to the [quickstart](https://github.com/Azure/azure-sdk-for-go/blob/main/documentation/new-version-quickstart.md).

#### Create fake credential for authorization

As mock server will accept all the request without authorization, you can create a fake credential for future use.

```go
import (
	"context"
	"time"

	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/policy"
)

type MockCredential struct {
}

func (c *MockCredential) GetToken(ctx context.Context, opts policy.TokenRequestOptions) (*azcore.AccessToken, error) {
	return &azcore.AccessToken{Token: "MockToken", ExpiresOn: time.Now().Add(time.Hour * 24).UTC()}, nil
}
```

#### Create client option for Mock Service Host

Before you create client and send request, you need to create an option to change the enpoint to the Mock Server Host and skip insecure TLS connection verification as the Mock Service Host using a self-signed certificate.

```go
import (
	"fmt"

	"net/http"

	"github.com/Azure/azure-sdk-for-go/sdk/azcore/arm"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/policy"
	"golang.org/x/net/http2"
)

tr := &http.Transport{}
if err := http2.ConfigureTransport(tr); err != nil {
  fmt.Printf("Failed to configure http2 transport: %v", err)
}
tr.TLSClientConfig.InsecureSkipVerify = true
httpClient := &http.Client{Transport: tr}
options := arm.ClientOptions{
  ClientOptions: policy.ClientOptions{
    Transport: httpClient,
  },
  Endpoint: arm.Endpoint("https://localhost:8443"),
}
```

#### Create client and do request

Now you can send the mock request with different APIs provided by SDK. Below is an example to list virtual machines by location.

```go
import (
  "context"
  "encoding/json"
  "fmt"

	"github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute"
)

cred := &MockCredential{}
ctx := context.Background()
client := armcompute.NewVirtualMachinesClient("00000000-0000-0000-0000-000000000000", cred, &options)
pager := client.ListByLocation("eastus", nil)
for {
  nextResult := pager.NextPage(ctx)
  if err := pager.Err(); err != nil {
    fmt.Fatalf("failed to advance page: %v", err)
    break
  }
  if !nextResult {
    break
  }
  for _, v := range pager.PageResponse().Value {
    if v, err := json.Marshal(v); err == nil {
      fmt.Printf("Pager result: %s\n", v)
    }
  }
}
```

After composing all the above code and execution after build, you will receive the following result if everything works well.

```shell
Pager result:ID - /subscriptions/00000000-0000-0000-0000-000000000000/providers/Microsoft.Compute/locations/eastus/virtualMachines/mockName
Location - aaaaaaaaaaaaaaaaaaaaaaaaaaa
Name - mockName
Type - Microsoft.Compute/locations/virtualMachines
```
### Using Azure CLI
#### Install Azure CLI

Follow the [guide](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) to install Azure CLI.
#### Activate the Python environment

Run `'az --version'` to get which python environment is used in your computer, for instance:
![az-version.png](doc/pic/az-version.png)

According to above output, you know the CLI is using Python virtual environment c:\ZZ\projects\codegen\venv_mock, saying it's YOUR_VENV.
Then you need to activate this venv:

```
# . <YOUR_VENV>/bin/activate       // for LINUX/mac
or
> <YOUR_VENV>\Scripts\activate    // for Windows
```

> **_NOTE:_** If your Azure CLI is installed with no python virtual environment (a system Python or a CLI embeded Python), don't need to activate any VENV, but make sure the following step will be executed with that Python folder.

#### Trust the certificate of Mock Service Host in Python environment

The Mock Service Host is using a self-signed certificate which will be appended into the file **cacert.pem** in your Python environment.

```shell
 cat .ssh/localhost-ca.crt >> <YOUR_VENV>/lib/python3.8/site-packages/certifi/cacert.pem       // for LINUX/mac
```

or

```bat
type .ssh\localhost-ca.crt >> <YOUR_VENV>\Lib\site-packages\certifi\cacert.pem                // for Windows
```

#### Configure Azure CLI to use Mock Service Host

```shell
# az login --server-principal --username <USERNAME> --password <PASSWORD> --tenant <TENANT> // login with any realworld credential
# az cloud register -n virtualCloud
                    --endpoint-resource-manager "https://localhost:8443"                    // connect to stateless endpoint
                    --endpoint-active-directory https://login.microsoftonline.com
                    --endpoint-active-directory-graph-resource-id https://graph.windows.net/
                    --endpoint-active-directory-resource-id https://management.core.windows.net/
# az cloud set -n virtualCloud
```

#### Request with Azure CLI

Now you can try any Azure CLI command, the setup is done if mocked response is received for the below command.

```shell
# az network vnet peering create --allow-vnet-access --name MyVnet1ToMyVnet2 --remote-vnet MyVnet2Id --resource-group MyResourceGroup --vnet-name MyVnet1

{
  "allowForwardedTraffic": true,
  "allowGatewayTransit": true,
  "allowVirtualNetworkAccess": true,
  "etag": "aaaaaaaaaaaaaaaaaaa",
  "id": "aaaa",
  "name": "MyVnet1ToMyVnet2",
  "peeringState": "Initiated",
  "provisioningState": "Succeeded",
  "remoteAddressSpace": {
    "addressPrefixes": [
      "aaaaaaaaaaaaaaaaaaaaa"
    ]
  },
  "remoteBgpCommunities": {
    "regionalCommunity": "aaaaaaaaaaaaaaaaaaaaaa",
    "virtualNetworkCommunity": "aaaaaaaaaaaaa"
  },
  "remoteVirtualNetwork": {
    "id": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
  },
  "type": "aaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "useRemoteGateways": true
}
```

## Generate your own client

> **_NOTE:_** The [autorest](https://www.npmjs.com/package/autorest) is required for the following step.
### Using Azure SDK for GO
#### Generation
You can use [Autorest.GO](https://github.com/Azure/autorest.go) to generate GO client lib with your own swagger files. Key steps are:

```diff
# autorest --version=3.7.3
           --use=@autorest/go@4.0.0-preview.36 
           --use=@autorest/gotest@1.3.0 
           --go 
           --track2 
           --output-folder=<output-folder> 
           --file-prefix="zz_generated_" 
           --clear-output-folder=false 
           --go.clear-output-folder=false 
+          --testmodeler.generate-mock-test // remember to add this option if want to run tests
           --generate-sdk=true 
           --azure-arm=true 
           --module-version=0.1.0 
           --module-name=<module-name>
           --module=github.com/<organization-name>/<repo-name>/<module-name>
           <path-to-the-swagger-readme.md>
# cd <output-folder>
# go mod tidy && go build && go vet         // resolve dependencies and build
```
Now you can try to use the generated lib for mock request.

- Example: agrifood

Below is sample steps for geneartion and mock usage for RP [agrifood](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/agrifood/resource-manager):

```bat
> autorest --version=3.7.3 
           --use=@autorest/go@4.0.0-preview.36 
           --use=@autorest/gotest@1.3.0 
           --go 
           --track2 
           --output-folder=./mock-test-lib/agrifood
           --file-prefix="zz_generated_" 
           --clear-output-folder=false 
           --go.clear-output-folder=false 
           --testmodeler.generate-mock-test
           --generate-sdk=true 
           --azure-arm=true 
           --module-version=0.1.0 
           --module-name=agrifood 
           --module=agrifood 
           ./azure-rest-api-specs/specification/agrifood/resource-manager/readme.md
# cd ./mock-test-lib/agrifood        // go to the output folder
# go mod tidy && go build && go vet  // resolve dependencies, build and install
# mkdir main && cd main              // create main package for test
```

Add the following code to the `main.go` file to send `Create Extension` request to Mock Server Host.

```go
package main

import (
	"context"
	"fmt"
	"time"

	"net/http"

	"agrifood"

	"github.com/Azure/azure-sdk-for-go/sdk/azcore"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/arm"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/policy"
	"golang.org/x/net/http2"
)

type MockCredential struct {
}

func (c *MockCredential) GetToken(ctx context.Context, opts policy.TokenRequestOptions) (*azcore.AccessToken, error) {
	return &azcore.AccessToken{Token: "MockToken", ExpiresOn: time.Now().Add(time.Hour * 24).UTC()}, nil
}

func main() {
	tr := &http.Transport{}
	if err := http2.ConfigureTransport(tr); err != nil {
		fmt.Printf("Failed to configure http2 transport: %v", err)
	}
	tr.TLSClientConfig.InsecureSkipVerify = true
	httpClient := &http.Client{Transport: tr}
	options := arm.ClientOptions{
		ClientOptions: policy.ClientOptions{
			Transport: httpClient,
		},
		Endpoint: arm.Endpoint("https://localhost:8443"),
	}

	cred := &MockCredential{}
	ctx := context.Background()
	client := agrifood.NewExtensionsClient("11111111-2222-3333-4444-555555555555", cred, &options)
	_, err := client.Create(ctx,
		"provider.extension",
		"examples-farmbeatsResourceName",
		"examples-rg",
		nil)
	if err != nil {
		fmt.Printf("Failed to get result: %v", err)
	} else {
		fmt.Printf("Extension Created:\nID - %s\nName - %s", *extension.ID, *extension.Name)
	}
}
```

Build and excute to get the mock result.

```shell
# go build
# ./main
Extension Created:
ID - /subscriptions/11111111-2222-3333-4444-555555555555/resourceGroups/examples-rg/providers/Microsoft.AgFoodPlatform/farmBeats/examples-farmbeatsResourceName/extensions/provider.extension
Name - provider.extension
```

#### Testing

Testcases are also generated for the client, you can run the end-to-end test case with Mock server in one command.
The test file is located at `<output-folder>\zz_generated_mock_test.go`

- Example: agrifood
Taking RP agrifood as example, the test can be run with below command:

```shell
# go test -v
=== RUN   TestExtensions_Create
--- PASS: TestExtensions_Create (0.03s)
=== RUN   TestExtensions_Get
--- PASS: TestExtensions_Get (0.01s)
=== RUN   TestExtensions_Update
--- PASS: TestExtensions_Update (0.01s)
=== RUN   TestExtensions_Delete
--- PASS: TestExtensions_Delete (0.01s)
=== RUN   TestExtensions_ListByFarmBeats
    zt_generated_mock_test.go:235: Page end.
--- PASS: TestExtensions_ListByFarmBeats (0.06s)
=== RUN   TestFarmBeatsExtensions_List
    zt_generated_mock_test.go:490: Page end.
--- PASS: TestFarmBeatsExtensions_List (0.04s)
=== RUN   TestFarmBeatsExtensions_Get
--- PASS: TestFarmBeatsExtensions_Get (0.05s)
=== RUN   TestFarmBeatsModels_Get
--- PASS: TestFarmBeatsModels_Get (0.02s)
=== RUN   TestFarmBeatsModels_CreateOrUpdate
--- PASS: TestFarmBeatsModels_CreateOrUpdate (0.02s)
=== RUN   TestFarmBeatsModels_Update
--- PASS: TestFarmBeatsModels_Update (0.01s)
=== RUN   TestFarmBeatsModels_Delete
--- PASS: TestFarmBeatsModels_Delete (0.01s)
=== RUN   TestFarmBeatsModels_ListBySubscription
    zt_generated_mock_test.go:978: Page end.
--- PASS: TestFarmBeatsModels_ListBySubscription (0.01s)
=== RUN   TestFarmBeatsModels_ListByResourceGroup
    zt_generated_mock_test.go:1037: Page end.
--- PASS: TestFarmBeatsModels_ListByResourceGroup (0.01s)
=== RUN   TestLocations_CheckNameAvailability
--- PASS: TestLocations_CheckNameAvailability (0.02s)
=== RUN   TestOperations_List
    zt_generated_mock_test.go:1214: Page end.
--- PASS: TestOperations_List (0.02s)
PASS
ok      agrifood        0.778s
```

### Using Azure CLI
#### Generation
Following the [Autorest.Az Guide](https://github.com/Azure/autorest.az#how-to-use-azure-cli-code-generator) you can generate CLI extension with your own swagger files. Key steps are:

```diff
# autorest  --az
            --use=https://trenton.blob.core.windows.net/trenton/autorest-az-1.7.3.tgz
            <path-to-the-swagger-readme.md>
            --azure-cli-extension-folder=<output-folder>
+           --gen-cmdlet-test               // remember to add this option if want to run tests
# cd <output-folder>/<extension-folder>
# python setup.py sdist bdist_wheel         // generate wheel-file (*.whl) in "dist" folder
# az extension add --source=<path-to-the-wheel-file>
```

Now you can try to look through and run your extended Azure CLI command.

```shell
# az <extension-name>  --help               // check generated comand groups
# az <extension-name> <group-name> --help  // check commands in the group
# az <extension-name> <group-name> <create|list|show|delete...> --help  // check detail command information
# az <extension-name> <group-name> <create|list|show|delete...> <--params...>  // run your command with Virtual Server
```
- Example: guestconfiguration

Below is sample steps for generate CLI extension for RP [guestconfiguration](https://github.com/Azure/azure-rest-api-specs/tree/main/specification/guestconfiguration/resource-manager):

```bat
> autorest  --az
            --use=https://trenton.blob.core.windows.net/trenton/autorest-az-1.7.3.tgz
            ..\azure-rest-api-specs\specification\guestconfiguration\resource-manager\readme.md
            --azure-cli-extension-folder=..\generated
            --gen-cmdlet-test
# cd ..\generated                     // go to the output folder
# cd src\guestconfig                  // go to the generated extension folder
# python setup.py sdist bdist_wheel
# az extension remove guestconfig     // remove it first since it's an existing extension
# az extension add --source=.\dist\guestconfig-0.1.0-py3-none-any.whl

# az guestconfig --help               // check information for the guestconfig extension.
...
```

> **_NOTE:_** The variable <extension-name> can be found in readme.az.md. For guestconfiguration, it's ["extensions: guestconfig"](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/guestconfiguration/resource-manager/readme.az.md#az)

#### Testing

Testcases are also generated for the CLI extension, you can run the end-to-end test case with Mock server in one command.
The test file is located at `<output-folder>\src\<extension-name>\azext_<extension-name>\tests\cmdlet\test_positive.py`
- Example: guestconfiguration
Taking RP guestconfiguration as example, the test can be run with below command:

```bat
> pip install pytest        // make sure pytest is installed
> pytest -rA <output-folder>\src\guestconfig\azext_guestconfig\tests\cmdlet\test_positive.py
...
========================= short test summary info ==========================
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_list
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_report_list
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_report_show
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_assignment_show
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp2
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp_assignment_list
PASSED ..\azure-cli-extensions\src\guestconfig\...::test_guest_configuration_hcrp_assignment_show
========================= 8 passed, 3 warnings in 36.24s =========================
```

# Configuration

You can create a file .env to customize the configurations used at runtime. The file .env should be located at current working directory, for instance:

```
+-- mock-service-host
|   +-- .env                 // configuration files
```

## Customize mock-service-host listen ports

Bellow options in .env are available to configure the server to listen specific local TCP ports.

```
httpsPortStateful=5001
httpPortStateless=5002
httpsPortStateless=5003
internalErrorPort=5004
```

## Consume a remote swagger repo

You can use below configures to configure the server to load and run against a remote swagger repo:

```
specRetrievalMethod=git
specRetrievalLocalRelativePath=./cache
specRetrievalGitUrl=https://github.com/Azure/azure-rest-api-specs-pr
specRetrievalGitBranch=main
```

## Consume a local swagger repo

Instead of download swagger repo from git remotely, you can configure the mock server to load your local swagger files:

```
specRetrievalMethod=filesystem
specRetrievalLocalRelativePath=../azure-rest-api-specs
validationPathsPattern=specification/*/resource-manager/*/**/*.json
```

## Configure example generation folder

With below configuration, the REST calling request and mocked response can be preserved in local swagger repo.

```
enableExampleGeneration=true
```

The example files are generated in the **mock** sub-folder relative to the swagger directory, for example:

-   [specRetrievalLocalRelativePath]\specification\apimanagement\resource-manager\Microsoft.ApiManagement\preview\2018-01-01\mock
    You can update exampleGenerationFolder in file .env to use another folder, for instance:

```
exampleGenerationFolder=examples
```

## Configure cascadeEnabled

The stateful endpoint has different behaviour on Create/Delete operations depending on value of **cascadeEnabled**.
The cascadeEnabled is false by default, you can enable it in file .env like below:

```
cascadeEnabled=true
```

