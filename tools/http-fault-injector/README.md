# http-fault-injector

## Overview
`http-fault-injector` is an HTTP proxy server for testing HTTP clients during "faults" like:

* Partial response (full headers, 50% of body). Then either wait indefinitely, close (TCP FIN), abort (TCP RST), or finish normally.
* No response. Then either wait indefinitely, close (TCP FIN), or abort (TCP RST).

## Installation
1. [Install .Net](https://dotnet.microsoft.com/download)

2. Install http-fault-injector
```
> dotnet tool install azure.sdk.tools.httpfaultinjector --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk/nuget/v3/index.json

You can invoke the tool using the following command: http-fault-injector
Tool 'azure.sdk.tools.httpfaultinjector' (version '0.1.0') was successfully installed.
```

## Updating
```
> dotnet tool update azure.sdk.tools.httpfaultinjector --global --add-source https://pkgs.dev.azure.com/azure-sdk/public/_packaging/azure-sdk/nuget/v3/index.json

Tool 'azure.sdk.tools.httpfaultinjector' was successfully updated from version '0.1.0' to version '0.1.1'.
```

## .NET Core Developer Certificate
`http-fault-injector` uses the [.NET development certificate](https://www.hanselman.com/blog/developing-locally-with-aspnet-core-under-https-ssl-and-selfsigned-certs).  You must either configure your machine and/or client language to trust this certificate, or disable SSL validation in your client app.

### Windows/Mac
1. `dotnet dev-certs https --trust`
2. Accept the popup to trust the cert.

### Ubuntu
1. Ensure you are using openssl 1.1.1h or later.  If the latest package for your distro is older, you may need to build and install openssl yourself.
2. `sudo dotnet dev-certs https -ep /usr/local/share/ca-certificates/aspnet/https.crt --format PEM`
3. `sudo update-ca-certificates`
4. Docs:
   1. https://docs.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-5.0&tabs=visual-studio#ubuntu-trust-the-certificate-for-service-to-service-communication
   2. https://github.com/dotnet/aspnetcore/issues/27344#issuecomment-815139224

After these steps, .NET clients should automatically trust the certificate.  Other client languages may need additional steps.

### Java Windows
1. Run `dotnet dev-certs https --export-path dotnet-dev-cert.pfx` to export the cert to a file
2. Run `keytool -importcert -cacerts -file dotnet-dev-cert.pfx` to import the cert to the Java default cacerts keystore
   1. Requires admin command prompt.

## Walkthrough
1. Run `http-fault-injector`
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

3. View request in `http-fault-injector`
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
p: Partial Response (full headers, 50% of body), then wait indefinitely
pc: Partial Response (full headers, 50% of body), then close (TCP FIN)
pa: Partial Response (full headers, 50% of body), then abort (TCP RST)
pn: Partial Response (full headers, 50% of body), then finish normally
n: No response, then wait indefinitely
nc: No response, then close (TCP FIN)
na: No response, then abort (TCP RST)
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
f: Full response
p: Partial Response (full headers, 50% of body), then wait indefinitely
pc: Partial Response (full headers, 50% of body), then close (TCP FIN)
pa: Partial Response (full headers, 50% of body), then abort (TCP RST)
pn: Partial Response (full headers, 50% of body), then finish normally
n: No response, then wait indefinitely
nc: No response, then close (TCP FIN)
na: No response, then abort (TCP RST)

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

## Request Handling
When HttpFaultInjector receives a request, it:

1. Prints the request info
2. Forwards the request to the upstream server
3. Prints the response info
4. Prompts you to select a response

The available responses are:

1. Full response.  Should be identical to sending the request directly to the upstream server.
2. Partial response (full headers, 50% of body).  Then either wait indefinitely, close (TCP FIN), abort (TCP RST), or finish normally.
3. No response.  Then either wait indefinitely, close (TCP FIN), or abort (TCP RST).

Some client timeouts handle "partial response" and "no response" differently, so it's important to ensure your overall http client stack handles both correctly (and as similar as possible).  For example, if "no response" is automatically retried after some client timeout, then "partial response" should behave the same.

For "close connection" and "abort connection", clients should detect the TCP FIN or RST immediately and either throw an error or retry.

## Client Configuration

### Redirection
When testing an HTTP client, you want to use the same codepath that will be used when talking directly to a server.  For this reason, HttpFaultInjector does not act as a traditional "http proxy" which uses a different codepath in the http client.  Instead, HttpFaultInjector acts like a typical web server, and you need to configure your http client to redirect requests to it.

At the last step in your http client pipeline:

1. Set the `X-Upstream-Host` header to the upstream host the proxy should redirect to.  This should be the host from the URI you are requesting.
2. Change the host and port in the URI to the HttpFaultInjector

## Runnable Sample Clients
* .NET: https://github.com/Azure/azure-sdk-tools/tree/main/tools/http-fault-injector/sample-clients/net
* Java: https://github.com/Azure/azure-sdk-tools/tree/main/tools/http-fault-injector/sample-clients/java
