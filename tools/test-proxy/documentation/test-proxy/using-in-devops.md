# Integrating test-proxy in devops

Test-Proxy is best used as an installable dependency that users can ignore after installing for the first time. Obviously the recommended methodology there is to use `docker` to simplify the amount of manual installation you need to complete.

Once `docker` is installed, it is preferred that the test frameworks in each language will take care of spinning up the test-proxy _for you_. This means that whether in cloud build or on your local machine, there is no additional complexity to starting up your tests.

Check out [tools/test-proxy/startup-scripts/](../startup-scripts/) for a few examples in various languages.

## Adding to Your Devops Build

Test-proxy has been shipped within the `eng/common/testproxy` folder for any repo owned by the azure-sdk team. All boilerplate/integration code is present within.

Important Files:

```text
eng/common/testproxy/
  dotnet-devcert.crt
  dotnet-devcert.pfx
  docker-start-proxy.ps1
  test-proxy-tool.yml
  test-proxy-docker.yml
```

A couple notes about the above:

- `dotnet-devcert.crt` is NOT DER encoded. This means if you need a `pem` file, you can just rename it. No binary to deal with.
- `dotnet-devcert.pfx` can be imported with password `password` (this fact is also highlighted in [trusting-cert-per-language.md](trusting-cert-per-language.md))
- `docker-start-proxy.ps1`: This can also be used locally to start and stop a singular instance of the test-proxy docker image.
- `test-proxy-tool.yml`: This template installs and runs the test-proxy as a dotnet tool.
- `test-proxy-docker.yml`: This template pulls down the relevant docker image and runs _that_ instead of directly running the tool.

Note that both `.yml` files can be used to start the proxy. It is up to the user to determine which methodology they wish to use. It is recommended that one uses the same method as they would recommend to the team for local testing.

Examples of Proxy Invocations in a few languages:

- [JS](https://github.com/Azure/azure-sdk-for-js/blob/main/eng/pipelines/templates/steps/test.yml#L30)
- [Python](https://github.com/Azure/azure-sdk-for-python/blob/main/eng/pipelines/templates/steps/build-test.yml#L42)
- [Go](https://github.com/Azure/azure-sdk-for-go/blob/main/eng/pipelines/templates/steps/build-test.yml#L47)

Also note that azure devops does NOT support `docker` on Mac agents. This means that installing and running the .NET tool using `test-proxy-tool.yml` is essential if recorded tests are expected to run on Mac agents.

## SSL Support In Devops

When running on DevOps, it is unlikely that the certificate will be trusted appropriately to ensure SSL during playback. Both the yml files provided in `eng/common/testproxy/` are intended to easily support this however.

To add this to your appropriate repository, open `Language-Settings.ps1` (wherever it may be in your repo) and add a function of the form:

```powershell
function Import-Dev-Cert-<lowercase-language>
{
  Write-Host "Certificate action here!"
  ...
}
```

If you're a bit confused as to what is meant by `<lowercase-language>`, refer to the variable value of `$Language` within the `Language-Settings.ps1`. Here's an example [from .NET](https://github.com/Azure/azure-sdk-for-net/blob/912d936723967bb4943437ab8bf284737b312ce8/eng/scripts/Language-Settings.ps1#L1). Here's another example [from Java.](https://github.com/Azure/azure-sdk-for-java/blob/main/eng/scripts/Language-Settings.ps1#L1)

A bunch of the "common" functions are defined and called this way.

This function will be _automatically picked up_ during all your builds once it's merged! How? Check in [eng/common/scripts/trust-proxy-certificate.ps1](../../../eng/common/scripts/trust-proxy-certificate.ps1) if you wish to understand.

## Error Investigations

When diagnosing failures that "only occur in CI" or on platforms where there is no way to get an actual debugging session, leverage the `debug` logging level. To take advantage of this in CI, create a build variable when queuing your build.

![image](https://user-images.githubusercontent.com/45376673/153307363-521d271c-980e-425b-876a-212fb1e5e7a3.png)

- Name: `Logging__LogLevel__Default`
- Value: `Debug` (or any value from [the .NET LogLevel Enum](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.loglevel?view=dotnet-plat-ext-6.0))

![image](https://user-images.githubusercontent.com/45376673/153307105-28ce55eb-73f6-47d7-b181-eaf189b3bfdc.png)
