using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Provides access to analyzer prompts and their associated context for error analysis.
    /// This class is thread-safe and optimized for high-frequency access.
    /// </summary>
    internal static class AnalyzerPromptProvider
    {
        private static readonly ConcurrentDictionary<string, AgentPromptFix> _prompts = new(StringComparer.OrdinalIgnoreCase);
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        private static readonly string ResourceName = "Azure.Tools.ErrorAnalyzers.AnalyzerPrompts.json";
        private static readonly Assembly SourceAssembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Initializes the static fields of the <see cref="AnalyzerPromptProvider"/> class.
        /// </summary>
        static AnalyzerPromptProvider()
        {
            LoadPromptsFromEmbeddedResource();
        }

        private static void LoadPromptsFromEmbeddedResource()
        {
            using var stream = SourceAssembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var prompts = JsonSerializer.Deserialize<Dictionary<string, AgentPromptFix>>(json, _serializerOptions);

            if (prompts != null)
            {
                foreach (var (key, value) in prompts)
                {
                    _prompts.TryAdd(key, value);
                }
            }
        }

        /// <summary>
        /// Tries to get the prompt associated with the specified rule ID.
        /// </summary>
        internal static bool TryGetPrompt(string ruleId, out string prompt)
        {
            prompt = string.Empty;

            if (string.IsNullOrWhiteSpace(ruleId))
            {
                return false;
            }

            if (_prompts.TryGetValue(ruleId, out var fix))
            {
                prompt = fix.Prompt;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to get the formatted context for the specified rule ID and error message.
        /// </summary>
        internal static bool TryGetContext(string ruleId, string errorMessage, out string context)
        {
            context = string.Empty;

            if (string.IsNullOrWhiteSpace(ruleId) || string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }

            if (!_prompts.TryGetValue(ruleId, out var fix))
            {
                return false;
            }

            try
            {
                context = string.Format(System.Globalization.CultureInfo.InvariantCulture, fix.Context, errorMessage);
                return true;
            }
            catch (FormatException)
            {
                context = string.Empty;
                return false;
            }
        }
    }
}
