using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stress.Watcher.Extensions;
using k8s;
using k8s.Models;
using Serilog;
using Serilog.Context;
using Serilog.Sinks.SystemConsole.Themes;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;

namespace Stress.Watcher
{
    public class JobEventHandler
    {
        private Kubernetes Client;

        private Serilog.Core.Logger Logger;

        public string Namespace;

        public JobEventHandler(
            Kubernetes client,
            string watchNamespace = ""
        )
        {
            Client = client;
            Namespace = watchNamespace;

            Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich
                .FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:hh:mm:ss} {Level:u3}] {Message,-30:lj} {Properties:j}{NewLine}{Exception}",
                    theme: AnsiConsoleTheme.Code
                )
                .CreateLogger();
        }

        public async Task Watch(CancellationToken cancellationToken)
        {
            string resourceVersion = null;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var listTask = Client.ListJobForAllNamespacesWithHttpMessagesAsync(
                        allowWatchBookmarks: true,
                        watch: true,
                        resourceVersion: resourceVersion,
                        cancellationToken: cancellationToken
                    );
                    var tcs = new TaskCompletionSource();
                    using var watcher = listTask.Watch<V1Job, V1JobList>(
                        (type, job) => {
                            resourceVersion = job.ResourceVersion();
                            HandleJobEvent(type, job);
                        },
                        (err) =>
                        {
                            Logger.Error(err, "Handling error event for job watch stream.");
                            if (err is KubernetesException kubernetesError)
                            {
                                // Handle "too old resource version"
                                if (string.Equals(kubernetesError.Status.Reason, "Expired", StringComparison.Ordinal))
                                {
                                    resourceVersion = null;
                                }
                            }
                            tcs.TrySetException(err);
                            throw err;
                        },
                        () => {
                            Logger.Warning("Job watch has closed.");
                            tcs.TrySetResult();
                        }
                    );
                    using var registration = cancellationToken.Register(watcher.Dispose);
                    await tcs.Task;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error with job watch stream.");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        public void HandleJobEvent(WatchEventType type, V1Job job)
        {
            using (LogContext.PushProperty("namespace", job.Namespace()))
            using (LogContext.PushProperty("job", job.Name()))
            {
                CreateAlertRule(job);
            }
        }

        public async Task CreateAlertRule(V1Job job)
        {
            if (!ShouldCreateAlertRule(job)) {
                Logger.Debug($"Skipping alert rule creation.");
                return;
            }

            if (ShouldCreateActionGroup(job)) {
                CreateActionGroup();
            }

            //Do some creation stuff
        }

        public bool ShouldCreateAlertRule(V1Job job)
        {

        }
    }
}
