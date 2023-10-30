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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.CommandLine;
using Azure.Sdk.Tools.TestProxy.CommandOptions;
using System.Text.Json;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

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
        public static string[] storedArgs;

        private static string resolveRepoLocation(string storageLocation = null)
        {
            var envValue = Environment.GetEnvironmentVariable("TEST_PROXY_FOLDER");
            return storageLocation ?? envValue ?? Directory.GetCurrentDirectory();
        }

        /// <summary>
        /// test-proxy
        /// </summary>
        /// <param name="args">CommandLineParser arguments. In server mode use double dash '--' and everything after that becomes additional arguments to Host.CreateDefaultBuilder. Ex. -- arg1 value1 arg2 value2 </param>
        public static async Task Main(string[] args = null)
        {
            storedArgs = args;
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions(Run);
            var resultCode = await rootCommand.InvokeAsync(args);

            Environment.Exit(resultCode);
        }

        private static async Task Run(object commandObj)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var semanticVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            System.Console.WriteLine($"Running proxy version is Azure.Sdk.Tools.TestProxy {semanticVersion}");

            new GitProcessHandler().VerifyGitMinVersion();
            DefaultOptions defaultOptions = (DefaultOptions)commandObj;

            TargetLocation = resolveRepoLocation(defaultOptions.StorageLocation);
            Resolver = new StoreResolver();
            DefaultStore = Resolver.ResolveStore(defaultOptions.StoragePlugin ?? "GitStore");
            var assetsJson = string.Empty;

            switch (commandObj)
            {
                case ConfigLocateOptions configOptions:
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(configOptions.AssetsJsonPath, TargetLocation);
                    System.Console.WriteLine(await DefaultStore.GetPath(assetsJson));
                    break;
                case ConfigShowOptions configOptions:
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(configOptions.AssetsJsonPath, TargetLocation);
                    using(var f = File.OpenRead(assetsJson))
                    {
                        using var json = JsonDocument.Parse(f);
                        System.Console.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    break;
                case ConfigCreateOptions configOptions:
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(configOptions.AssetsJsonPath, TargetLocation);
                    throw new NotImplementedException("Interactive creation of assets.json feature is not yet implemented.");
                case ConfigOptions configOptions:
                    System.Console.WriteLine("Config verb requires a subcommand after the \"config\" verb.\n\nCorrect Usage: \"Azure.Sdk.Tools.TestProxy config locate|show|create -a path/to/assets.json\"");
                    break;
                case StartOptions startOptions:
                    StartServer(startOptions);
                    break;
                case PushOptions pushOptions:
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(pushOptions.AssetsJsonPath, TargetLocation);
                    await DefaultStore.Push(assetsJson);
                    break;
                case ResetOptions resetOptions:
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(resetOptions.AssetsJsonPath, TargetLocation);
                    await DefaultStore.Reset(assetsJson);
                    break;
                case RestoreOptions restoreOptions:
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(restoreOptions.AssetsJsonPath, TargetLocation);
                    await DefaultStore.Restore(assetsJson);
                    break;
                case DefaultOptions defaultOpts:
                    StartServer(new StartOptions()
                    {
                        AdditionalArgs = new string[] { },
                        StorageLocation = defaultOpts.StorageLocation,
                        StoragePlugin = defaultOpts.StoragePlugin,
                        Insecure = false,
                        Dump = false
                    });
                    break;
                default:
                    throw new ArgumentException($"Unable to parse the argument set: {string.Join(" ", storedArgs)}");
            }
        }

        private static void StartServer(StartOptions startOptions)
        {
            _insecure = startOptions.Insecure;
            Regex.CacheSize = 0;

            var statusThreadCts = new CancellationTokenSource();

            var statusThread = PrintStatus(
                () => $"[{DateTime.UtcNow.ToString("HH:mm:ss")}] Recorded: {RequestsRecorded}\tPlayed Back: {RequestsPlayedBack}",
                newLine: true, statusThreadCts.Token);

            var host = Host.CreateDefaultBuilder((startOptions.AdditionalArgs??new string[] { }).ToArray());

            host.ConfigureWebHostDefaults(
                builder =>
                    builder.UseStartup<Startup>()
                    // ripped directly from implementation of ConfigureWebDefaults@https://github.dev/dotnet/aspnetcore/blob/a779227cc2694a50b074a097889ed9e80d15cd77/src/DefaultBuilder/src/WebHost.cs#L176
                    .ConfigureLogging((hostBuilder, loggingBuilder) =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConfiguration(hostBuilder.Configuration.GetSection("Logging"));
                        loggingBuilder.AddConsole(options =>
                        {
                            options.LogToStandardErrorThreshold = startOptions.UniversalOutput ? LogLevel.None : LogLevel.Error;
                        }).AddSimpleConsole(options =>
                        {
                            options.TimestampFormat = "[HH:mm:ss] ";
                        });
                        loggingBuilder.AddDebug();
                        loggingBuilder.AddEventSourceLogger();
                    })
                    .ConfigureKestrel(kestrelServerOptions =>
                    {
                        kestrelServerOptions.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http1);
                        // default minimum rate is 240 bytes per second with 5 second grace period. Bumping to 50bps with a graceperiod of 20 seconds.
                        kestrelServerOptions.Limits.MinRequestBodyDataRate = new MinDataRate(bytesPerSecond: 50, gracePeriod: TimeSpan.FromSeconds(20));
                    })
                );

            var app = host.Build();

            if (startOptions.Dump)
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
