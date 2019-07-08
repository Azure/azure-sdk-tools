﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using NotificationConfiguration.Services;

namespace NotificationConfiguration
{
    class Program
    {
        /// <summary>
        /// Create notification groups for failures in scheduled builds
        /// </summary>
        /// <param name="organization">Azure DevOps Organization</param>
        /// <param name="project">Name of the DevOps project</param>
        /// <param name="pathPrefix">Path prefix to include pipelines (e.g. "\net")</param>
        /// <param name="tokenVaraibleName">Environment variable token name (e.g. "SYSTEM_ACCESSTOKEN")</param>
        /// <param name="dryRun">Prints changes but does not alter any objects</param>
        /// <returns></returns>
        static async Task Main(
            string organization,
            string project, 
            string pathPrefix, 
            string tokenVaraibleName, 
            bool dryRun = false)
        {
            var devOpsToken = Environment.GetEnvironmentVariable(tokenVaraibleName);
            var devOpsCreds = new VssBasicCredential("nobody", devOpsToken);
            var devOpsConnection = new VssConnection(new Uri($"https://dev.azure.com/{organization}/"), devOpsCreds);

#pragma warning disable CS0618 // Type or member is obsolete
            var loggerFactory = new LoggerFactory().AddConsole(includeScopes: true);
#pragma warning restore CS0618 // Type or member is obsolete
            var devOpsServiceLogger = loggerFactory.CreateLogger<AzureDevOpsService>();
            var notificationConfiguratorLogger = loggerFactory.CreateLogger<NotificationConfigurator>();

            var devOpsService = new AzureDevOpsService(devOpsConnection, devOpsServiceLogger);


            var configurator = new NotificationConfigurator(devOpsService, notificationConfiguratorLogger);
            await configurator.ConfigureNotifications(project, pathPrefix, !dryRun);
        }
    }
}
