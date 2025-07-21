namespace Mcp
{
    using ModelContextProtocol.Server;
    using System.ComponentModel;
    [McpServerToolType]
    public class InitHandler
    {
        private IInit impl

        ;

        public InitHandler(IInit impl)
        {
            this.impl = impl;
        }

        [McpServerTool(Name = "init_quickstart"), Description(@"This is the tool to call when you want to initialize a new TypeSpec project.
        **Call this tool when starting a new TypeSpec project.**
        Pass in the `template` to use: `azure-core` for data-plane services, or `azure-arm` for resource-manager services.
        Pass in the `serviceNamespace` to use, which is the namespace of the service you are creating. Should be Pascal case.
        Pass in the `outputDirectory` where the project should be created. This must be an existing empty directory.
        Returns the path to the created project.")]
        public async Task<string> QuickstartAsync(
            string template, string serviceNamespace, string outputDirectory, CancellationToken cancellationToken = default
        )
        {
            return await this.impl.QuickstartAsync(template, serviceNamespace, outputDirectory, cancellationToken);
        }

        [McpServerTool(Name = "init_convert_swagger"), Description(@"Converts an existing Azure service swagger definition to a TypeSpec project.
        **Call this tool when trying to convert an existing Azure service to TypeSpec.**
        This command should only be ran once to get started working on a TypeSpec project.
        Verify whether the source swagger describes an Azure Resource Management (ARM) API
        or a data plane API if unsure.
        Pass in the `pathToSwaggerReadme` which is the path or URL to the swagger README file.
        Pass in the `outputDirectory` where the TypeSpec project should be created. This must be an existing empty directory.
        Pass in `isAzureResourceManagement` to indicate whether the swagger is for an Azure Resource Management (ARM) API.
        This should be true if the swagger's path contains `resource-manager`.
        Pass in `fullyCompatible` to indicate whether the generated TypeSpec project should be fully compatible with the swagger.
        It is recommended not to set this to `true` so that the converted TypeSpec project
        leverages TypeSpec built-in libraries with standard patterns and templates.
        Returns path to the created project.")]
        public async Task<string> ConvertSwaggerAsync(
            string pathToSwaggerReadme, string outputDirectory, bool? isAzureResourceManagement, bool? fullyCompatible, CancellationToken cancellationToken = default
        )
        {
            return await this.impl.ConvertSwaggerAsync(pathToSwaggerReadme, outputDirectory, isAzureResourceManagement, fullyCompatible, cancellationToken);
        }
    }
}