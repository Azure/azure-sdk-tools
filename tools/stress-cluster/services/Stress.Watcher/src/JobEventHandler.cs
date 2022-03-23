using System;
using System.Collections.Generic;
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
// using Azure.ResourceManager.Insights;
// using Azure.ResourceManager.Insights.Models;
using Microsoft.Azure.Management.Monitor;
using Microsoft.Azure.Management.Monitor.Models;
using Azure.ResourceManager.Resources;

namespace Stress.Watcher
{
    public class JobEventHandler
    {
        private CancellationToken cancellationToken;

        private MonitorManagementClient monitorManagementClient;

        private Kubernetes Client;

        private Serilog.Core.Logger Logger;

        public string Namespace;

        public JobEventHandler(
            Kubernetes client,
            MonitorManagementClient monitorClient,
            string watchNamespace = ""
        )
        {
            Client = client;
            monitorManagementClient = monitorClient;
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
                            HandleJobEvent(type, job, cancellationToken);
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
                catch (ErrorResponseException ex)
                {
                    Logger.Error(ex, $"Error with job watch stream: {ex.Body.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Error with job watch stream: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); 
                }
            }
        }

        public void HandleJobEvent(WatchEventType type, V1Job job, CancellationToken cancellationToken)
        {
            using (LogContext.PushProperty("namespace", job.Namespace()))
            using (LogContext.PushProperty("job", job.Name()))
            {
                CreateAlertRule(job, cancellationToken).ContinueWith(t =>
                {
                    if (t.Exception != null)
                    {
                        Logger.Error(t.Exception, "Error creating alert rule.");
                    }
                });
            }
        }

        public async void CreateAlertRule(V1Job job, CancellationToken cancellationToken)
        {
            if (!ShouldCreateAlertRule(job, cancellationToken)) {
                Logger.Debug($"Skipping alert rule creation.");
                return;
            }

            if (ShouldCreateActionGroup(job)) {
                //await CreateActionGroup(job);
            }

            await monitorManagementClient.MetricAlerts.CreateOrUpdateAsync("rg-stress-cluster-test", "PodFailureTestAl", FailedPodsAlertRule(), cancellationToken);
            await monitorManagementClient.ScheduledQueryRules.CreateOrUpdateAsync("rg-stress-cluster-test", "ImgPullbackTestAl", ImgPullbackAlertRule(), cancellationToken);
        }

        public MetricAlertResource FailedPodsAlertRule()
        {
            MetricAlertSingleResourceMultipleMetricCriteria metricCriteria = new MetricAlertSingleResourceMultipleMetricCriteria(
                allOf: new List<MetricCriteria>()
                {
                    new MetricCriteria()
                    {
                        MetricName = "kube_pod_status_phase",
                        MetricNamespace="Microsoft.ContainerService/managedClusters",
                        Name = "Metric1",
                        Dimensions = new MetricDimension[]
                        {
                            new MetricDimension("phase", "Include", new string[]{"Failed"}),
                            new MetricDimension("namespace", "Include", new string[]{"albert"})
                        },
                        Threshold = 0,
                        OperatorProperty = "GreaterThan",
                        TimeAggregation = "Total",
                        
                    }
                }
            );

            IList<string> scope = new string[] {"/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourceGroups/rg-stress-cluster-test/providers/Microsoft.ContainerService/managedClusters/stress-test"};

            MetricAlertAction alertAction = new MetricAlertAction("/subscriptions/faa080af-c1d8-40ad-9cce-e1a450ca5b57/resourcegroups/rg-stress-cluster-test/providers/microsoft.insights/actiongroups/notify albert");
            IList<MetricAlertAction> alertActions = new MetricAlertAction[] {alertAction};
            return new MetricAlertResource(
                location:"global",
                severity:3,
                enabled:true,
                scopes:scope,
                evaluationFrequency:new TimeSpan(0,1,0),
                windowSize:new TimeSpan(0, 5, 0),
                criteria:metricCriteria,
                type:"Microsoft.Insights/metricAlerts",
                targetResourceType:"Microsoft.ContainerService/managedClusters",
                autoMitigate:true,
                actions:alertActions
            );
        }

        public LogSearchRuleResource  ImgPullbackAlertRule() {


            return new LogSearchRuleResource(
                location: "westus",
                source:new Source(),
                action: new Action(),
                name:"ImagePullbackTest",
                type:"Microsoft.Insights/scheduledQueryRules",
                tags:
                
            )


            // AlertingAction
        }
        public async Task CreateActionGroup(V1Job job) {

        }

        public bool ShouldCreateAlertRule(V1Job job, CancellationToken cancellationToken)
        {
            // Check if owner is legit alias
            // Check if alert rule is already there
            Logger.Information("Should Create Alert Rule");
            // Logger.Information(monitorManagementClient.ActionGroups.ListBySubscription().Count().ToString());
            foreach(MetricAlertResource alert in monitorManagementClient.MetricAlerts.ListByResourceGroup("rg-stress-cluster-test")) {
                // Logger.Information($"Rule Name: {alert.Name}");
                // Logger.Information($"Rule description: {alert.Description}");
                // Logger.Information($"TargetResourcesType: {alert.TargetResourceType}");
                // Logger.Information("Action Group Ids:");
                // foreach(MetricAlertAction action in alert.Actions) {
                //     Logger.Information(action.ActionGroupId);
                // }
                if (alert.Name == "Pod Failure Test Al") {
                    Logger.Information("alert already there");
                    return false;
                }
            }
            if (job.Namespace() == "albert") {
                Logger.Information("This namespace is albert");
                return true;      
            }

            return false;
        }

        public bool ShouldCreateActionGroup(V1Job job) {
            return true;
        }
    }
}
