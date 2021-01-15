# http-fault-injector

# Overview
`http-fault-injector` is an HTTP proxy server for testing HTTP clients during "faults" like "connection closed in middle of body".

# Installation
1. [Install .Net](https://dotnet.microsoft.com/download)

2. Install http-fault-injector
```
dotnet tool install azure.sdk.tools.httpfaultinjector --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk/nuget/v3/index.json

You can invoke the tool using the following command: http-fault-injector
Tool 'azure.sdk.tools.httpfaultinjector' (version '0.1.0') was successfully installed.
```

## Usage
1. Run http-fault-injector
```
> http-fault-injector

Now listening on: http://0.0.0.0:7777
Now listening on: https://0.0.0.0:7778
Application started. Press Ctrl+C to shut down.
```

2. Clone and run .NET sample client
```
> git clone https://github.com/Azure/azure-sdk-tools

> cd azure-sdk-tools\tools\http-fault-injector\sample-clients\net\

> dotnet run

Sending request...
```

3. View request in http-fault-injector
```
[10:47:20.089] Upstream Request
[10:47:20.092] URL: https://www.example.org/
[10:47:20.097] Sending request to upstream server...

[10:47:20.190] Upstream Response
[10:47:20.191] StatusCode: OK
[10:47:20.191] Headers:
[10:47:20.195]   Content-Length:1256
[10:47:20.195] Reading upstream response body...
[10:47:20.196] ContentLength: 1256

Select a response then press ENTER:
f: Full response
```

4. Choose response `f`.  Server sends full response, client prints OK.
```
> Server
f

[10:48:04.431] Sending downstream response...
[10:48:04.432] StatusCode: 200
[10:48:04.432] Headers:
[10:48:04.436]   Content-Length:1256
[10:48:04.436] Writing response body of 1256 bytes...
[10:48:04.439] Finished writing response body

> Client
OK
```

5. Run client again, choose response `pc`.  Server sends partial response then closes, client shows error.

```
> Server
Select a response then press ENTER:
pc: Partial Response (full headers, 50% of body), then close (TCP FIN)

pc

[10:51:11.231] Sending downstream response...
[10:51:11.232] StatusCode: 200
[10:51:11.232] Headers:
[10:51:11.233]   Content-Length:1256
[10:51:11.233] Writing response body of 628 bytes...
[10:51:11.234] Finished writing response body

> Client
Sending request...
Unhandled exception. System.Net.Http.HttpRequestException: Error while copying content to a stream.
 ---> System.IO.IOException: The response ended prematurely.
```
