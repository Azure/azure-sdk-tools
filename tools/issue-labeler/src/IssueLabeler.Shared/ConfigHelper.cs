using Microsoft.Extensions.Configuration;

namespace IssueLabeler.Shared
{
    public static class ConfigHelper
    {
        public static bool TryGetConfigValue(this IConfiguration config, string configName, out string? configValue, string? defaultValue = null)
        {

            if (string.IsNullOrEmpty(config[configName]))
            {
                configValue = defaultValue;
                return defaultValue != null;
            }
            configValue = config[configName];
            return true;
        }
    }
}
