# Azure SDK Test-Proxy Contribution guide

Within this folder are all the components that make up the the tool `Azure.Sdk.Tools.TestProxy`, henceforth referred to as the `test-proxy`.

This product is currently developed on Visual Studio 2022, but CLI build/test also work fine. Currently, this project only supports .NET 6.0, though .NET 7.0 support is coming.

To contribute a change:

- Fork.
- Create a branch on your fork with your change.
- Locally test your change, add a unit test, commit.
- Submit a PR to this repo from your forked branch!
  - Relevant azure-sdk devs will review your PR.

## Directory Guide

| Folder | Purpose |
|---|---|
| `Azure.Sdk.Tools.TestProxy/` | The actual recording server. Standard `csproj`, minimal external dependencies, packed as .NET tool.  |
| `Azure.Sdk.Tools.TestProxy.Tests/` | The tests for the recording server. Tests that reach out to external resources are assigned trait `Integration`. |
| `docker/` | The dockerfiles (linux/windows) that are currently produced for the test-proxy. Heading towards retirement now that standalone executables are ready. |
| `documentation/` | As labelled :) |
| `sample-clients/` | Various sample usages of the `test-proxy` categorized by language. These are intended to help users who need additional context to the readme. |
| `scripts/` | Script directory containing various scripts for example or one-off use. The most important of which is `scripts/test-scripts/CLIIntegration.Tests.ps1`. This file honors the name, and calls `test-proxy` as an external tool to verify functionality. |
| `swagger/` | A swagger file defining the interface of the test-proxy for external tools. |

## Tool structure

At its core, the project is a simple `ASP.NET` server with a CLI tool bolted on.

A `route` is a path on the server that responds to requests. EG "/Info/Available" translates to `http://localhost:5000/Info/Available` when hosting the proxy on http port 5000.

```
Azure.Sdk.Tools.TestProxy/
  <namespace folders>
  Admin.cs ----------------> Provides routes /Admin/AddSanitizer, /Admin/AddTransform, /Admin/SetMatcher, /Admin/SetRecordingOptions
  Info.cs  ----------------> /Info/Available, /Info/Active
  Playback.cs -------------> /Playback/Start, /Playback/Stop
  Record.cs ---------------> /Record/Start, /Record/Stop
  RecordingHandler.cs -----> Core implementation of record/playback functionality. All the actual logic for handling of recordings.
  Startup.cs --------------> Startup configuration, CLI operation access, and generic redirect handling.
```

The namespace folders are named for their purpose, with all general-use additions going under the `Common` namespace.

### How the client requests are generically handled

The most important "gotcha" about how this server works is to be aware of how the proxy handles the routing for the requests flowing to it. If the route does not match one of the routes defined for the other controllers, it falls back to a more generic `HandleRequest` function in `Record` or `Playback` controllers.

Normal flow:

- Client POSTS to /Record/Playback, gets a `recording-id` back.
- Client begins sending redirected requests from their test run. They have injected 2 additional headers to every request going to the proxy:
  - `"x-record-mode": "record"`
  - `"x-recording-id": "<recording-id>"`
- Test Proxy redirects incoming request regardless of the route "magically"
- Client POSTS to /Record/Stop, recording is written to disk.

This is configured in `Startup.cs` function `MapRecording`.

```cs
foreach (var controller in new[] { "playback", "record" })
{
    app.MapWhen(
        context =>
            controller.Equals(
                GetRecordingMode(context),
                StringComparison.OrdinalIgnoreCase),
        app =>
        {
            app.UseRouting();
            app.UseEndpoints(
                endpoints => endpoints.MapFallbackToController(
                    "{*path}", "HandleRequest", controller));
        });
}
```

Effectively this means:

> If the recording mode is record, send this to the `HandleRequest` function in the `Record` controller. Same is true for `playback` and `Playback` controller.

The `x-recording-id` header is then utilized to associate the request/response pair with the appropriate recording!

## Testing

As mentioned in the table before, the tests for this project reside within `Azure.Sdk.Tools.TestProxy.Tests`. When contributing a change, please:

1. Run the unit tests locally to ensure nothing has been affected.
2. Verify your change with at least one additional unit test. More is better!

### Note about integration tests

The tests marked with trait `Integration` are gated on access to a private github repo that is used for the purpose. Feel free to replace the target repo `Azure/azure-sdk-assets-integration` with your _own_ repo, as that will absolutely work. However that could be quite painful with the number of tests.

## Gotchas

When opening the project in VS2022, you may see the following:

![image](https://user-images.githubusercontent.com/45376673/218187142-040881c7-2dfa-4f9f-84c6-c2058d7c878c.png)


This is due to the fact that the `test-proxy` is part of a greater tooling repo, and must align with a pinned .NET version. To resolve this issue easily, please do the following.

```bash
dotnet --list-sdks
3.1.426 [C:\Program Files\dotnet\sdk]
5.0.415 [C:\Program Files\dotnet\sdk]
6.0.113 [C:\Program Files\dotnet\sdk]
6.0.308 [C:\Program Files\dotnet\sdk]
6.0.401 [C:\Program Files\dotnet\sdk]
6.0.405 [C:\Program Files\dotnet\sdk]
7.0.101 [C:\Program Files\dotnet\sdk]
```

Find a version starting with a `6`, if you don't have one on your system, [install it!](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

Once you have that, find the [`global.json`](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) at root of this repo.

```json
{
  "msbuild-sdks": {
    "Microsoft.Build.Traversal": "3.2.0"
  },
  "sdk": {
    "version": "7.0.102",
    "rollForward": "feature"
  }
}
```

Change the `version` to one that shows up in the list when you call `dotnet --list-sdks`.
