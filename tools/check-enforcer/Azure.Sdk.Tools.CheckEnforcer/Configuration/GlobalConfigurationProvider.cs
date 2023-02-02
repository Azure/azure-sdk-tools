using Azure.Data.AppConfiguration;
using Azure.Sdk.Tools.CheckEnforcer.Configuration;
using Microsoft.Extensions.Azure;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GlobalConfigurationProvider : IGlobalConfigurationProvider
    {
        private ConfigurationClient configurationClient;

        public GlobalConfigurationProvider(ConfigurationClient configurationClient)
        {
            this.configurationClient = configurationClient;
        }

        private object applicationIDLock = new object();
        private string applicationID;

        public string GetApplicationID()
        {
            if (applicationID == null)
            {
                lock(applicationIDLock)
                {
                    if (applicationID == null)
                    {
                        ConfigurationSetting applicationIDSetting = configurationClient.GetConfigurationSetting(
                            "checkenforcer/github-app-id"
                            );
                        applicationID = applicationIDSetting.Value;
                    }
                }
            }

            return applicationID;
        }

        private object applicationNameLock = new object();
        private string applicationName;

        public string GetApplicationName()
        {
            if (applicationName == null)
            {
                lock (applicationNameLock)
                {
                    if (applicationName == null)
                    {
                        ConfigurationSetting applicationNameSetting = configurationClient.GetConfigurationSetting(
                            "checkenforcer/check-name"
                            );
                        applicationName = applicationNameSetting.Value;
                    }
                }
            }

            return applicationName;
        }

        private object maxRequestsPerPeriodLock = new object();
        private int maxRequestsPerPeriod = -1;

        public int GetMaxRequestsPerPeriod()
        {
            if (maxRequestsPerPeriod == -1)
            {
                lock (maxRequestsPerPeriodLock)
                {
                    if (maxRequestsPerPeriod == -1)
                    {
                        ConfigurationSetting applicationNameSetting = configurationClient.GetConfigurationSetting(
                            "checkenforcer/max-requests-per-period"
                            );
                        maxRequestsPerPeriod = int.Parse(applicationNameSetting.Value);
                    }
                }
            }

            return maxRequestsPerPeriod;
        }

        private object periodDurationInSecondsLock = new object();
        private int periodDurationInSeconds = -1;

        public int GetPeriodDurationInSeconds()
        {
            if (periodDurationInSeconds == -1)
            {
                lock (periodDurationInSecondsLock)
                {
                    if (periodDurationInSeconds == -1)
                    {
                        ConfigurationSetting applicationNameSetting = configurationClient.GetConfigurationSetting(
                            "checkenforcer/period-duration-in-seconds"
                            );
                        periodDurationInSeconds = int.Parse(applicationNameSetting.Value);
                    }
                }
            }

            return periodDurationInSeconds;
        }

    }
}
