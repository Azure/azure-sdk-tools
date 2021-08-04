# Azure SDK Tools Test Proxy

For a detailed explanation, check the README.md one level up from this one. This project is intended to act as an out-of-proc record/playback server and is intended to be **non-language-specific**.

## Installation

### Via Local Compile or .NET

1. [Install .Net 5.0](https://dotnet.microsoft.com/download)
2. Install test-proxy

```powershell
> dotnet tool install azure.sdk.tools.testproxy --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk/nuget/v3/index.json
```

This feed is available in [the public azure-sdk project.](https://dev.azure.com/azure-sdk/public/_packaging?_a=feed&feed=azure-sdk)

Note that given there are only dev versions available, please add `--version <selectedVersion>`. You will see a failure that **gives you the latest version of the package** if you do not provide this additional argument.

After successful installation, run the tool:

```powershell
> test-proxy --storage-location <location>
```

### Via Docker Image

Feel free to build the docker file locally within working directory `/tools/test-proxy/docker/`. All required resources are located within. There are additional helpful tips regarding the docker build and install in the [docker readme](../docker/README)

```powershell
> docker build . -t test-proxy
> docker run -v <your-volume-name-or-location>:/etc/testproxy -p 5001:5001 -p 5000:5000 -t test-proxy
```

Or, leverage the azure sdk eng sys container registry.

```powershell
> docker run -v <your-volume-name-or-location>:/etc/testproxy -p 5001:5001 -p 5000:5000 azsdkengsys.azurecr.io/engsys/testproxy:latest
```

Note in both cases you will need to provide the port and volume mapping as arguments.

Within the container, recording outputs are written within the directory `/etc/testproxy`. It is a non-destructive initialize. Any files that exist in this volume locally will only be appended to/updated in place.

## Command line arguments

The test-proxy resolves the its storage location via:

1. Command Line argument `storage-location`
2. Environment variable TEST_PROXY_FOLDER
3. If neither are present, the working directory from which the server is invoked.

It uses the pre-release package [`System.CommandLine.DragonFruit`](https://github.com/dotnet/command-line-api) to parse arguments.

By default, the server will listen on the following port mappings:

| protocol | port |
|-|-|
| http | 5000 |
| https | 5001 |

## How do I use the test-proxy to get a recording?

Use either local or docker image to start the tool. Reference the [Installation](#installation) section of this readme. SSL Configuration is discussed below in [SSL Support](#ssl-support).

A couple notes before running the test-proxy:

- If running the test-proxy directly, ensure that the working directory is set. The default should probably be the root of your sdk-for-X repo. Reference [command line arguments](#command-line-arguments) for options here.
- If running the proxy out of docker, ensure you **map** `etc/testproxy/` to a local folder on your drive. The docker image itself takes advantage of the command-line arguments mentioned in the above bullet.

### Where will my recordings end up?

In the next step, you will be asked to provide a header to `/record/start/` under key `x-recording-file`. The value provided will be consumed by the test-proxy and **used to write your recording to disk**.

For example, let's invoke the test-proxy:

```powershell
test-proxy --storage-location "C:/repo/sdk-for-net/"
```

When we **start** a test run (method outlined in next section), we have to provide a file location via header `x-recording-file`.

When your recording is finalized, it will be stored following the below logic.

```script
root = C:/repo/sdk-for-net/
recording = sdk/tools/test-proxy/tests/testFile.testFunction.cs

final_output_location = C:/repo/sdk-for-net/sdk/tools/test-proxy/tests/testFile.testFunction.cs.json
```

During a `playback` start, the **same** value for header `x-recording-file` should be provided. This allows the test-proxy to load a previous recording into memory.

### Start the test run

Before each individual test runs, a `recordingId` must be retrieved from the test-proxy by POST-ing to the Proxy Server.

```json
URL: https://localhost:5001/record/start
headers {
    "x-recording-file": "<path-to-test>/recordings/<testfile>.<testname>"
}
```

You will receive a recordingId in the reponse under header `x-recording-id`. This value should be included under header `x-recording-id` in all further requests.

### Run your tests

The implicit assumption about this proxy is that you as a dev have _some way_ to reroute your existing requests (with some header additions) to the test-proxy.

The implementation is language specific, but what you want to do is:

1. Prevent each request originating from the test test from hitting their original endpoint.
2. Make the following changes to every outgoing request
    1. Place original request scheme + hostname in header `x-recording-upstream-base-uri`
       - Example header setting: `x-recording-upstream-base-uri: "https://fakeazsdktestaccount.table.core.windows.net"`
    2. Replace request <scheme:hostname> with Proxy Server <scheme:hostname>. (currently https://localhost:5001 or http://localhost:5000)
       - Example transformation: `https://fakeazsdktestaccount.table.core.windows.net/Tables` -> `http://localhost:5001/Tables`.
    3. Add header `"x-recording-id": <x-recording-id>` from startup step
    4. Add header `"x-recording-mode": "record"`
3. As each request hits the test proxy (due to the fact the target hostname has been updated), the test-proxy will invoke the requests and store their results prior to returning to the test code.
   - The request/response results are saved into memory based on the header `x-recording-id`. Forgetting this means your recordings will always be empty!
   - Recordings are saved after STOPPING your test.

A [custom transport](https://github.com/Azure/azure-sdk-tools/blob/main/tools/test-proxy/sample-clients/net/storage-blob/Program.cs#L155) or [request policy](https://github.com/Azure/azure-sdk-for-python/blob/main/tools/azure-devtools/src/azure_devtools/perfstress_tests/_policies.py#L11) are both examples of implementing the above logic.

### When finished running test

After your test has finished and there are no additional requests to be recorded.

POST to the proxy server:

```json
URL: https://localhost:5001/record/stop
headers {
    "x-recording-id": "<x-recording-id>",
}
```

This will **finalize** your recording by:

- Removing from active sessions.
- Applying session/recording sanitizers.
- Saving to disk.

## How do I use the test-proxy to play a recording back?

### Start playback

Extremely similar to recording start.

POST to the proxy server:

```json
URL: https://localhost:5001/playback/start
headers {
    "x-recording-file": "<path-to-test>/recordings/<testfile>.<testname>"
}
```

As with `/record/start`, check response header `x-recording-id` to get the recording-id for usage later on.

### During your test run

The configuration here is **practically identical** to what we lay out in [Run your tests](#run-your-tests). The only difference is that  `x-recording-mode` header should be set to `playback`.

### Stop playback

This really only allows the server to free up a few bits, but:

POST to the proxy server:

```json
URL: https://localhost:5001/playback/stop
headers {
    "x-recording-id": "<x-recording-id>"
}
```

### An important note about perf testing

If a user does **not** provide a `fileId` via header `x-recording-file`, the recording will be saved **in-memory only**. If a recording is saved into memory, the only way to retrieve it is to access the playback by passing along the original recordingId that you **recorded it with**.

Start the recording **without a `x-recording-file` header**.

```json
URL: https://localhost:5001/record/start
```

The POST will return recordingId `X`.

To load this recording for playback...

```json
URL: https://localhost:5001/playback/start
headers {
    "x-recording-id": "X"
}
```

### See example implementations

Of course, feel free to check any of the [examples](https://github.com/Azure/azure-sdk-tools/tree/feature/http-recording-server/tools/test-proxy/sample-clients) to see actual test code and invocations.

Additionally, Nick Guerrera [Prototyped a JS example](https://github.com/nguerrera/azure-sdk-for-js/tree/oop-hack) as well.

## Session and Test Level Transforms, Sanitiziers, and Matchers

A `sanitizer` is used to remove sensitive information prior to storage. When a request comes in during `playback` mode, the same set of `sanitizers` are applied prior to matching with the recordings.

`Matchers` are used to retrieve a `RecordEntry` from a `RecordSession`. As of now, only a single matcher can be used when retrieving an entry during playback.

Default sets of `matcher`, `transforms`, and `sanitizers` are applied during recording and playback. These default settings are all set at the `session` level. Customization is allowed for these default sets by accessing the `Admin` controller.

**When creating a custom sanitizer/matcher/transform, a single constructor must be provided.**

This is due to the fact that if there are **arguments** to the constructor, the body attributes will be mapped **by name.**

### Add Sanitizer

Add a simple Uri Sanitizer that leverages lookahead to ensure it's not overly aggressive:

```json
POST
url: <proxyURL>/Admin/AddSanitizer
headers: {
    "x-abstraction-identifier": "UriRegexSanitizer"
}
body: {
    "value": "fakeaccount",
    "regex": "[a-z]+(?=\\.(?:table|blob|queue)\\.core\\.windows\\.net)"
}
```

Add a more expansive Header sanitizer that uses a target group instead of filtering by lookahead:

```json
POST
url: <proxyURL>/Admin/AddSanitizer
headers: {
    "x-abstraction-identifier": "HeaderRegexSanitizer"
}
body: {
    "key": "Location",
    "value": "fakeaccount",
    "regex": "https\\:\\/\\/(?<account>[a-z]+)\\.(?:table|blob|queue)\\.core\\.windows\\.net",
    "groupForReplace": "account"
}
```

### Apply Matcher

```json
POST
url: <proxyURL>/Admin/SetMatcher
headers: {
    
}
bodyBODY: {

}
```

### For Sanitizers, Matchers, or Transforms in general

When invoked as basic requests to the `Admin` controller, these settings will be applied to **all** further requests and responses. Both `Playback` and `Recording`. Where applicable.

- `sanitizers` are applied before the recording is saved to disk AND when an incoming request is matched.
- A custom `matcher` can be set for a session or individual recording and is applied when retrieving an entry from a loaded recording.
- `transforms` are applied when returning a request during playback.

Currently, the configured set of transforms/playback/sanitizers are NOT propogated onto disk alongside the recording.

### Viewing Available/Active Sanitizers, Matchers, and Transforms

Launch the test-proxy through your chosen method, then visit:

- `<proxyUrl>/Info/Available` to see all available
- `<proxyUrl>/Info/Active` to see all currently active.

Note that the `constructor arguments` that are documented must be present (where documented as such) in the body of the POST sent to the Admin Interface.

## Testing

This project uses `xunit` as the test framework. This is the most popular .NET test solution [according to this twitter poll](https://twitter.com/shahedC/status/1131337874903896065?ref_src=twsrc%5Etfw%7Ctwcamp%5Etweetembed%7Ctwterm%5E1131337874903896065%7Ctwgr%5E%7Ctwcon%5Es1_c10&ref_url=https%3A%2F%2Fwakeupandcode.com%2Funit-testing-in-asp-net-core%2F) and it's also what the majority of the test projects in the `Azure/azure-sdk-tools` repo utilize as well.

## SSL Support

The test-proxy server supports SSL, but due to its local-hosted nature, SSL validation will always be an issue without some manual intervention. Real SSL certificates are not available for localhost! As a result of this, if attempting to leverage SSL when recording, some additional setup will be required.

Within this repository there is a single certificate.

* `dev_certificate/dotnet-devcert.pfx`: generated on a `Ubuntu` distribution using `openssl`.

Unfortunately, the `dotnet dev-certs` generated certificates are _not_ acceptable to a standard ubuntu distro. The issue is that the `KeyUsage` field in the `.crt` [MUST contain](https://github.com/dotnet/aspnetcore/issues/7246#issuecomment-541165030) the `keyCertSign` flag. Certificates generated by `dotnet dev-certs` do NOT have this flag. This means that if you're on Windows AND running the Ubuntu docker image, you will need to trust the `dotnet-devcert.pfx` locally prior to `docker run`.

For further details on importing and using the provided dev-certificates, please investigate the [additional docker readme.](../docker/README.md)

### On Mac and Windows, .NET can be used to generate a local certificate

There are two options here, generate your own SSL Cert, or import an existing one.
#### Option 1

Invoke the command:

```powershell
> dotnet dev-certs https --trust
```

This will be automatically retrieved if you run the nuget installed version of the tool. You may optionally use `openssl` [like so](https://raw.githubusercontent.com/BorisWilhelms/create-dotnet-devcert/f3b5da6f9107834eb31ea5ba7c0583e14cda6b31/create-dotnet-devcert.sh) to generate a certificate. Note that this shell script creates a dev cert that is compatible with ubuntu.

#### Option 2

Import the appropriate already existing certificate within the `tools/test-proxy/docker/dev_certificate` folder.

### Docker Image + SSL

To connect to the docker on SSL, both the docker image and your local machine must trust the same SSL cert. The docker image already has `dotnet-devcert.pfx` imported and trusted, so all that is necessary is to trust that same cert prior to `docker run`.

In the future, passing in a custom cert via a bound volume that contains your certificate will be a possibility as well.

For additional reading on this process for trusting SSL certs locally, feel free to read up [here.](https://devblogs.microsoft.com/aspnet/configuring-https-in-asp-net-core-across-different-platforms/) The afore-mentioned [docker specific readme](../docker/README.md) also has details that are relevant.