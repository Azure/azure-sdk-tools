using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Configuration
{
    public class RepositoryConfigurationCacheEntry
    {
        public RepositoryConfigurationCacheEntry(DateTimeOffset fetched, IRepositoryConfiguration repositoryConfiguration)
        {
            this.Fetched = fetched;
            this.Configuration = repositoryConfiguration;
        }

        public IRepositoryConfiguration Configuration { get; }
        public DateTimeOffset Fetched { get; }
    }
}
