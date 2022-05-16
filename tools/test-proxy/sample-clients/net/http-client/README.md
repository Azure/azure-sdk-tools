# Test Proxy Integration Example - Standard .NET HTTP Client

This readme show basic record and playback of a request against the Azure SDK Test Proxy. This example utilizes the base .NET HTTP Client.

Invocation Steps:

- Install the test-proxy
  - `dotnet tool install azure.sdk.tools.testproxy --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_pac
kaging/azure-sdk-for-net/nuget/v3/index.json --version 1.0.0-dev*`
- Run the test proxy `test-proxy`
- Open the [sln](./Azure.Sdk.Tools.TestProxy.HttpClientSample.sln) in a compatible Visual Studio. It has been tested in VS2019/VS2022.
- Run the application
