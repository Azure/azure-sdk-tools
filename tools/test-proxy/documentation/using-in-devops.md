# Integrating test-proxy in devops

Test-Proxy is best used as an installable dependency that users can ignore after installing for the first time. Obviously the recommended methodology there is to use `docker` to simplify the amount of manual installation you need to complete.

Once `docker` is installed, it is preferred that the test frameworks in each language will take care of spinning up the test-proxy _for you_. This means that whether in cloud build or on your local machine, there is no additional complexity to starting up your tests.

Check out [tools/test-proxy/startup-scripts/](../startup-scripts/) for a few examples in various languages.

## Adding to Your Devops Build

### With Docker

Docker is obviously the preferred methodology, as there are less moving parts that can be affected by the devops hosts.

There are a couple options here. Discussed above are `start-up scripts` that can be referenced to make your _test framework_ spin up the appropriate docker container for testing. However, if that is not possible, one can manually do the same thing with a couple devops tasks. Something like the following...

```yml
  - pwsh: |
      choco install docker-desktop --version=2.1.0.3
    displayName: 'Install docker desktop'

  - pwsh: |
      docker run `
      --detach `
      -v $(Build.SourcesDirectory):/etc/testproxy `
      -p 5001:5001 `
      -p 5000:5000 `
      azsdkengsys.azurecr.io/engsys/testproxy:latest
    displayName: 'Run the docker container'
```

### Without Docker

If `docker` is not an option for your unit tests, there will need to be a manual installation and invocation of the test-proxy. It should be noted, that the below, while working on Linux and Windows, can be extremely finicky. Use with caution.

Something along the lines of...

```yml
  - pwsh: |
      dotnet tool install `
        azure.sdk.tools.testproxy `
        --tool-path $(Build.BinariesDirectory)/test-proxy `
        --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json `
        --version <version>
    displayName: "Install TestProxy"

  - pwsh: |
      if ($IsWindows) {
        Start-Process $(Build.BinariesDirectory)/test-proxy/test-proxy.exe -ArgumentList "--storage-location '$(Build.SourcesDirectory)'" -NoNewWindow -PassThru
      }
      else {
        $(Build.BinariesDirectory)/test-proxy/test-proxy --storage-location "$(Build.SourcesDirectory)" &
      }

      <start your tests>
    displayName: "Run Tests"
```

The key factor at play is that on linux/mac machines, one cannot leverage `Start-Process` like if you're on a Windows host. This is due to the fact that the terminal _owns_ any processes that are started within it. In DevOps, each step _is its own process_. This entirely precludes any simple `fire and forget` options. The recommended methodology is to use the `nohup` (No Hang Up) command to ensure that the test-proxy continues running after the fact.
