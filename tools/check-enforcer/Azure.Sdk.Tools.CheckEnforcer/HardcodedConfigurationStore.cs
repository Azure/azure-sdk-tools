using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public class HardcodedConfigurationStore : IConfigurationStore
    {
        public int ApplicationID => 40233;
        public string ApplicationName => "check-enforcer";
        public int ApplicationTokenLifetimeInMinutes => 10;

        public int MinimumCheckRuns => 1; // TODO: Need to make this config file driven.
    }
}
