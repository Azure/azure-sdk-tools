# Using Wiremock as the azure-sdk proxy server

Wiremock is one of the most popular mocking solutions that exist in the wild. The source code is [available here.](https://github.com/tomakehurst/wiremock)

Before we roll our own, we should at least do a couple sample runs to understand WireMock works. 

Traditional WireMock usage doesn't have the concept of "recordings" that need to be intialized for playback. Once a stub is saved using `POST __admin/recordsings.stop`, it is immediately available for use.

WireMock also supports saving proxied traffic after it's happened, more along the lines of how our current `test-proxy` works. The way this works is to:

1. Start WireMock with some targetBaseUrl being proxied. 
2. Send traffic through wiremock 
3. Calling `POST __/admin/recordings/snapshot`. 

When we do this, all the traffic that wiremock has seen since the last time we `snapshotted` will be saved to stub mappings.

Additionally, WireMock is **NOT** meant to be a general purpose proxy service. When you start your recording, you need to **know all base URLs that you want to proxy.**

From what I can tell, you cannot start multiple recordings at the same time. This means that to record a test that hits a couple URLs, we'll need to:

1. Start recording for 1/X base URLs the test hits. Run the test.
2. ... repeat for all base urls the test may be hitting
3. Re-run the test, allowing wiremock to match the stubs.

For the example in storage where the "requestId" header in the response has to match request, WireMock _does_ have options here to write from the [request into the response body.](http://wiremock.org/docs/response-templating/)


```JSON
POST /__admin/recordings/start
{
  "targetBaseUrl": "http://example.mocklab.io"
}
```

That I have found, you cannot redirect to MULTIPLE base urls in the same recording pass.

This is an intentional design decision, one that I believe makes using WireMock not suitable for our general purpose "start it up and go" philosophy that we want to take with this project.

### Running an example locally

Go and download the wiremock jar [here](http://wiremock.org/docs/running-standalone/).

Once you have the wiremock server (this example assumes `2.27.2`), start it with:

```powershell
java -jar wiremock-standalone-2.27.2.jar --https-port 5001 --verbose
```

By providing the https port, WireMock will default to using a dev certificate.

Set environment variable AZURE_RECORD_MODE to `record`.

```python
pytest test.py
```

To run in playback, set environment variable AZURE_RECORD_MODE to `playback`.

## Discovered missing features

Unable to proxy multiple base URLs at the same time. It is **necessary** to "start recording" with a single target base URL that is being proxied.

[Simulating Faults])(http://wiremock.org/docs/simulating-faults/) only supports 4 different types of fault injection. [Extending wiremock could solve this though.](http://wiremock.org/docs/extending-wiremock/)

- EMPTY_RESPONSE
- MALFORMED_RESPONSE_CHUNK
- RANDOM_DATA_THEN_CLOSE
- CONNECTION_RESET_BY_PEER
