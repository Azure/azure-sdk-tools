using CommandLine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;

namespace Azure.Sdk.Tools.HttpFaultInjector
{
    public static class Program
    {
        private class Options
        {
            [Option('i', "insecure", Default = false, HelpText = "Allow insecure upstream SSL certs")]
            public bool Insecure { get; set; }

            [Option('t', "keep-alive-timeout", Default = 120, HelpText = "Keep-alive timeout (in seconds)")]
            public int KeepAliveTimeout { get; set; }
        }

        public static void Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.HelpWriter = Console.Error;
                settings.IgnoreUnknownArguments = true;
            });

            parser.ParseArguments<Options>(args).WithParsed(options => Run(options, args));
        }

        private static void Run(Options options, string[] args)
        {
            TimeSpan keepAlive = TimeSpan.FromSeconds(options.KeepAliveTimeout);
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
            {
                Args = args,
                ContentRootPath = Directory.GetParent(Assembly.GetExecutingAssembly().Location)?.FullName
            });

            builder.WebHost.ConfigureKestrel(kestrelOptions =>
            {
                kestrelOptions.Limits.KeepAliveTimeout = keepAlive;
            });

            builder.Services.AddHttpLogging(logging =>
            {
                logging.LoggingFields = HttpLoggingFields.RequestPropertiesAndHeaders | HttpLoggingFields.ResponsePropertiesAndHeaders;
                logging.RequestHeaders.Add(Utils.ResponseSelectionHeader);
                logging.RequestHeaders.Add(Utils.UpstreamBaseUriHeader);
                logging.RequestHeaders.Add("ETag");
                logging.ResponseHeaders.Add("ETag");
            });

            // TODO: we can switch to SocketsHttpHandler and configure read/write/connect timeouts separately
            // for now let's just set upstream timeout to be slightly bigger than client timeout.
            var httpClientBuilder = builder.Services.AddHttpClient("upstream", client => client.Timeout = keepAlive + TimeSpan.FromSeconds(1));

            if (options.Insecure)
            {
                httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return new HttpClientHandler()
                    {
                        // Allow insecure SSL certs
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };
                });
            }

            builder.Logging.ClearProviders();
            builder.Logging.AddOpenTelemetry(o =>
            {
                o.SetResourceBuilder(ResourceBuilder.CreateEmpty());
                o.IncludeFormattedMessage = false;
                o.IncludeScopes = false;
                o.ParseStateValues = true;
                // can add more exporters, e.g. ApplicationInsights
                o.AddConsoleExporter();
            });
            var app = builder.Build();
            app.UseHttpLogging();
            app.UseMiddleware<FaultInjectingMiddleware>();
            app.Run();
        }
    }
}
