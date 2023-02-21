# `HttpClient` sample unit project

This test project shows a prospective dev how they might convert their tests to consume the test-proxy as a recording/playback solution.

## Run the test-proxy

- `dotnet tool install azure.sdk.tools.testproxy --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json --version 1.0.0-dev*`
- `cd` to this directory.
- Invoke `test-proxy`

## Run the tests

- Run tests in your preferred method, either through `dotnet test` or visual studio test explorer. Both work!

### Recording using Visual Studio for manual debugging

Unfortunately .NET test projects have a bad habit of not honoring environment variable settings from test projects. As a result of this, if the user is debugging these tests from a local visual studio instance.

- Update the RECORD_MODE static in `SampleTestClass.cs` to "record".
- Run tests -> see new recordings

### Recording using `dotnet test`

This one is much easier. 

```powershell
$env:RECORD_MODE="record"
dotnet test .
```
