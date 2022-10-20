# Azure SDK Tools Test Proxy

- [Azure SDK Tools Test Proxy](#azure-sdk-tools-test-proxy)
  - [Installation](#installation)
    - [Via Local Compile or .NET](#via-local-compile-or-net)
    - [Via Docker Image](#via-docker-image)
      - [A note about docker caching](#a-note-about-docker-caching)
  - [Command line arguments](#command-line-arguments)
    - [Storage Location](#storage-location)
    - [Port Assignation](#port-assignation)
      - [Environment Variable](#environment-variable)
      - [Input arguments](#input-arguments)
  - [Environment Variables](#environment-variables)
  - [How do I use the test-proxy to get a recording?](#how-do-i-use-the-test-proxy-to-get-a-recording)
    - [Where will my recordings end up?](#where-will-my-recordings-end-up)
    - [Start the test run](#start-the-test-run)
    - [Run your tests](#run-your-tests)
    - [When finished running test](#when-finished-running-test)
      - [Storing `variables`](#storing-variables)
      - [Customizing what gets recorded](#customizing-what-gets-recorded)
  - [How do I use the test-proxy to play a recording back?](#how-do-i-use-the-test-proxy-to-play-a-recording-back)
    - [Start playback](#start-playback)
      - [Optional `variables` returned](#optional-variables-returned)
    - [During your test run](#during-your-test-run)
    - [Stop playback](#stop-playback)
    - [An important note about perf testing](#an-important-note-about-perf-testing)
    - [See example implementations](#see-example-implementations)
  - [Session and Test Level Transforms, Sanitizers, and Matchers](#session-and-test-level-transforms-sanitizers-and-matchers)
    - [Add Sanitizer](#add-sanitizer)
      - [A note about where sanitizers apply](#a-note-about-where-sanitizers-apply)
    - [For Sanitizers, Matchers, or Transforms in general](#for-sanitizers-matchers-or-transforms-in-general)
    - [Viewing available/active Sanitizers, Matchers, and Transforms](#viewing-availableactive-sanitizers-matchers-and-transforms)
    - [Resetting active Sanitizers, Matchers, and Transforms](#resetting-active-sanitizers-matchers-and-transforms)
      - [Reset the session](#reset-the-session)
      - [Reset for a specific recordingId](#reset-for-a-specific-recordingid)
  - [Recording Options](#recording-options)
    - [Redirection Settings](#redirection-settings)
      - [Providing your own `Host` header](#providing-your-own-host-header)
  - [Testing](#testing)
  - [SSL Support](#ssl-support)
    - [On Mac and Windows, .NET can be used to generate a local certificate](#on-mac-and-windows-net-can-be-used-to-generate-a-local-certificate)
      - [Option 1](#option-1)
      - [Option 2](#option-2)
    - [Docker Image + SSL](#docker-image--ssl)
  - [Troubleshooting](#troubleshooting)
    - [Visual studio](#visual-studio)
      - [ASP.NET and web development](#aspnet-and-web-development)
      - [Windows IIS](#windows-iis)
  - [Asset Sync (Retrieve External Test Recordings)](#asset-sync-retrieve-external-test-recordings)

For a detailed explanation and more-or-less spec, check the [README.md](../README.md) one level up from this one.

This test proxy is intended to provide out-of-process record/playback capabilities compatible with any language. It offers session and recording level customization during both `record` and `playback` of a test.

All that is required to start recording is to make minor updates to the requests made within a given test. A standard request looks something like this:

![request_changes](https://user-images.githubusercontent.com/45376673/131716856-5a89eba5-bdb8-45a4-9195-164de01aa35a.png)

Modified to be recordable by the test proxy, it should look like this:

![request_changes_after](https://user-images.githubusercontent.com/45376673/131716970-dee28516-cb45-4589-abce-6d2aa6bec93d.png)

There is a walkthrough through the process below in the [how do I use the test proxy to get a recording.](#how-do-i-use-the-test-proxy-to-get-a-recording)

## Installation

### Via Local Compile or .NET

1. [Install .Net 5.0 or 6.0](https://dotnet.microsoft.com/download)
2. Install test-proxy

```powershell
dotnet tool update azure.sdk.tools.testproxy --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk-for-net/nuget/v3/index.json --version "1.0.0-dev*"
```

To uninstall an existing test-proxy
```powershell
dotnet tool uninstall --global azure.sdk.tools.testproxy
```

The test-proxy is also available from the [azure-sdk-for-net public feed](https://dev.azure.com/azure-sdk/public/_artifacts/feed/azure-sdk-for-net)

After successful installation, run the tool:

```powershell
test-proxy --storage-location <location>
```

If you've already installed the tool, you can always check the installed version by invoking:

```powershell
test-proxy --version
```

### Via Docker Image

The Azure SDK Team maintains a public Azure Container Registry.

```powershell
docker run -v <your-volume-name-or-location>:/srv/testproxy/ -p 5001:5001 -p 5000:5000 azsdkengsys.azurecr.io/engsys/test-proxy:latest
```

For example, to save test recordings to disk in your repo's `/sdk/<service>/tests/recordings` directory, provide the path to the root of the repo:

```powershell
docker run -v C:\\repo\\azure-sdk-for-<language>:/srv/testproxy/ -p 5001:5001 -p 5000:5000 azsdkengsys.azurecr.io/engsys/test-proxy:latest
```

Note the **port and volume mapping** as arguments! Any files that exist in this volume locally will only be appended to/updated in place. It is a non-destructive initialize.

Within the container, recording outputs are written within the directory `/srv/testproxy/`.

NOTE: if you are authenticated to github via SSH keys instead of a credential manager with https, you must mount your ssh credentials into docker. The following command shows an example mounting the default ssh key ~/.ssh/id_rsa on linux:

```bash
docker run -v /home/ben/.ssh:/root/.ssh -v /home/ben/sdk/azure-sdk-for-go:/srv/testproxy --add-host=host.docker.internal:host-gateway -p 5001:5001 -p 5000:5000 testproxy bash -c 'eval `ssh-agent` && ssh-add /root/.ssh/id_rsa && test-proxy start --dump'
```

#### A note about docker caching

The azure-sdk team regularly update the image associated with the `latest` tag. Combined with the fact that docker will aggressively cache if possible, it is very possible that developers' local machines may be running outdated versions of the test-proxy.

To ensure that your local copy is up to date, run:

```powershell
docker pull azsdkengsys.azurecr.io/engsys/test-proxy:latest
```

## Command line arguments

This is the help information for test-proxy. It uses the nuget package [`CommandLineParser`](https://www.nuget.org/packages/CommandLineParser) to parse arguments.

The test-proxy executable fulfills one of two primary purposes:

1. The test-proxy server (the only option up to this point)
2. [`asset-sync`](#asset-sync-retrieve-external-test-recordings) push/restore/reset.

This is surfaced by only showing options for the default commands. Each individual command has its own argument set that can be detailed by invoking `test-proxy <command> --help`.

```text
/>test-proxy --help
Azure.Sdk.Tools.TestProxy 1.0.0-dev.20220926.1
c Microsoft Corporation. All rights reserved.

  start      (Default Verb) Start the TestProxy.

  push       Push the assets, referenced by assets.json, into git.

  reset      Reset the assets, referenced by assets.json, from git to their original files referenced by the tag. Will prompt
             if there are pending changes.

  restore    Restore the assets, referenced by assets.json, from git.

  help       Display more information on a specific command.

  version    Display version information.
```

### Storage Location

The test-proxy resolves the storage location via:

1. Command Line argument `--storage-location` or the abbreviation `-l`
2. Environment variable TEST_PROXY_FOLDER
3. If neither are present, the working directory from which the server is invoked.

### Port Assignation

By default, the server will listen on the following port mappings:

| protocol | port |
|-|-|
| http | 5000 |
| https | 5001 |

#### Environment Variable

Set `ASPNETCORE_URLS` to define a custom port for either http or https (or both). Here are some examples:

```powershell
$env:ASPNETCORE_URLS="http://*:3331"  // Set custom port for http only
$env:ASPNETCORE_URLS="http://*3331;https://*:8881"  // set custom ports for both http and https
```

#### Input arguments

When starting the test proxy there are Verbs, as shown in the test-proxy --help output above. For the `start` verb, there can be additional command arguments that need to get passed to **Host.CreateDefaultBuilder** 'as is'. The way this is done is through the use of `--` or dashdash as it's referred to. If `--` is on the command line, everything after will be treated as arguments that will be passed into this Host call.

For example, you can use command line argument `--urls` to bind to a non-default host and port. This configuration will override the environment configuration. To bind to localhost http 9000, provide the argument `-- --urls http://localhost:9000`. Note that `--`, space, then the `--urls http://localhost:9000`.

```powershell
test-proxy -- --urls "http://localhost:9000;https://localhost:9001"
```

## Environment Variables

The test-proxy is integrated with the following environment variables.

| Variable | Usage |
|---|---|
| `TEST_PROXY_FOLDER` | if command-line argument `storage-location` is not provided when invoking the proxy, this environment variable is also checked for a valid directory to use as test-proxy context. |
| `Logging__LogLevel__Microsoft` | Defaults to `Information`. Possible valid values are `Information`, `Warning`, `Error`, `Critical`.  |

Both of the above variables can be set in the `docker` runtime by providing additional arguments EG: `docker run -e Logging__LogLevel__Microsoft=Warning azsdkengsys.azurecr.io/engsys/test-proxy:latest`. For multiple environment variables, just use multiple `-e` provisions.

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

```jsonc
// Targeted URI: https://localhost:5001/record/start
// request body
{
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
    2. Replace request <scheme:hostname> with Proxy Server <scheme:hostname>. (currently `https://localhost:5001` or `http://localhost:5000`)
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

```jsonc
// Targeted URI: https://localhost:5001/record/start

// header dictionary
{
    "x-recording-id": "<x-recording-id>",
    "Content-Type": "application/json"
}

// optional body storing VARIABLE values. See section below for additional detail.
{
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

Given that recordings are _not traditionally accessible_ to the client code, there is no way to "retrieve" what those random non-secret values WERE. Without that capability, one must sanitize _everything_ that could be possibly random.

An alternative is this `variable` concept. During a final POST to `/Record/Stop`, set the `Content-Type` header and make the `body` of the request a simple JSON map. The test-proxy will pass back these values in the `body` of `/Playback/Start`.

#### Customizing what gets recorded

Some tests send large request bodies that are not meaningful and should not be stored in the session records. In order to disable storing the request body for a specific request, add the request header "x-recording-skip" and set the value to "request-body". This header can also be used to skip an entire request/response pair from being included in the recording - this is useful for cleanup code that you might have as part of your test. To skip the request/response pair, set the "x-recording-skip" header value to "request-response". Note that the "x-recording-skip" should only be specified when in `Record` mode. As a result, any request that would use the "request-response" value when in `Record` mode should not be sent when in `Playback` mode. For requests that use "request-body" in `Record` mode, you should either null out the body of the request before sending to the test proxy when in `Playback` mode, or you can set a `CustomDefaultMatcher` with `compareBodies = false`.

One can also prevent update of a recording file on disk by providing a header of value `x-recording-skip: "request-response"` along with your POST to `/Record/Stop`.

## How do I use the test-proxy to play a recording back?

### Start playback

Extremely similar to recording start.

POST to the proxy server:

```jsonc
// Targeted URI: https://localhost:5001/playback/start
// request body
{
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

```jsonc
// targeted URI: https://localhost:5001/playback/stop
// header dictionary
{
    "x-recording-id": "<x-recording-id>"
}
```

### An important note about perf testing

If a user does **not** provide a `fileId` via body key `x-recording-file`, the recording will be saved **in-memory only**. If a recording is saved into memory, the only way to retrieve it is to access the playback by passing along the original recordingId that you **recorded it with**.

Start the recording **without a `x-recording-file` body value**.

```jsonc
// targeted URI: https://localhost:5001/record/start
// the request body will be EMPTY
{}
```

The POST will return a valid recordingId value which we will call `X`.

To load this recording for playback...

```jsonc
// Targeted URI https://localhost:5001/playback/start
// header dictionary
{
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

```jsonc
// POST TO Targeted URI: <proxyURL>/Admin/AddSanitizer
// header dictionary
{
    "x-abstraction-identifier": "UriRegexSanitizer"
}
// request body
{
    "value": "fakeaccount",
    "regex": "[a-z]+(?=\\.(?:table|blob|queue)\\.core\\.windows\\.net)"
}
```

Add a more expansive Header sanitizer that uses a target group instead of filtering by lookahead:

```jsonc
// POST to URI <proxyURL>/Admin/AddSanitizer
// dictionary dictionary
{
    "x-abstraction-identifier": "HeaderRegexSanitizer"
}
// request body
{
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

To reset a session to default customization, `POST` to `Admin/Reset`.

This API operates exclusively on the `Session` level if no recordingId is provided in the header. Any customizations on individual recordings are left untouched.

#### Reset for a specific recordingId

However, if a recordingId is provided in the header dictionary, the reset operation applies to only that individual recording.

```jsonc
// POST TO url: <proxyURL>/Admin/Reset
// header dictionary
{
    "x-recording-id": "<guid>"
}
```

The session level updates will remain unchanged.

## Recording Options

The test-proxy offers further customization beyond that offered by sanitizers, matchers, and transforms. To access this additional functionality, one must POST to `/Admin/SetRecordingOptions` with a json body that contains the options.

```jsonc
// below is an object representing all valid inputs for SetRecordingOptions body
// POST to /Admin/SetRecordingOptions
{
   // boolean value accepted. string or raw.
   "HandleRedirects": "true/false"
   // setting context directory will change the "root" path that the test proxy uses when loading a recording
   "ContextDirectory": "<valid path on local disk>",
   // as yet unused. will allow to swap away from git-backed recording storage
   "AssetsStore": "<NullStore/GitStore>",
   // customizes some transport settings all or a single recording
   "Transport": {
      // any number of certificates will be allowed here
      "Certificates": [
        {
          "PemValue": "<string content>",
          "PemKey": "<string content>",
        }
      ],
      // used specifically so that an SSL connection presenting a non-standard certificate can still be validated
      "TLSValidationCert": "<public key portion of TLS cert>",
      // if not provided, the TLS Validation validation callbacks will be used for all targeted hosts
      // if provided, only requests to the hostname contained herein will be validated against the cert present in TLSValidationCert
      "TLSValidationCertHost": "<hostname for the targeted resource associated with TLS Cert>",
   }
}
```

### Redirection Settings

The test-proxy does NOT transparent follow redirects by default. That means that if the initial request sent by the test-proxy results in some `3XX` redirect status, it **will not** follow. It will return that redirect response to the client to allow THEM to handle the redirect.

In certain cases, this is not a possibility for the client. Javascript Browser tests are a great example of this. Since both "modes" are supported, the test-proxy exposes this as a setting `HandleRedirects`.

To set this setting, POST to the `/Admin/SetRecordingOptions` route.

Example:

```jsonc
// POST to URI: https://localhost:5001/Admin/SetRecordingOptions
// body is a json dictionary, the value of HandleRedirects can be multiple representation of "true"
{
    "HandleRedirects": true
}
// ...or
{
    "HandleRedirects": 1
}

// to disable, it's just the opposite, with similar alternative support
{
    "HandleRedirects": false
}
// ...or
{
    "HandleRedirects": "false"
}
```

#### Providing your own `Host` header

In normal running, the test-proxy actually sets the Host header automatically. If one wants to provide a _specific_ header that gets used during the request, the request must be accompanied by header:

```text
"x-recording-upstream-host-header": "<host value>"
```

## Testing

This project uses `Xunit` as the test framework. This is the most popular .NET test solution [according to this twitter poll](https://twitter.com/shahedC/status/1131337874903896065?ref_src=twsrc%5Etfw%7Ctwcamp%5Etweetembed%7Ctwterm%5E1131337874903896065%7Ctwgr%5E%7Ctwcon%5Es1_c10&ref_url=https%3A%2F%2Fwakeupandcode.com%2Funit-testing-in-asp-net-core%2F) and it's also what the majority of the test projects in the `Azure/azure-sdk-tools` repo utilize as well.

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
dotnet dev-certs https --trust
```

This will be automatically retrieved if you run the nuget installed version of the tool. You may optionally use `openssl` [like so](https://raw.githubusercontent.com/BorisWilhelms/create-dotnet-devcert/f3b5da6f9107834eb31ea5ba7c0583e14cda6b31/create-dotnet-devcert.sh) to generate a certificate. Note that this shell script creates a dev cert that is compatible with ubuntu.

#### Option 2

Import the appropriate already existing certificate within the `eng/common/testproxy/` folder.

### Docker Image + SSL

To connect to the docker on SSL, both the docker image and your local machine must trust the same SSL cert. The docker image already has `dotnet-devcert.pfx` imported and trusted, so all that is necessary is to trust that same cert prior to `docker run`.

In the future, passing in a custom cert via a bound volume that contains your certificate will be a possibility as well.

For additional reading on this process for trusting SSL certs locally, feel free to read up [here.](https://devblogs.microsoft.com/aspnet/configuring-https-in-asp-net-core-across-different-platforms/) The afore-mentioned [docker specific readme](../docker/README.md) also has details that are relevant.

## Troubleshooting

### Visual studio

If you get the message dialog `The project doesn't know how to run the profile Azure.Sdk.Tools.TestProxy`, you can fix it by reviewing the next two things:

#### ASP.NET and web development

Run Visual Studio installer and make sure ASP.NET and web development is installed.

![image](https://user-images.githubusercontent.com/24213737/152257876-be1ed946-20bc-47ff-83da-f9ae05db290a.png)

Then, confirm in the right panel that `Development time IIS support` is not checked:

![image](https://user-images.githubusercontent.com/24213737/152257948-c61e6876-eb36-4414-b8de-8c85aa0532bb.png)

#### Windows IIS

[Add Internet Information](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/development-time-iis-support?view=aspnetcore-6.0) Services to your Windows installation. Here is the list of features to enable:

![image](https://user-images.githubusercontent.com/24213737/152258180-0bac3e7f-910c-45fd-aa5f-fc932fce91e6.png)

## Asset Sync (Retrieve External Test Recordings)

The `test-proxy` optionally offers integration with other git repositories for **storing** and **retrieving** recordings. This enables the proxy to work against repositories that do not emplace their test recordings directly alongside their test implementations.

For further reading about this feature, please refer to the [asset-sync documentation folder](../documentation/asset-sync/README.md).
