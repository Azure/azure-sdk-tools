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

## See example implementations

Of course, feel free to check any of the [examples](https://github.com/Azure/azure-sdk-tools/tree/feature/http-recording-server/tools/test-proxy/sample-clients) to see actual test code and invocations.

Additionally, Nick Guerrera [Prototyped a JS example](https://github.com/nguerrera/azure-sdk-for-js/tree/oop-hack) as well.