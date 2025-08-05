# Azure Template Package client library for Go

[Describe the service, and what kinds of features it provides - this should be a short paragraph]

Use the (package) client module `github.com/Azure/azure-sdk-for-go/sdk/(package path)` in your application to:

(give a bullet point list of activities you can do with this client)

Key links:

[Source code][source] | [API reference documentation][docs] | [Product documentation][product_docs] | [Samples][go_samples]

## Getting started

### Prerequisites

- [Supported](https://aka.ms/azsdk/go/supported-versions) version of Go - [Install Go](https://go.dev/doc/install)
- Azure subscription - [Create a free account](https://azure.microsoft.com/free/)
- (Include examples on how to deploy this resource using the Azure CLI, and via the Azure Portal)

### Install the package

Install the Azure (TODO: package name goes here) client module for Go with `go get`:

```
go get github.com/Azure/azure-sdk-for-go/sdk/(package path)
```

# Key concepts

(Bullet point list of your library's main concepts.)

# Examples

Examples for using this library can be found with the module documentation at https://pkg.go.dev/github.com/Azure/azure-sdk-for-go/sdk/(package path)#pkg-examples

# Troubleshooting

### Logging

This module uses the classification-based logging implementation in `azcore`. To enable console logging for all SDK modules, set `AZURE_SDK_GO_LOGGING` to `all`. Use the `azcore/log` package to control log event output or to enable logs for `azidentity` only. For example:
```go
import azlog "github.com/Azure/azure-sdk-for-go/sdk/azcore/log"

// print log output to stdout
azlog.SetListener(func(event azlog.Event, s string) {
    fmt.Println(s)
})

// include only azidentity credential logs
azlog.SetEvents(azidentity.EventAuthentication)
```

# Next steps

More sample code should go here, along with links out to the appropriate example tests.

## Contributing

For details on contributing to this repository, see the [contributing guide][azure_sdk_for_go_contributing].

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

### Additional Helpful Links for Contributors  

Many people all over the world have helped make this project better.  You'll want to check out:

* [What are some good first issues for new contributors to the repo?](https://github.com/azure/azure-sdk-for-go/issues?q=is%3Aopen+is%3Aissue+label%3A%22up+for+grabs%22)
* [How to build and test your change][azure_sdk_for_go_contributing_developer_guide]
* [How you can make a change happen!][azure_sdk_for_go_contributing_pull_requests]
* Frequently Asked Questions (FAQ) and Conceptual Topics in the detailed [Azure SDK for Go wiki](https://github.com/azure/azure-sdk-for-go/wiki).

<!-- ### Community-->
### Reporting security issues and security bugs

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) <secure@microsoft.com>. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://www.microsoft.com/msrc/faqs-report-an-issue).

### License

Azure SDK for Go is licensed under the [MIT](https://github.com/Azure/azure-sdk-for-go/blob/main/sdk/template/aztemplate/LICENSE.txt) license.

<!-- LINKS -->
[azure_sdk_for_go_contributing]: https://github.com/Azure/azure-sdk-for-go/blob/main/CONTRIBUTING.md
[azure_sdk_for_go_contributing_developer_guide]: https://github.com/Azure/azure-sdk-for-go/blob/main/CONTRIBUTING.md#developer-guide
[azure_sdk_for_go_contributing_pull_requests]: https://github.com/Azure/azure-sdk-for-go/blob/main/CONTRIBUTING.md#pull-requests
[azure_cli]: https://learn.microsoft.com/cli/azure
[azure_pattern_circuit_breaker]: https://learn.microsoft.com/azure/architecture/patterns/circuit-breaker
[azure_pattern_retry]: https://learn.microsoft.com/azure/architecture/patterns/retry
[azure_portal]: https://portal.azure.com
[azure_sub]: https://azure.microsoft.com/free/
[cloud_shell]: https://learn.microsoft.com/azure/cloud-shell/overview
[cloud_shell_bash]: https://shell.azure.com/bash
[source]: https://github.com/Azure/azure-sdk-for-go/tree/main/sdk/(package path)
[docs]: https://pkg.go.dev/github.com/Azure/azure-sdk-for-go/tree/main/sdk/(package path)
[product_docs]: (service URL)
[go_samples]: https://pkg.go.dev/github.com/Azure/azure-sdk-for-go/sdk/(package path)#pkg-examples
