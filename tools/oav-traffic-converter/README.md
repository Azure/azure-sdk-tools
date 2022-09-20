# oav-traffic-converter

This little tool is intended to showcase what it would look like to transform a directory full of [`azure-sdk test-proxy`](https://github.com/Azure/azure-sdk-tools/tree/main/tools/test-proxy/Azure.Sdk.Tools.TestProxy) recordings files into traffic files consumable by the `oav` tool.

Currently, `oav` has the following restrictions:

1. Requires request/response payloads to be in individual files
2. Requires `api-version` to be in the the `URI` of each liverequest.
3. Requires JSON bodies only. (not string content)
4. Requires No UTF8 encoding.
5. Requires `statuscode` to be a `string` type.

Most of these can be patched, and I (scbedd) have volunteered to do so. Even with that, there are still discussions WRT certain features I _don't_ want to meet in the middle on. The split up payload files is definitely one of those criticisms.

All discussions WRT compatibility with [`oav` ](https://github.com/Azure/oav/) should be in context of the **live validation mode**.

**This converter does not handle non-json files. It 100% assumes valid json input.**

## Local Sample Invocation

```node
npm run build
node ./build/cli.js convert --directory <input-dir> --out <output-dir>
```

So for a local example...

```node
npm run build
node ./build/cli.js convert --directory ./input-example/ --out ./output-example/
```

Time a run...

```powershell
# on windows
measure-command { node .\build\cli.js convert --directory C:/repo/oav-traffic-converter/input-example/ --out ./output-example/ | out-host }
```

```sh
# on linux
time node ./build/cli.js convert --directory ./input-example/ --out ./output-example/
```

Cleanup a sample run...

```powershell
Get-ChildItem .\output-example\ -Filter *.json | ? { !$_.Name.Contains("output-example.json") -and !$_.Name.Contains("test_retry.pyTestStorageRetrytest_retry_on_server_error0.json") } | % { Remove-Item $_ }
```

## Specifics

The current output format of the test-proxy is fairly close to what oav requires.

```json
{
  "Entries": [
    {
      "RequestUri": "",
      "RequestMethod": "POST",
      "RequestHeaders": {},
      "RequestBody": "{}",
      "StatusCode": 201,
      "ResponseHeaders": {},
      "ResponseBody": {} 
    }
  ]
}
```

needs to convert to

```json
{
  "liveRequest": {
    "body": {},
    "method": "",
    "url": "",
    "headers": {}
  },
  "liveResponse": {
    "body": {},
    "statusCode": "201",
    "headers": {}
  }
}
```

## Recommendations going forward

The converter performed adequately enough to not put a huge wrench in the process if we want to run this live.

| Benchmark | Time (on windows) |
|---|---|
| 1 file | Instant |
| 536 files (python tables recorded tests) | ~1 second (900ms to 1422ms) |
| 1300 files (.NET blob recorded tests) | ~5 seconds (4000ms to 5700ms) |

We should patch `oav` for the more esoteric requirements, then update our recordings to mostly their format. If the certain restrictions are _not_ relaxed, then the converter will probably be integrated directly into `oav`.

### Discovered Issues

This tool has been run against the python `tables` tests successfully.

* Also tested against all `azure.storage.blobs` SessionRecordings, ran into a `too many open files` error, but was quite effective otherwise.
* The request URLS _must_ have an API Version in them. This necessitates conversion of the recording until `oav` is patched.

`Too many open files` is caused by the fact that we're just opening all the threads at once. We just gotta find an efficient way to batch without removing the performance characteristics of the current solution.
