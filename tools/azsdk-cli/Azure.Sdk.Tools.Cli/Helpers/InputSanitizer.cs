// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IInputSanitizer
    {
        public string SanitizeLanguage(string languageId);
    }
    public class InputSanitizer : IInputSanitizer
    {
        public string SanitizeLanguage(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
            {
                return string.Empty;
            }

            var lang = languageId.ToLower();
            return lang switch
            {
                ".net" => ".NET",
                "csharp" => ".NET",
                "dotnet" => ".NET",
                "net" => ".NET",
                "android" => "Android",
                "c" => "C",
                "cpp" => "C++",
                "go" => "Go",
                "ios" => "iOS",
                "java" => "Java",
                "javascript" => "JavaScript",
                "js" => "JavaScript",
                "typescript" => "JavaScript",
                "python" => "Python",
                "rust" => "Rust",
                _ => languageId
            };
        }
    }
}
