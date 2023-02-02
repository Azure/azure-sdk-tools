using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.CheckEnforcer.Configuration
{
    public class RepositoryConfiguration : IRepositoryConfiguration
    {
        public RepositoryConfiguration()
        {
            // These value represent defaults.
            MinimumCheckRuns = 1;
            IsEnabled = true;
            TimeoutInMinutes = 1;
            Message = @"This repository is protected by Check Enforcer. The _check-enforcer_ check-run will not pass until there is at least one more check-run successfully passing. Check Enforcer supports the following comment commands:

- ```/check-enforcer evaluate```; tells Check Enforcer to evaluate this pull request.
- ```/check-enforcer override```; by-pass Check Enforcer (approvals still required).";
        }

        [YamlMember(Alias = "minimumCheckRuns")]
        public uint MinimumCheckRuns { get; internal set; }

        [YamlMember(Alias = "enabled")]
        public bool IsEnabled { get; internal set; }

        [YamlMember(Alias = "format")]
        public string Format { get; internal set; }

        [YamlMember(Alias = "timeout")]
        public uint TimeoutInMinutes { get; internal set; }

        [YamlMember(Alias = "message")]
        public string Message { get; internal set; }

        public override string ToString()
        {
            var json = JsonConvert.SerializeObject(this);
            return json;
        }
    }
}
