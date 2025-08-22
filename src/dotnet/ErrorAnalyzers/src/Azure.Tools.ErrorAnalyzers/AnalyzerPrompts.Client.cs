// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Tools.ErrorAnalyzers
{
    /// <summary>
    /// Client (AZC) rule prompts for Azure SDK client library analyzers.
    /// </summary>
    internal static partial class AnalyzerPrompts
    {
        /// <summary>
        /// Adds client rule prompts to the builder.
        /// </summary>
        static partial void AddClientPrompts(Dictionary<string, AgentPromptFix> builder)
        {
            // AZC0012 - Generic Type Name Violation
            builder["AZC0012"] = new AgentPromptFix(
                prompt: """
                TASK: Fix AZC0012 Generic Type Name Violation

                The analyzer has detected a generic type name that has a high chance of collision with BCL types or types from other libraries.

                INSTRUCTIONS:
                1. Read the error message carefully to identify the generic type name
                2. Look for specific suggestions in the error message (e.g., 'Consider using a more descriptive multi-word name, such as...')
                3. If suggestions are provided in the error message, prioritize using one of those suggestions
                4. If no suggestions are provided, choose a descriptive multi-word alternative based on the type's purpose
                5. Rename the type throughout the codebase, including all references, imports, and documentation
                6. Ensure the new name follows Azure SDK naming conventions and avoids BCL type collisions

                NAMING GUIDELINES:
                • Use descriptive multi-word names that clearly indicate the type's purpose
                • Avoid single generic words like 'Client', 'Manager', 'Helper', 'Service', 'Data', 'Wrapper'
                • Include service/domain context: 'BlobServiceClient', 'KeyVaultSecret', 'TestServiceWrapper'
                • Consider the type's role: 'Client' for service clients, 'Model' for data models, 'Options' for configuration

                EXAMPLES:
                • 'Wrapper' → 'TestServiceWrapperClient', 'ConfigurationWrapper', 'ResponseWrapper'
                • 'Client' → 'BlobServiceClient', 'TableServiceClient', 'KeyVaultClient'
                • 'Manager' → 'ResourceManager', 'ConnectionManager', 'CredentialManager'

                Prioritize any specific suggestions provided in the error message, as they are context-aware.
                """,
                context: "RULE: AZC0012 - Avoid generic type names\nORIGINAL ERROR: {0}\nBACKGROUND: Generic type names have a high chance of collision with BCL (Base Class Library) types or types from other libraries. Azure SDK guidelines require descriptive, multi-word names that clearly indicate the type's purpose and avoid namespace conflicts. The error message may include specific naming suggestions that should be prioritized as they are context-aware and follow the appropriate naming patterns for the specific service or domain."
            );

            // AZC0030 - Model Naming Suffix Violation
            builder["AZC0030"] = new AgentPromptFix(
                prompt: """
                TASK: Fix AZC0030 Model Naming Suffix Violation

                The analyzer has detected a model naming issue where the suffix doesn't follow Azure SDK conventions.

                INSTRUCTIONS:
                1. Read the error message carefully to identify the current model name and problematic suffix
                2. Look for the suggested replacement name in the error message (format: 'We suggest renaming it to {suggested_name}')
                3. If a specific suggestion is provided, use that exact name
                4. If no suggestion is provided, choose an appropriate suffix based on the model's purpose
                5. Rename the type throughout the codebase, including all references, imports, and documentation
                6. Ensure the new name follows Azure SDK model naming conventions

                COMMON MODEL SUFFIX PATTERNS:
                • Input/Request models: end with 'Options', 'Parameters', 'Request', or 'Input'
                • Output/Response models: end with 'Result', 'Response', 'Output', or 'Info'
                • Configuration models: end with 'Configuration', 'Settings', or 'Options'
                • Data models: end with 'Data', 'Properties', or descriptive domain terms
                • Event models: end with 'Event', 'EventData', or 'EventArgs'

                EXAMPLES:
                • 'UserModel' → 'UserData' or 'UserInfo'
                • 'CreateModel' → 'CreateOptions' or 'CreateParameters'
                • 'ResponseModel' → 'CreateResponse' or 'GetResult'

                Always prioritize the specific suggestion provided in the error message.
                """,
                context: "RULE: AZC0030 - Model naming suffix conventions\nORIGINAL ERROR: {0}\nBACKGROUND: Azure SDK model naming conventions require specific suffixes that clearly indicate the model's purpose and usage pattern. Inconsistent suffixes make the API harder to understand and use. The error message typically includes a specific suggested name that follows the appropriate conventions for the model's role in the API."
            );

            // AZC0034 - Type Name Conflict
            builder["AZC0034"] = new AgentPromptFix(
                prompt: """
                TASK: Fix AZC0034 Type Name Conflict

                The analyzer has detected a type name conflict that could cause confusion or ambiguity.

                INSTRUCTIONS:
                1. Read the error message to identify the conflicting type name and what it conflicts with
                2. Look for the suggested replacement name (format: 'Consider renaming to {suggested_name}')
                3. If a specific suggestion is provided, use that name as it's designed to resolve the conflict
                4. If no suggestion is provided, choose a more specific name that differentiates the type's purpose
                5. Rename the type throughout the codebase, including all references, imports, and documentation
                6. Ensure the new name clearly distinguishes this type from the conflicting type

                CONFLICT RESOLUTION STRATEGIES:
                • Add domain/service context: 'User' → 'BlobUser', 'StorageUser', 'KeyVaultUser'
                • Add purpose/role context: 'Client' → 'ServiceClient', 'ManagementClient', 'DataClient'
                • Add scope context: 'Options' → 'CreateOptions', 'ListOptions', 'UpdateOptions'
                • Add implementation detail: 'Provider' → 'HttpProvider', 'RestProvider', 'JsonProvider'

                EXAMPLES:
                • 'Response' conflicting with System.Net.Http.HttpResponseMessage → 'CreateResponse', 'GetBlobResponse'
                • 'Client' conflicting with another Client → 'BlobServiceClient', 'TableServiceClient'
                • 'Request' conflicting with HttpRequestMessage → 'CreateRequest', 'UpdateUserRequest'

                Always use the specific suggestion from the error message when provided, as it's tailored to resolve the exact conflict.
                """,
                context: "RULE: AZC0034 - Avoid type name conflicts\nORIGINAL ERROR: {0}\nBACKGROUND: Type name conflicts can cause ambiguity, confusion, and compilation errors. Azure SDK guidelines require unique, descriptive type names that don't conflict with existing types in the BCL, framework, or other commonly used libraries. The error message identifies the specific conflict and typically provides a suggested alternative name that resolves the issue while maintaining clarity about the type's purpose."
            );

            // AZC0035 - Missing Model Factory Method
            builder["AZC0035"] = new AgentPromptFix(
                prompt: """
                TASK: Fix AZC0035 Missing Model Factory Method

                The analyzer has detected an output model type that should have a corresponding method in a model factory class.

                INSTRUCTIONS:
                1. Read the error message to identify the output model type that needs a factory method
                2. Find or create a model factory class (should end with 'ModelFactory')
                3. Add a static method that returns the specified model type
                4. The factory method should follow Azure SDK naming conventions
                5. Include all necessary parameters to construct the model
                6. Ensure the method is public and static

                MODEL FACTORY PATTERNS:
                • Factory class naming: '{ServiceName}ModelFactory' (e.g., 'BlobModelFactory', 'KeyVaultModelFactory')
                • Method naming: Match the model name or use a descriptive verb (e.g., 'CreateUser', 'User')
                • Method signature: Include all properties as parameters or use optional parameters
                • Return type: The exact model type specified in the error

                IMPLEMENTATION STEPS:
                1. Locate existing ModelFactory class or create new one if none exists
                2. Add static method with appropriate name
                3. Include parameters for all model properties (consider optional parameters for nullable properties)
                4. Return new instance of the model with provided values
                5. Add appropriate XML documentation

                EXAMPLE FACTORY METHOD:
                ```csharp
                public static UserInfo UserInfo(string name, int id, string email = null)
                {
                    return new UserInfo(name, id, email);
                }
                ```

                The factory method enables testing scenarios by allowing creation of output models with specific values.
                """,
                context: "RULE: AZC0035 - Output models must have factory methods\nORIGINAL ERROR: {0}\nBACKGROUND: Azure SDK requires that all output model types have corresponding static factory methods in a ModelFactory class. This enables testing scenarios where developers need to create instances of output models with specific values for unit tests or mocking. Without factory methods, output models are difficult to instantiate in test scenarios because they often have internal constructors or read-only properties. The factory methods provide a public API for creating test instances of these models."
            );
        }
    }
}
