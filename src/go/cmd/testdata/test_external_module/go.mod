module test_external_module

go 1.18

require github.com/Azure/azure-sdk-for-go/sdk/azcore v0.1.0

replace github.com/Azure/azure-sdk-for-go/sdk/azcore => ./azcore
