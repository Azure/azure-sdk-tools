using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Configuration
{
    public interface IRepositoryConfiguration
    {
        string Format { get; }
        bool IsEnabled { get; }

        uint MinimumCheckRuns { get; }
    }
}
