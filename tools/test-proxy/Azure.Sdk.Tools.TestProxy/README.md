# Azure SDK Tools Test Proxy

For a detailed explanation and more-or-less spec, check the [README.md](../README.md) one level up from this one.

This test proxy is intended to provide out-of-process record/playback capabilities compatible with any language. It offers session and recording level customization during both `record` and `playback` of a test.

All that is required to start recording is to make minor updates to the requests made within a given test. A standard request looks something like this:

![request_changes](https://user-images.githubusercontent.com/45376673/131716856-5a89eba5-bdb8-45a4-9195-164de01aa35a.png)

Modified to be recordable by the test proxy, it should look like this:

![request_changes_after](https://user-images.githubusercontent.com/45376673/131716970-dee28516-cb45-4589-abce-6d2aa6bec93d.png)

There is a walkthrough through the process below in the [how do I use the test proxy to get a recording.](#how-do-i-use-the-test-proxy-to-get-a-recording)

## Installation

### Via Local Compile or .NET

1. [Install .Net 5.0](https://dotnet.microsoft.com/download)
2. Install test-proxy

```powershell
> dotnet tool install azure.sdk.tools.testproxy --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json --version 1.0.0-dev*
```

This feed is available in [the public azure-sdk project.](https://dev.azure.com/azure-sdk/public/_packaging?_a=feed&feed=azure-sdk)

After successful installation, run the tool:

```powershell
> test-proxy --storage-location <location>
```

If you've already installed the tool, you can always check the installed version by invoking:

```powershell
> test-proxy --version
```

### Via Docker Image

The Azure SDK Team maintains a public Azure Container Registry.

```powershell
> docker run -v <your-volume-name-or-location>:/srv/testproxy/ -p 5001:5001 -p 5000:5000 azsdkengsys.azurecr.io/engsys/testproxy-lin:latest
```

For example, to save test recordings to disk in your repo's `/sdk/<service>/tests/recordings` directory, provide the path to the root of the repo:

```powershell
> docker run -v C:\\repo\\azure-sdk-for-<language>:/srv/testproxy/ -p 5001:5001 -p 5000:5000 azsdkengsys.azurecr.io/engsys/testproxy-lin:latest
```

Note the **port and volume mapping** as arguments! Any files that exist in this volume locally will only be appended to/updated in place. It is a non-destructive initialize.

Within the container, recording outputs are written within the directory `/srv/testproxy/`.

#### A note about docker caching

The azure-sdk team regularly update the image associated with the `latest` tag. Combined with the fact that docker will aggressively cache if possible, it is very possible that developers' local machines may be running outdated versions of the test-proxy.

To ensure that your local copy is up to date, run:

```powershell
> docker pull azsdkengsys.azurecr.io/engsys/testproxy-lin:latest
```

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

## Environment Variables

The test-proxy is integrated with the following environment variables.

| Variable | Usage |
|---|---|
| `TEST_PROXY_FOLDER` | if command-line argument `storage-location` is not provided when invoking the proxy, this environment variable is also checked for a valid directory to use as test-proxy context. |
| `Logging__LogLevel__Microsoft` | Defaults to `Information`. Possible valid values are `Information`, `Warning`, `Error`, `Critical`.  |

Both of the above variables can be set in the `docker` runtime by providing additional arguments EG: `docker run -e Logging__LogLevel__Microsoft=Warning azsdkengsys.azurecr.io/engsys/testproxy-lin:latest`. For multiple environment variables, just use multiple `-e` provisions.

## How do I use the test-proxy to get a recording?

Use either local or docker image to start the tool. Reference the [Installation](#installation) section of this readme. SSL Configuration is discussed below in [SSL Support](#ssl-support).

A couple notes before running the test-proxy:

- If running the test-proxy directly, ensure that the working directory is set. The default should probably be the root of your sdk-for-X repo. Reference [command line arguments](#command-line-arguments) for options here.
- If running the proxy out of docker, ensure you **map** `etc/testproxy/` to a local folder on your drive. The docker image itself takes advantage of the command-line arguments mentioned in the above bullet.

### Where will my recordings end up?

In the next step, you will be asked to provide a JSON body within your POST to `/record/start/`. This body should be a JSON object with a top-level key `x-recording-file` present. The value of this key will be consumed by the test-proxy and **used to write your recording to disk**.

For example, let's invoke the test-proxy:

```powershell
test-proxy --storage-location "C:/repo/sdk-for-net/"
```

When we **start** a test run (method outlined in next section), we have to provide a file location within JSON body key `x-recording-file`.

When your recording is finalized, it will be stored following the below logic.

```script
root = C:/repo/sdk-for-net/
recording = sdk/tools/test-proxy/tests/testFile.testFunction.cs

final_output_location = C:/repo/sdk-for-net/sdk/tools/test-proxy/tests/testFile.testFunction.cs.json
```

During a `playback` start, the **same** value for `x-recording-file` should be provided within the POST body. This allows the test-proxy to load a previous recording into memory.

Please note that if a **absolute** path is presented in header `x-recording-file`. The test-proxy will write directly to that file. If the parent folders do not exist, they will be created at run-time during the write operation.

### Start the test run

Before each individual test runs, a `recordingId` must be retrieved from the test-proxy by POST-ing to the Proxy Server.

```json
URL: https://localhost:5001/record/start
BODY {
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
headers: {
    "x-recording-id": "<x-recording-id>",
    "Content-Type": "application/json"
}
<optional> body: {
    "key1": "value1",
    "key2": "value2"
}
```

The `Content-Type` must be set ONLY if a body with values is also sent. Read section directly below [storing variables](#storing-variables) for a description.

This will **finalize** your recording by:

- Removing from active sessions.
- Applying session/recording sanitizers.
- Saving to disk.

#### Storing `variables`

In the above example notice that there is an optional body. This is extremely useful when your tests have an element of "randomness" to them. A great example of non-secret randomness would be a `tablename` provided during `tables storage` tests. It is extremely common for a given azure-sdk test framework to generate a name like `u324bca`. Unfortunately, randomness can lead to difficulties during `playback`. The URI, headers, and body of a request must match _exactly_ with a recording entry.

Given that reccordings are _not traditionally accessible_ to the client code, there is no way to "retrieve" what those random non-secret values WERE. Without that capability, one must sanitize _everything_ that could be possibly random.

An alternative is this `variable` concept. During a final POST to `/Record/Stop`, set the `Content-Type` header and make the `body` of the request a simple JSON map. The test-proxy will pass back these values in the `body` of `/Playback/Start`.

## How do I use the test-proxy to play a recording back?

### Start playback

Extremely similar to recording start.

POST to the proxy server:

```json
URL: https://localhost:5001/playback/start
BODY: {
    "x-recording-file": "<path-to-test>/recordings/<testfile>.<testname>"
}
```

As with `/record/start`, check response header `x-recording-id` to get the recording-id for usage later on.

#### Optional `variables` returned

In the case where a user set `variables` in `/Record/Stop`, those values will also be returned in the `body` of the result from `/Playback/Start`. There is no manipulation of these values, whatever is saved during `/Record/Stop` should be identical to what is available from `/Playback/Start`.

As one could guess based on the routes, these variables are saved and returned on a per-recording basis.

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

If a user does **not** provide a `fileId` via body key `x-recording-file`, the recording will be saved **in-memory only**. If a recording is saved into memory, the only way to retrieve it is to access the playback by passing along the original recordingId that you **recorded it with**.

Start the recording **without a `x-recording-file` body value**.

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

## Session and Test Level Transforms, Sanitizers, and Matchers

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

#### A note about where sanitizers apply

Each sanitizer is optionally prefaced with the **specific part** of the request/response pair that it applies to. These prefixes are

- `Uri`
- `Header`
- `Body`

A sanitizer that does _not_ include this prefix is something different, and probably applies at the session level instead on an individual request/response pair.

### For Sanitizers, Matchers, or Transforms in general

When invoked as basic requests to the `Admin` controller, these settings will be applied to **all** further requests and responses. Both `Playback` and `Recording`. Where applicable.

- `sanitizers` are applied before the recording is saved to disk AND when an incoming request is matched.
- A custom `matcher` can be set for a session or individual recording and is applied when retrieving an entry from a loaded recording.
- `transforms` are applied when returning a request during playback.

Currently, the configured set of transforms/playback/sanitizers are NOT propogated onto disk alongside the recording.

### Viewing available/active Sanitizers, Matchers, and Transforms

Launch the test-proxy through your chosen method, then visit:

- `<proxyUrl>/Info/Available` to see all available
- `<proxyUrl>/Info/Active` to see all currently active.

Note that the `constructor arguments` that are documented must be present (where documented as such) in the body of the POST sent to the Admin Interface.

### Resetting active Sanitizers, Matchers, and Transforms

Given that the test-proxy offers the ability to set up customizations for an entire session or a single recording, it also must provide the ability to **reset** these settings without entirely restarting the server.

This is allowed through the use of the `/Admin/Reset` API. A `reset` operation "returns to default".

#### Reset the session

```json
POST
url: <proxyURL>/Admin/Reset
```

This API operates exclusively on the `Session` level if no recordingId is provided in the header. Any customizations on individual recordings are left untouched.

#### Reset for a specific recordingId

```json
POST
url: <proxyURL>/Admin/Reset
headers: {
    "x-recording-id": "<guid>"
}
```

If the recordingId is specified in the header, that individual recording's settings will be cleared. The session level updates will remain unchanged.

## Testing

This project uses `xunit` as the test framework. This is the most popular .NET test solution [according to this twitter poll](https://twitter.com/shahedC/status/1131337874903896065?ref_src=twsrc%5Etfw%7Ctwcamp%5Etweetembed%7Ctwterm%5E1131337874903896065%7Ctwgr%5E%7Ctwcon%5Es1_c10&ref_url=https%3A%2F%2Fwakeupandcode.com%2Funit-testing-in-asp-net-core%2F) and it's also what the majority of the test projects in the `Azure/azure-sdk-tools` repo utilize as well.

## SSL Support

The test-proxy server supports SSL, but due to its local-hosted nature, SSL validation will always be an issue without some manual intervention. Real SSL certificates are not available for localhost! As a result of this, if attempting to leverage SSL when recording, some additional setup will be required.

Within this repository there is a single certificate.

- `eng/common/testproxy/dotnet-devcert.pfx`: generated on a `Ubuntu` distribution using `openssl`.

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

Import the appropriate already existing certificate within the `eng/common/testproxy/` folder.

### Docker Image + SSL

To connect to the docker on SSL, both the docker image and your local machine must trust the same SSL cert. The docker image already has `dotnet-devcert.pfx` imported and trusted, so all that is necessary is to trust that same cert prior to `docker run`.

In the future, passing in a custom cert via a bound volume that contains your certificate will be a possibility as well.

For additional reading on this process for trusting SSL certs locally, feel free to read up [here.](https://devblogs.microsoft.com/aspnet/configuring-https-in-asp-net-core-across-different-platforms/) The afore-mentioned [docker specific readme](../docker/README.md) also has details that are relevant.
