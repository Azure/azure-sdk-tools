// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Services.Languages;

namespace Azure.Sdk.Tools.Cli.SampleGeneration
{
    /// <summary>
    /// Provides language-specific support for sample generation including instructions and file extensions.
    /// </summary>
    internal static class LanguageSupport
    {
        /// <summary>
        /// Gets language-specific instructions for generating high-quality samples.
        /// </summary>
        /// <param name="language">The programming language</param>
        /// <returns>Language-specific instructions and best practices</returns>
        public static string GetInstructions(string language)
        {
            return language.ToLowerInvariant() switch
            {
                "dotnet" => $@"
Language-specific instructions for .NET:
- Filenames must be descriptive without file extension (e.g., ""CreateKey"", ""RetrieveKey"")
- IMPORTANT: When relevant, generate TWO separate samples for each scenario: one ending with 'Sync' (synchronous) and one ending with 'Async' (asynchronous)
- Use namespace of the form <client library namespace>.Tests.Samples
- The sample class should inherit from SamplesBase<T> where T is the appropriate TestEnvironment class
- Follow this template:
{CodeTemplates.Dotnet}
",

                "java" => @"
Language-specific instructions for Java:
- Filenames must be descriptive without file extension (e.g., ""CreateKeyExample"", ""RetrieveKeysExample"")
- Use proper Java naming conventions: PascalCase for classes, camelCase for methods and variables
- Include appropriate import statements
- Use proper package declarations
- Include JavaDoc comments for public methods and classes
- Use try-with-resources for resource management
- Follow Java coding standards and best practices
- Use DefaultAzureCredential for authentication examples
- Include proper exception handling
- Return structured JSON in this format: [{""fileName"": ""ExampleName"", ""content"": ""// Java code here""}]",

                "typescript" => $@"
Language-specific instructions for TypeScript:
- Filenames must be descriptive without file extension (e.g., ""createKey"", ""retrieveKeys"")
- Follow this template:
{CodeTemplates.TypeScript}",

                "python" => $@"
Language-specific instructions for Python:
- Filenames must be descriptive without file extension (e.g., ""create_key"", ""retrieve_key"")
- IMPORTANT: When relevant, generate TWO separate samples for each scenario: one ending with 'Sync' (synchronous) and one ending with 'Async' (asynchronous)
- Follow this template:
{CodeTemplates.Python}",

                "go" => @"
Language-specific instructions for Go:
- Filenames must be descriptive without file extension (e.g., ""CreateKeyExample"", ""RetrieveKeysExample"")
- Use proper Go naming conventions: PascalCase for exported functions and types, camelCase for unexported
- Include appropriate import statements and package declaration
- Use proper error handling with explicit error returns
- Include proper context handling for operations
- Use DefaultAzureCredential for authentication examples
- Follow Go coding standards and best practices
- Include comments for exported functions and types
- Return structured JSON in this format: [{""fileName"": ""ExampleName"", ""content"": ""// Go code here""}]",

                _ => throw new ArgumentException($"Unsupported language: '{language}'. Supported languages are: {string.Join(", ", GetSupportedLanguages())}", nameof(language))
            };
        }

        /// <summary>
        /// Gets the appropriate file extension for the given language.
        /// </summary>
        /// <param name="language">The programming language</param>
        /// <returns>File extension including the dot</returns>
        public static string GetFileExtension(string language)
        {
            return language.ToLowerInvariant() switch
            {
                "dotnet" => ".cs",
                "java" => ".java",
                "typescript" => ".ts",
                "python" => ".py",
                "go" => ".go",
                _ => throw new ArgumentException($"Unsupported language: '{language}'. Supported languages are: {string.Join(", ", GetSupportedLanguages())}", nameof(language))
            };
        }

        /// <summary>
        /// Gets the list of supported programming languages.
        /// </summary>
        /// <returns>Array of supported language identifiers</returns>
        public static string[] GetSupportedLanguages()
        {
            return new[]
            {
                "dotnet",
                "java",
                "typescript",
                "python",
                "go"
            };
        }

        /// <summary>
        /// Checks if a language is supported for sample generation.
        /// </summary>
        /// <param name="language">The language to check</param>
        /// <returns>True if the language is supported, false otherwise</returns>
        public static bool IsLanguageSupported(string language)
        {
            var supportedLanguages = GetSupportedLanguages();
            return supportedLanguages.Contains(language.ToLowerInvariant());
        }

        /// <summary>
        /// Creates a typechecker for the specified language.
        /// </summary>
        public static ILanguageTypechecker CreateTypechecker(string language, IDockerService dockerService, ILogger logger)
        {
            return language.ToLowerInvariant() switch
            {
                "typescript" => new TypeScriptTypechecker(dockerService, logger),
                _ => throw new ArgumentException($"Language '{language}' sample verification is not yet implemented.", nameof(language))
            };
        }

        public static string GetTypecheckingInstructions(string language)
        {
            return language.ToLowerInvariant() switch
            {
                "dotnet" => "Ensure proper using statements, namespace declarations, and type safety. Fix any compilation errors.",
                "typescript" => "Fix import statements, type annotations, and ensure strict TypeScript compliance. Resolve any tsc errors.",
                "python" => "Fix import statements, type hints, and ensure mypy and flake8 compliance. Resolve type and lint errors.",
                "java" => "Fix import statements, class declarations, and ensure javac compilation. Resolve compilation errors.",
                "go" => "Fix import statements, package declarations, and ensure go build and golint compliance. Resolve build and lint errors.",
                _ => "Fix syntax and type errors according to language best practices."
            };
        }
    }
}
