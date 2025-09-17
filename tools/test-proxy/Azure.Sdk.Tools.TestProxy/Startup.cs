// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.CommandOptions;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.AutoShutdown;
using Azure.Sdk.Tools.TestProxy.Models;
using Azure.Sdk.Tools.TestProxy.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Net.Sockets; // added for CONNECT tunneling
using System.Buffers;     // added for buffer pooling in tunneling

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
        public static ServerRecordingConfiguration ProxyConfiguration { get; } = new ServerRecordingConfiguration();

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

        private static async Task<int> Run(object commandObj)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var semanticVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            System.Console.WriteLine($"Running proxy version is Azure.Sdk.Tools.TestProxy {semanticVersion}");
            int returnCode = 0;

            new GitProcessHandler().VerifyGitMinVersion();
            DefaultOptions defaultOptions = (DefaultOptions)commandObj;

            TargetLocation = resolveRepoLocation(defaultOptions.StorageLocation);
            Resolver = new StoreResolver();
            DefaultStore = Resolver.ResolveStore(defaultOptions.StoragePlugin ?? "GitStore");
            var assetsJson = string.Empty;

            switch (commandObj)
            {
                case ConfigLocateOptions configOptions:
                    DefaultStore.SetStoreExceptionMode(false);
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(configOptions.AssetsJsonPath, TargetLocation);
                    System.Console.WriteLine(await DefaultStore.GetPath(assetsJson));
                    break;
                case ConfigShowOptions configOptions:
                    DefaultStore.SetStoreExceptionMode(false);
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(configOptions.AssetsJsonPath, TargetLocation);
                    using(var f = File.OpenRead(assetsJson))
                    {
                        using var json = JsonDocument.Parse(f);
                        System.Console.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    break;
                case ConfigCreateOptions configOptions:
                    DefaultStore.SetStoreExceptionMode(false);
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(configOptions.AssetsJsonPath, TargetLocation);
                    throw new NotImplementedException("Interactive creation of assets.json feature is not yet implemented.");
                case ConfigOptions configOptions:
                    System.Console.WriteLine("Config verb requires a subcommand after the \"config\" verb.\n\nCorrect Usage: \"Azure.Sdk.Tools.TestProxy config locate|show|create -a path/to/assets.json\"");
                    break;
                case StartOptions startOptions:
                    if (startOptions.StandardProxyMode) {
                        // default to playback when run in standard mode. hitting Record/Start or Record/Playback will switch this setting
                        ProxyConfiguration.Mode = UniversalRecordingMode.StandardPlayback;
                    }
                    else
                    {
                        ProxyConfiguration.Mode = UniversalRecordingMode.Azure;
                    }
                    StartServer(startOptions);
                    break;
                case PushOptions pushOptions:
                    DefaultStore.SetStoreExceptionMode(false);
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(pushOptions.AssetsJsonPath, TargetLocation);
                    await DefaultStore.Push(assetsJson);
                    break;
                case ResetOptions resetOptions:
                    DefaultStore.SetStoreExceptionMode(false);
                    assetsJson = RecordingHandler.GetAssetsJsonLocation(resetOptions.AssetsJsonPath, TargetLocation);
                    await DefaultStore.Reset(assetsJson);
                    break;
                case RestoreOptions restoreOptions:
                    DefaultStore.SetStoreExceptionMode(false);
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
                        AutoShutdownTime = -1,
                        Dump = false
                    });
                    break;
                default:
                    throw new ArgumentException($"Unable to parse the argument set: {string.Join(" ", storedArgs)}");
            }

            return returnCode;
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
                        if (!startOptions.UniversalOutput)
                        {
                            loggingBuilder.AddConsole(options =>
                            {
                                options.LogToStandardErrorThreshold = LogLevel.Error;
                            });
                        }
                        loggingBuilder.AddSimpleConsole(options =>
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

            var shutdownService = app.Services.GetRequiredService<ShutdownConfiguration>();
            if (startOptions.AutoShutdownTime > -1)
            {
                shutdownService.EnableAutoShutdown = true;
                shutdownService.TimeoutInSeconds = startOptions.AutoShutdownTime;
                // start the first iteration of the shutdown timer
                app.Services.GetRequiredService<ShutdownTimer>().ResetTimer();
            }

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
            services.AddSingleton<ShutdownConfiguration>();
            services.AddSingleton<ShutdownTimer>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseCors("DefaultPolicy");
            app.UseMiddleware<HttpExceptionMiddleware>();
            app.UseMiddleware<ShutdownTimerMiddleware>();

            // Basic CONNECT tunneling (no MITM). Must run very early.
            app.Use(async (context, next) =>
            {
                if (!HttpMethods.IsConnect(context.Request.Method))
                {
                    await next();
                    return;
                }

                var target = context.Request.Path.Value?.Trim('/');
                if (string.IsNullOrEmpty(target) || !TryParseConnectTarget(target, out var host, out var port))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }

                var upgrade = context.Features.Get<IHttpUpgradeFeature>();
                if (upgrade == null)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }

                using var upstream = new TcpClient();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
#if NET8_0_OR_GREATER
                    await upstream.ConnectAsync(host, port, cts.Token);
#else
                    await upstream.ConnectAsync(host, port);
#endif
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status502BadGateway;
                    return;
                }

                // Per RFC respond success then tunnel
                var establish = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\nProxy-Agent: azure-test-proxy\r\n\r\n");
                await context.Response.Body.WriteAsync(establish, 0, establish.Length);
                await context.Response.Body.FlushAsync();

                System.IO.Stream downstreamStream;
                try
                {
                    downstreamStream = await upgrade.UpgradeAsync();
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return;
                }

                using (downstreamStream)
                using (var upstreamStream = upstream.GetStream())
                {
                    var relayCts = new CancellationTokenSource();
                    var t1 = PumpAsync(downstreamStream, upstreamStream, relayCts.Token);
                    var t2 = PumpAsync(upstreamStream, downstreamStream, relayCts.Token);

                    var finished = await Task.WhenAny(t1, t2);
                    relayCts.Cancel();
                    try { await Task.WhenAll(t1, t2); } catch { }
                }
            });

            // always need to handle absolute-form requests from HTTP proxies
            app.Use(async (context, next) =>
            {
                var feat = context.Features.Get<IHttpRequestFeature>();
                var raw = feat?.RawTarget;

                // If HTTP proxy absolute-form is used, RawTarget looks like "http://host:port/path?query"
                if (!string.IsNullOrEmpty(raw) &&
                    (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                    Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                {
                    // Preserve the original absolute target for recording metadata if you want
                    context.Items["proxy.original-target"] = uri.ToString();

                    context.Request.Scheme = uri.Scheme;
                    // NOTE: HostString handles default ports (don’t pass a port if it's default)
                    context.Request.Host = uri.IsDefaultPort
                        ? new HostString(uri.Host)
                        : new HostString(uri.Host, uri.Port);

                    context.Request.Path = PathString.FromUriComponent(uri);
                    context.Request.QueryString = QueryString.FromUriComponent(uri);

                    // If the incoming Host header was the proxy host, it’s OK; upstream routing will
                    // be derived from Scheme/Host/Path set above (and your handler can set/ensure Host).
                    // If you prefer: context.Request.Headers["Host"] = context.Request.Host.ToString();
                }

                await next();
            });

            DebugLogger.ConfigureLogger(loggerFactory);

            if (Startup.ProxyConfiguration.Mode == UniversalRecordingMode.Azure)
            {
                // Legacy Azure SDK behavior: route based on x-recording-mode header
                MapRecording(app);
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
                return;
            }
            else
            {
                app.Use(async (context, next) =>
                {
                    // Allow admin/record/playback controller endpoints as-is
                    var path = context.Request.Path.Value ?? string.Empty;
                    bool isControllerEndpoint =
                        path.StartsWith("/Record", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/Playback", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);

                    if (!isControllerEndpoint)
                    {
                        var cfg = Startup.ProxyConfiguration;
                        var handler = context.RequestServices.GetRequiredService<RecordingHandler>();

                        if (cfg.Mode == UniversalRecordingMode.StandardPlayback)
                        {
                            await handler.HandlePlaybackRequest(cfg.RecordingId, context.Request, context.Response);
                            return;
                        }
                        else if (cfg.Mode == UniversalRecordingMode.StandardRecord)
                        {
                            await handler.HandleRecordRequestAsync(cfg.RecordingId, context.Request, context.Response);
                            return;
                        }
                    }

                    await next();
                });

                // expose the controller endpoints for admin/record/playback
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }
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

        // --- Helpers for CONNECT support ---
        private static bool TryParseConnectTarget(string raw, out string host, out int port)
        {
            host = string.Empty;
            port = 443;
            // expected format host[:port]
            var idx = raw.LastIndexOf(':');
            if (idx > 0 && idx < raw.Length - 1 && int.TryParse(raw[(idx + 1)..], out var parsed))
            {
                host = raw[..idx];
                port = parsed;
                return true;
            }
            host = raw;
            return !string.IsNullOrWhiteSpace(host);
        }

        private static async Task PumpAsync(Stream source, Stream destination, CancellationToken token)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(32 * 1024);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int read;
                    try
                    {
                        read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                    }
                    catch
                    {
                        break; // read error -> terminate this direction
                    }

                    if (read == 0) break; // EOF

                    try
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, read), token);
                        await destination.FlushAsync(token);
                    }
                    catch
                    {
                        break; // write error
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
