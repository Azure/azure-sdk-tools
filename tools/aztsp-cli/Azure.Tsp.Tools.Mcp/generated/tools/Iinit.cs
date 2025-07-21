namespace Mcp
{
    /// <summary>
    /// This interface provides tools for initializing TypeSpec projects and converting existing Azure service swagger definitions to TypeSpec projects.
    /// Use this interface to onboard to TypeSpec for new services or convert existing services.
    /// </summary>
    public interface IInit
    {
        /// <summary>
        /// This is the tool to call when you want to initialize a new TypeSpec project.
        /// Call this tool when starting a new TypeSpec project.
        /// Pass in the <c>template</c> to use: <c>azure-core</c> for data-plane services, or <c>azure-arm</c> for resource-manager services.
        /// Pass in the <c>serviceNamespace</c> to use, which is the namespace of the service you are creating. Should be Pascal case. Exclude the 'Microsoft.' prefix for ARM services.
        /// Pass in the <c>outputDirectory</c> where the project should be created. This must be an existing empty directory.
        /// Returns the path to the created project.
        /// </summary>
        /// <param name="template">The template to use for the TypeSpec project.
        /// Valid values are:
        /// <list type="bullet">
        ///     <item><description>`azure-core`: for data-plane services.</description></item>
        ///     <item><description>`azure-arm`: for resource-manager services.</description></item>
        /// </list></param>
        /// <param name="serviceNamespace">The namespace of the service you are creating.
        /// This should be in Pascal case and represent the service's namespace.
        /// For example, "MyService" for a service named "My Service".</param>
        /// <param name="outputDirectory">The output directory for the generated TypeSpec project.
        /// This directory must already exist.</param>
        public Task<string> QuickstartAsync(
            string template, string serviceNamespace, string outputDirectory, CancellationToken cancellationToken = default
        );

        /// <summary>
        /// Converts an existing Azure service swagger definition to a TypeSpec project.
        /// Call this tool when trying to convert an existing Azure service to TypeSpec.
        /// This command should only be ran once to get started working on a TypeSpec project.
        /// Verify whether the source swagger describes an Azure Resource Management (ARM) API
        /// or a data plane API if unsure.
        /// Pass in the <c>pathToSwaggerReadme</c> which is the path or URL to the swagger README file.
        /// Pass in the <c>outputDirectory</c> where the TypeSpec project should be created. This must be an existing empty directory.
        /// Pass in <c>isAzureResourceManagement</c> to indicate whether the swagger is for an Azure Resource Management (ARM) API.
        /// This should be true if the swagger's path contains <c>resource-manager</c>.
        /// Pass in <c>fullyCompatible</c> to indicate whether the generated TypeSpec project should be fully compatible with the swagger.
        /// It is recommended not to set this to <c>true</c> so that the converted TypeSpec project
        /// leverages TypeSpec built-in libraries with standard patterns and templates.
        /// Returns path to the created project.
        /// </summary>
        /// <param name="pathToSwaggerReadme">The path or URL to an Azure swagger README file.</param>
        /// <param name="outputDirectory">The output directory for the generated TypeSpec project.
        /// This directory must already exist.</param>
        /// <param name="isAzureResourceManagement">Whether the generated TypeSpec project is for an Azure Resource Management (ARM) API.
        /// This should be true if the swagger's path contains <c>resource-manager</c>.</param>
        /// <param name="fullyCompatible">Whether to generate a TypeSpec project that is fully compatible with the swagger.
        /// It is recommended not to set this to <c>true</c> so that the converted TypeSpec project
        /// leverages TypeSpec built-in libraries with standard patterns and templates.</param>
        public Task<string> ConvertSwaggerAsync(
            string pathToSwaggerReadme, string outputDirectory, bool? isAzureResourceManagement, bool? fullyCompatible, CancellationToken cancellationToken = default
        );
    }
}