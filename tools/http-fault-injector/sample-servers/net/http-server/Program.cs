using System;
using System.Buffers;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Azure.Sdk.Tools.HttpFaultInjector.HttpServerSample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Any, 5000);
                    options.Listen(IPAddress.Any, 5001, listenOptions =>
                    {
                        listenOptions.UseHttps("testCert.pfx", "testPassword");
                    });
                })
                .Configure(app => app.Run(async context =>
                {
                    Console.WriteLine($"Request: {context.Request.Path}");

                    if (!bool.TryParse(context.Request.Query["chunked"], out var chunked))
                    {
                        chunked = false;
                    }

                    string response;
                    if (context.Request.Path == "/download")
                    {
                        if (!int.TryParse(context.Request.Query["length"], out var length))
                        {
                            length = 1024;
                        }
                        response = new string('a', length);
                    }
                    else if (context.Request.Path == "/upload")
                    {
                        var totalBytes = 0;
                        var buffer = ArrayPool<byte>.Shared.Rent(8192);
                        try
                        {
                            int bytesRead;
                            while ((bytesRead = await context.Request.Body.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                totalBytes += bytesRead;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(buffer);
                        }

                        response = totalBytes.ToString();
                    }
                    else
                    {
                        response = "Hello World!";
                    }

                    var shortResponse = (response.Length <= 40 ? response : response.Substring(0, 40) + "...");

                    Console.WriteLine($"Response: {response.Substring(0, Math.Min(40, response.Length))}");
                    Console.WriteLine($"ResponseLength: {response.Length}");

                    if (!chunked)
                    {
                        context.Response.ContentLength = response.Length;
                    }

                    await context.Response.WriteAsync(response);
                }))
                .Build()
                .Run();
        }
    }
}
