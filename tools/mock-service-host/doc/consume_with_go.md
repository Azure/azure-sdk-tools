# Consume Mock Service Host With Azure SDK for GO

## Use official SDK

### Install SDK package

Make sure your Go version is 1.16 or above. Azure SDK for GO use [Go modules](https://github.com/golang/go/wiki/Modules) for versioning and dependency management. As an example, to mock with Azure Compute, you would run following command to install `armcompute` module:

```sh
go get github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute
```
You are also recommended to install other packages for authentication and core functionalities :
```sh
go get github.com/Azure/azure-sdk-for-go/sdk/azcore
go get github.com/Azure/azure-sdk-for-go/sdk/azidentity
```
> **_NOTE:_** If your are not familiar with the usage of Azure SDK for GO, you can refer to the [quickstart](https://aka.ms/azsdk/go/mgmt).

### Create mock credential for authorization

As Mock Service Host will accept all the request without authorization, you can create a mock credential for future use.

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

### Create client option for Mock Service Host

Before you create client and send request, you need to create an option to change the enpoint target to Mock Service Host and skip insecure TLS connection verification as the Mock Service Host using a self-signed certificate.

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

### Create client and do request

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

## Generate your own SDK

> **_NOTE:_** The [autorest](https://www.npmjs.com/package/autorest) is required for the following step.

### Generation
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

Add the following code to the `main.go` file to send `Create Extension` request to Mock Service Host.

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

### Testing

Testcases are also generated for the client, you can run the end-to-end test case with Mock Service Host in one command.
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
