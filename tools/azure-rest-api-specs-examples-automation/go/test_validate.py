import unittest
from os import path

from validate import GoVet
from models import GoExample


class TestGoVet(unittest.TestCase):

    def test_example(self):
        code = '''package main
import "fmt"
func main() {
    fmt.Println("hello world")
}
'''

        tmp_path = path.abspath('.')
        go_examples = [GoExample('code', '', code)]
        go_vet = GoVet(tmp_path, 'rsc.io/quote@v1.5.2', '', go_examples)
        result = go_vet.vet()
        self.assertTrue(result.succeeded)

    def test_package_v2(self):
        code = r'''package armcompute_test
import (
	"context"
	"log"
	"github.com/Azure/azure-sdk-for-go/sdk/azcore/to"
	"github.com/Azure/azure-sdk-for-go/sdk/azidentity"
	"github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute/v2"
)
func ExampleDisksClient_BeginRevokeAccess() {
	cred, err := azidentity.NewDefaultAzureCredential(nil)
	if err != nil {
		log.Fatalf("failed to obtain a credential: %v", err)
	}
	ctx := context.Background()
	client, err := armcompute.NewDisksClient("{subscription-id}", cred, nil)
	if err != nil {
		log.Fatalf("failed to create client: %v", err)
	}
	poller, err := client.BeginRevokeAccess(ctx,
		"myResourceGroup",
		"myDisk",
		nil)
	if err != nil {
		log.Fatalf("failed to finish the request: %v", err)
	}
	_, err = poller.PollUntilDone(ctx, nil)
	if err != nil {
		log.Fatalf("failed to pull the result: %v", err)
	}
}
'''

        go_mod = r'''module github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute/v2

go 1.18

require (
	github.com/Azure/azure-sdk-for-go/sdk/azcore v1.0.0
	github.com/Azure/azure-sdk-for-go/sdk/azidentity v1.0.0
	github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute v1.0.0
	github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/internal v1.0.0
	github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/network/armnetwork v1.0.0
	github.com/stretchr/testify v1.7.0
)

require (
	github.com/Azure/azure-sdk-for-go/sdk/internal v1.0.0 // indirect
	github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/resources/armresources v1.0.0 // indirect
	github.com/AzureAD/microsoft-authentication-library-for-go v0.4.0 // indirect
	github.com/davecgh/go-spew v1.1.1 // indirect
	github.com/dnaeon/go-vcr v1.1.0 // indirect
	github.com/golang-jwt/jwt v3.2.1+incompatible // indirect
	github.com/google/uuid v1.1.1 // indirect
	github.com/kylelemons/godebug v1.1.0 // indirect
	github.com/pkg/browser v0.0.0-20210115035449-ce105d075bb4 // indirect
	github.com/pmezard/go-difflib v1.0.0 // indirect
	golang.org/x/crypto v0.0.0-20220511200225-c6db032c6c88 // indirect
	golang.org/x/net v0.0.0-20220425223048-2871e0cb64e4 // indirect
	golang.org/x/sys v0.0.0-20211216021012-1d35b9e2eb4e // indirect
	golang.org/x/text v0.3.7 // indirect
	gopkg.in/yaml.v2 v2.4.0 // indirect
	gopkg.in/yaml.v3 v3.0.0-20210107192922-496545a6307b // indirect
)
'''

        tmp_path = path.abspath('.')
        go_examples = [GoExample('code', '', code)]
        go_vet = GoVet(tmp_path, 'github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/compute/armcompute/v2@v2.0.0',
                       go_mod, go_examples)
        result = go_vet.vet()
        self.assertTrue(result.succeeded)
