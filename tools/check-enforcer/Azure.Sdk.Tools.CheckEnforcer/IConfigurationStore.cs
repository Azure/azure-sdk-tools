using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer
{
    public interface IConfigurationStore
    {
        int ApplicationID { get; }
        string ApplicationName { get; }
        int ApplicationTokenLifetimeInMinutes { get; }

        int MinimumCheckRuns { get; }
    }
}
