# Integrating test-proxy in devops

Test-Proxy is best used as an installable dependency that users can ignore after installing for the first time. Obviously the recommended methodology there is to use `docker` to simplify the amount of manual installation you need to complete.

Once `docker` is installed, it is preferred that the test frameworks in each language will take care of spinning up the test-proxy _for you_. This means that whether in cloud build or on your local machine, there is no additional complexity to starting up your tests.

Check out [tools/test-proxy/startup-scripts/](../startup-scripts/) for a few examples in various languages.

## Adding to Your Devops Build

### With Docker

Test-proxy has been shipped within the `eng/common/testproxy` folder for any repo owned by the azure-sdk team.

Important Files:

```
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
- `test-proxy-tool.yml`: This one installs and runs the test-proxy as a dotnet tool.
- `test-proxy-docker.yml`: This yml pulls down the relevant docker image and runs _that_ instead of directly running the tool.

Note that both `.yml` files can be used to start the proxy. It is up to the user to determine which methodology they wish to use. It is recommended that one uses the same method as they would recommend to the team for local testing.
