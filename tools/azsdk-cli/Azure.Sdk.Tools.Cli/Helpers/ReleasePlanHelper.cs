// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.


using Octokit;

namespace Azure.Sdk.Tools.Cli.Helpers
{
    public interface IReleasePlanHelper
    {
        public string SanitizeName(string languageId);
    }
    public class ReleasePlanHelper : IReleasePlanHelper
    {
        public string SanitizeName(string languageId)
        {
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
