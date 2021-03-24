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

namespace Azure.Sdk.Tools.TestProxy
{
    public sealed class Startup
    {
        internal static int RequestsRecorded;
        internal static int RequestsPlayedBack;

        public Startup(IConfiguration configuration) { }

        public static void Main(string[] args)
        {
            var statusThreadCts = new CancellationTokenSource();
            var statusThread = PrintStatus(
                () => $"Recorded: {RequestsRecorded}\tPlayed Back: {RequestsPlayedBack}",
                newLine: true, statusThreadCts.Token);

            var host = Host.CreateDefaultBuilder(args);

            host.ConfigureWebHostDefaults(
                builder => builder.UseStartup<Startup>());

            host.Build().Run();

            statusThreadCts.Cancel();
            statusThread.Join();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

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
                        Console.WriteLine(obj);
                    }
                    else
                    {
                        Console.Write(obj);
                        needsExtraNewline = true;
                    }
                }

                if (needsExtraNewline)
                {
                    Console.WriteLine();
                }

                Console.WriteLine();
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
