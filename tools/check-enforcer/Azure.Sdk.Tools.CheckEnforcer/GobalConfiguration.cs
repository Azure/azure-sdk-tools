using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class GlobalConfiguration
    {

        public string GetApplicationID()
        {
            var id = Environment.GetEnvironmentVariable("GITHUBAPP_ID");
            return id;
        }

        public string GetApplicationName()
        {
            var applicationName = Environment.GetEnvironmentVariable("CHECK_NAME");
            return applicationName;
        }

        public const int ApplicationTokenLifetimeInMinutes = 10;
    }
}
