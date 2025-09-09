// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IInputSanitizer
    {
        public string SanitizeName(string languageId);
    }
    public class InputSanitizer : IInputSanitizer
    {
        public string SanitizeName(string languageId)
        {
            if (string.IsNullOrEmpty(languageId))
            {
                return string.Empty;
            }

            var lang = languageId.ToLower();
            return lang switch
            {
                "dotnet" => ".NET",
                "csharp" => ".NET",
                ".net" => ".NET",
                "typescript" => "JavaScript",
                "python" => "Python",
                "javascript" => "JavaScript",
                "java" => "Java",
                "go" => "Go",
                _ => languageId
            };
        }
    }
}
