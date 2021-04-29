# Azure SDK Tools Test Proxy

For a detailed explanation, check the README.md one level up from this one. This project is intended to act as an out-of-proc record/playback server and is intended to be **non-language-specific**.

## Installation
1. [Install .Net Core 3.1](https://dotnet.microsoft.com/download)

2. Install test-proxy
```
> dotnet tool install azure.sdk.tools.testproxy --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk/nuget/v3/index.json
```

Note that given there are only dev versions available, please add `--version <selectedVersion>`.

Otherwise, nuget will kick back with a "these are the versions available". Please choose the latest one.

### Before Invoking, install a dev certificate

Install a developer HTTPS Cert. If you installed .NET in the previous step, invoke this command.

```
> dotnet dev-certs https --trust
```

## Command line arguments

The test-proxy resolves the its storage location via:

1. Command Line argument `storage-location`
2. Environment variable TEST_PROXY_FOLDER
3. If neither are present, the working directory from which the server is invoked.

It uses:

* the pre-release package [`System.CommandLine.DragonFruit`](https://github.com/dotnet/command-line-api) to parse arguments.
* the package [`libgit2sharp`](https://github.com/libgit2/libgit2sharp/) to handle git integration.

## How do I use the test-proxy to get a recording?

Run the test-proxy locally. Leverage the command line arguments above to set the working directory.

```
> test-proxy --storage-location <your target folder>
```

### Start the test run

POST to the Proxy Server. Pass in your "test-id". This should be the path to your testfile + test name.
```
URL: https://localhost:5001/record/start
headers {
    "x-recording-file": "<path-to-test>.<testname>"
}
```
You will receive a test-id in the reponse, look for header `x-recording-id`.

### Before any outgoing request during your test run

1. Prevent outgoing request from hitting original URL
2. Make the following changes to the outgoing request
    1. Place original Request URL in header "x-recording-upstream-base-uri"
    2. Replace Request URL with Proxy Server URL. (currently https://localhost:5001. This will probably change as SSL updates happen)
    3. Add header "x-recording-id": <x-recording-id> from startup step
    4. Add header "x-recording-mode": "record"

### When finished running test

After your test has finished and there are no additional requests to be recorded.

POST to the proxy server:

```
URL: https://localhost:5001/record/stop
headers {
    "x-recording-id": <x-recording-id>,
    "x-recording-save": true
}

```

## How do I use the test-proxy to play a recording back?

### Start playback 

Extremely similar to recording start.

POST to the proxy server:

```
URL: https://localhost:5001/playback/start
headers {
    "x-recording-file": "<path-to-test>.<testname>"
}
```

Check response header `x-recording-id` to get the recording-id.

### During your test run

1. Prevent outgoing request from hitting original URL
2. Make the following changes to the outgoing request
    1. Place original Request URL in header "x-recording-upstream-base-uri"
    2. Replace Request URL with Proxy Server URL. (currently https://localhost:5001. This will change as SSL updates happen)
    3. Add header "x-recording-id": <x-recording-id> from startup step
    4. Add header "x-recording-mode": "playback"

### Stop playback

This really only allows the server to free up a few bits, but:

POST to the proxy server:

```
URL: https://localhost:5001/playback/stop
headers {
    "x-recording-id": "<x-recording-id>"
}
```

### See example implementations

Of course, feel free to check any of the [examples](https://github.com/Azure/azure-sdk-tools/tree/feature/http-recording-server/tools/test-proxy/sample-clients) to see actual test code and invocations.

Additionally, Nick Guerrera [Prototyped a JS example](https://github.com/nguerrera/azure-sdk-for-js/tree/oop-hack) as well.


## Session and Test Level Transforms, Sanitiziers, and Matchers

A `sanitizer` is used to remove sensitive information prior to storage. When a request comes in during `playback` mode, the same set of `sanitizers` are applied prior to matching with the recordings.

`Matchers` are used to retrieve a `RecordEntry` from a `RecordSession`. As of now, only a single matcher can be used when retrieving an entry during playback.

Default sets of `matcher`, `transforms`, and `sanitizers` are applied during recording and playback. These default settings are all set at the `session` level. Customization is allowed for these default sets by accessing the `Admin` controller.

<example1 of sanitizer>

<example2 of transform update>

<example of matcher update>

When invoked as basic requests to the `Admin` controller, these settings will be applied to **all** further requests and responses. Both `Playback` and `Recording`.

However, it is also possible to set these at the individual recording level, prior to sending any requests. To do this, invoke the same controllers with a header that states the individual recordingId.

<example of matcher update at testid level> 

<example of transform update at testid level>

<example of sanitizer update at testid level>


* `sanitizers` can be set at an individual level during both a `record` and `playback` session.
* `matchers` can be set at for a `playback` session. 
* `transforms` can be set for a `playback` session.

Currently, these settings are NOT propogated onto disk. That may change in the near future.

## Testing

This project uses `xunit` as the test framework. This is the most popular .NET test solution [according to this twitter poll](https://twitter.com/shahedC/status/1131337874903896065?ref_src=twsrc%5Etfw%7Ctwcamp%5Etweetembed%7Ctwterm%5E1131337874903896065%7Ctwgr%5E%7Ctwcon%5Es1_c10&ref_url=https%3A%2F%2Fwakeupandcode.com%2Funit-testing-in-asp-net-core%2F) and it's also what the majority of the test projects in the `Azure/azure-sdk-tools` repo utilize as well.
