// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using CommandLine;
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
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.Linq;
using Azure.Sdk.Tools.TestProxy.CommandParserOptions;

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
        /// <param name="args">CommandLineParser arguments. In server mode use double dash '--' and everything after that becomes additional arguments to Host.CreateDefaultBuilder. Ex. -- arg1 value1 arg2 value2 </param>
        public static async Task Main(string[] args = null)
        {
            VerifyVerb(args);
            var parser = new Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.HelpWriter = System.Console.Out;
                settings.EnableDashDash = true;
            });

            await parser.ParseArguments<StartOptions, PushOptions, ResetOptions, RestoreOptions>(args)
                .WithNotParsed(ExitWithError)
                .WithParsedAsync(Run);
        }

        static void ExitWithError(IEnumerable<Error> errors)
        {

            // ParseArguments lumps help/--help and version/--version into WithNotParsed
            // but their type is VersionRequestedError and HelpRequestedError/HelpVerbRequestedError.
            // If the user is requesting help or version, don't exit 1, just exit 0
            if (errors.Count() == 1)
            {
                Error err = errors.First();
                if ((err.Tag == ErrorType.HelpVerbRequestedError || err.Tag == ErrorType.HelpRequestedError || err.Tag == ErrorType.VersionRequestedError))
                {
                    Environment.Exit(0);
                }
            }
            Environment.Exit(1);
        }

        /// <summary>
        /// This is only necessary because if there's a default verb defined, ours is start,
        /// CommandLineParser doesn't verify the verb. If the issue is fixed this function
        /// can be removed.
        /// https://github.com/commandlineparser/commandline/issues/849
        /// </summary>
        /// <param name="args"></param>
        static void VerifyVerb(string[] args)
        {
            // no arguments means the server is starting with all the default options
            if (args.Length == 0)
            {
                return;
            }

            // if the first argument starts with a dash then they're options and the
            // default verb is being used.
            if (args[0].StartsWith("-"))
            {
                return;
            }

            // last but not least, the first argument is a verb, verify it's our verb
            // version and help are default verbs and need to be in here
            string[] array = { "start", "reset", "restore", "push", "version", "help" };
            if (!array.Contains(args[0]))
            {
                // The odd looking formatting is to make this look like the same error
                // CommandLineParser would output if the verb wasn't recognized.
                string error = @$"ERROR(S):
  Verb '{args[0]}' is not recognized.

  --help       Display this help screen.

  --version    Display version information.
";
                System.Console.WriteLine(error);
                Environment.Exit(1);
            }
        }

        private static async Task Run(object commandObj)
        {
            new GitProcessHandler().VerifyGitMinVersion();
            DefaultOptions defaultOptions = (DefaultOptions)commandObj;

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var semanticVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            System.Console.WriteLine($"Running proxy version is Azure.Sdk.Tools.TestProxy {semanticVersion}");

            TargetLocation = resolveRepoLocation(defaultOptions.StorageLocation);
            Resolver = new StoreResolver();
            DefaultStore = Resolver.ResolveStore(defaultOptions.StoragePlugin ?? "GitStore");

            switch (commandObj)
            {
                case StartOptions startOptions:
                    StartServer(startOptions);
                    break;
                case PushOptions pushOptions:
                    var assetsJson = RecordingHandler.GetAssetsJsonLocation(pushOptions.AssetsJsonPath, TargetLocation);
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
                default:
                    throw new ArgumentException("Invalid verb. The only supported verbs are start, push, reset and restore.");
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

            var host = Host.CreateDefaultBuilder(startOptions.AdditionalArgs.ToArray());

            host.ConfigureWebHostDefaults(
                builder =>
                    builder.UseStartup<Startup>()
                    // ripped directly from implementation of ConfigureWebDefaults@https://github.dev/dotnet/aspnetcore/blob/a779227cc2694a50b074a097889ed9e80d15cd77/src/DefaultBuilder/src/WebHost.cs#L176
                    .ConfigureLogging((hostBuilder, loggingBuilder) =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddConfiguration(hostBuilder.Configuration.GetSection("Logging"));
                        loggingBuilder.AddSimpleConsole(formatterOptions =>
                        {
                            formatterOptions.TimestampFormat = "[HH:mm:ss] ";
                        });
                        loggingBuilder.AddDebug();
                        loggingBuilder.AddEventSourceLogger();
                    })
                    .ConfigureKestrel(kestrelServerOptions =>
                    {
                        kestrelServerOptions.ConfigureEndpointDefaults(lo => lo.Protocols = HttpProtocols.Http1);
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
