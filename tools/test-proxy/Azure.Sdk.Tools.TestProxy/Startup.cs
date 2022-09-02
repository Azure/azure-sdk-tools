// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.TestProxy.Common;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Azure.Sdk.Tools.TestProxy.Store;
using Azure.Sdk.Tools.TestProxy.Console;
using System.Diagnostics.CodeAnalysis;

namespace Azure.Sdk.Tools.TestProxy
{
    [ExcludeFromCodeCoverage]
    public sealed class Startup
    {
        internal static int RequestsRecorded;
        internal static int RequestsPlayedBack;

        private static bool _insecure;
        internal static bool Insecure => _insecure;

        public Startup(IConfiguration configuration) { }

        public static string TargetLocation;
        public static StoreResolver Resolver;
        public static IAssetsStore DefaultStore;

        private static string resolveRepoLocation(string storageLocation = null)
        {
            var envValue = Environment.GetEnvironmentVariable("TEST_PROXY_FOLDER");
            return storageLocation ?? envValue ?? Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// test-proxy
        /// </summary>
        /// <param name="insecure">Allow untrusted SSL certs from upstream server</param>
        /// <param name="storageLocation">The path to the target local git repo. If not provided as an argument, Environment variable TEST_PROXY_FOLDER will be consumed. Lacking both, the current working directory will be utilized.</param>
        /// <param name="storagePlugin">Does the user have a preference as to a default storage plugin? Defaults to "No plugin" currently.</param>
        /// <param name="command">A specific test-proxy action to be carried out. Supported options: ["Save", "Restore", "Reset"]</param>
        /// <param name="assetsJsonPath">Only required if a "command" value is present. This should be a path to a valid assets.json within a language repository.</param>
        /// <param name="dump">Flag. Pass to dump configuration values before starting the application.</param>
        /// <param name="version">Flag. Pass to get the version of the tool.</param>
        /// <param name="args">Unmapped arguments un-used by the test-proxy are sent directly to the ASPNET configuration provider.</param>
        public static void Main(bool insecure = false, string storageLocation = null, string storagePlugin = null, string command = null, string assetsJsonPath = null, bool dump = false, bool version = false, string[] args = null)
        {
            if (version)
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var semanticVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
                var assemblyVersion = assembly.GetName().Version;

                System.Console.WriteLine($"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}-dev.{semanticVersion}");

                Environment.Exit(0);
            }

            TargetLocation = resolveRepoLocation(storageLocation);
            Resolver = new StoreResolver();
            DefaultStore = Resolver.ResolveStore(storagePlugin ?? "GitStore");

            if (!String.IsNullOrWhiteSpace(command))
            {
                switch (command.ToLowerInvariant())
                {
                    case "save":
                        DefaultStore.Push(assetsJsonPath);
                        break;
                    case "restore":
                        DefaultStore.Restore(assetsJsonPath);
                        break;
                    case "reset":
                        DefaultStore.Reset(assetsJsonPath);
                        break;
                    default:
                        throw new Exception($"One must provide a valid value for argument \"command\". \"{command}\" is not a valid option.");
                }
            }

            _insecure = insecure;
            Regex.CacheSize = 0;

            var statusThreadCts = new CancellationTokenSource();

            var statusThread = PrintStatus(
                () => $"[{DateTime.UtcNow.ToString("HH:mm:ss")}] Recorded: {RequestsRecorded}\tPlayed Back: {RequestsPlayedBack}",
                newLine: true, statusThreadCts.Token);

            var host = Host.CreateDefaultBuilder(args);

            host.ConfigureWebHostDefaults(
                builder =>
                    builder.UseStartup<Startup>()
                    // ripped directly from implementation of ConfigureWebDefaults@https://github.dev/dotnet/aspnetcore/blob/a779227cc2694a50b074a097889ed9e80d15cd77/src/DefaultBuilder/src/WebHost.cs#L176
                    .ConfigureLogging((hostBuilder, loggingBuilder) =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConfiguration(hostBuilder.Configuration.GetSection("Logging"));
                        loggingBuilder.AddSimpleConsole(options =>
                        {
                            options.TimestampFormat = "[HH:mm:ss] ";
                        });
                        loggingBuilder.AddDebug();
                        loggingBuilder.AddEventSourceLogger();
                    })
                    .ConfigureKestrel(options =>
                    {
                        options.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http1);
                    })
                );

            var app = host.Build();

            if (dump)
            {
                var config = app.Services?.GetService<IConfiguration>();
                System.Console.WriteLine("Dumping Resolved Configuration Values:");
                if (config != null)
                {
                    foreach (var c in config.AsEnumerable())
                    {
                        System.Console.WriteLine(c.Key + " = " + c.Value);
                    }
                }
            }

            app.Run();

            statusThreadCts.Cancel();
            statusThread.Join();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(name: "DefaultPolicy",
                    builder =>
                    {
                        builder.AllowAnyHeader()
                               .AllowAnyMethod()
                               .AllowAnyOrigin()
                               .WithExposedHeaders("*");
                    });
            });

            services.AddControllers(options =>
            {
                options.InputFormatters.Add(new EmptyBodyFormatter());
            });
            services.AddControllersWithViews();
            services.AddRazorPages();

            var singletonRecordingHandler = new RecordingHandler(
                TargetLocation,
                store: DefaultStore,
                storeResolver: Resolver
            );

            services.AddSingleton<RecordingHandler>(singletonRecordingHandler);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseCors("DefaultPolicy");
            app.UseMiddleware<HttpExceptionMiddleware>();

            DebugLogger.ConfigureLogger(loggerFactory);

            MapRecording(app);
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }

        // Route requests with header x-recording-mode = X to X.HandleRequest
        // These are requests to be recorded or played back.
        private void MapRecording(IApplicationBuilder app)
        {
            foreach (var controller in new[] { "playback", "record" })
            {
                app.MapWhen(
                    context =>
                        controller.Equals(
                            GetRecordingMode(context),
                            StringComparison.OrdinalIgnoreCase),
                    app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(
                            endpoints => endpoints.MapFallbackToController(
                                "{*path}", "HandleRequest", controller));
                    });
            }
        }

        private static string GetRecordingMode(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue("x-recording-mode", out var values) || values.Count != 1)
            {
                return null;
            }

            return values[0];
        }

        // Run in dedicated thread instead of using async/await in ThreadPool, to ensure this thread has priority
        // and never fails to run to due ThreadPool starvation.
        private static Thread PrintStatus(Func<object> status, bool newLine, CancellationToken token, int intervalSeconds = 1)
        {
            var thread = new Thread(() =>
            {
                bool needsExtraNewline = false;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        Task.Delay(TimeSpan.FromSeconds(intervalSeconds), token).Wait();
                    }
                    catch (Exception e) when (ContainsOperationCanceledException(e))
                    {
                    }

                    var obj = status();

                    if (newLine)
                    {
                        System.Console.WriteLine(obj);
                    }
                    else
                    {
                        System.Console.Write(obj);
                        needsExtraNewline = true;
                    }
                }

                if (needsExtraNewline)
                {
                    System.Console.WriteLine();
                }

                System.Console.WriteLine();
            });

            thread.Start();

            return thread;
        }

        private static bool ContainsOperationCanceledException(Exception e)
        {
            if (e is OperationCanceledException)
            {
                return true;
            }
            else if (e.InnerException != null)
            {
                return ContainsOperationCanceledException(e.InnerException);
            }
            else
            {
                return false;
            }
        }
    }
}
