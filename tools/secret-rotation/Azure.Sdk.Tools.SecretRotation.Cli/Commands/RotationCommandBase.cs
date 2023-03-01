using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Identity;
using Azure.Sdk.Tools.SecretRotation.Azure;
using Azure.Sdk.Tools.SecretRotation.Configuration;
using Azure.Sdk.Tools.SecretRotation.Core;
using Azure.Sdk.Tools.SecretRotation.Stores.AzureActiveDirectory;
using Azure.Sdk.Tools.SecretRotation.Stores.AzureAppService;
using Azure.Sdk.Tools.SecretRotation.Stores.AzureDevOps;
using Azure.Sdk.Tools.SecretRotation.Stores.Generic;
using Azure.Sdk.Tools.SecretRotation.Stores.KeyVault;
using Microsoft.Extensions.Logging.Console;

namespace Azure.Sdk.Tools.SecretRotation.Cli.Commands;

public abstract class RotationCommandBase : Command
{
    private readonly Option<string> configOption = new(new[] { "--config", "-c" }, "Configuration path")
    {
        IsRequired = true, 
        Arity = ArgumentArity.ExactlyOne
    };

    private readonly Option<bool> verboseOption = new(new[] { "--verbose", "-v" }, "Verbose output");

    protected RotationCommandBase(string name, string description) : base(name, description)
    {
        AddOption(this.configOption);
        AddOption(this.verboseOption);
        this.SetHandler(ParseAndHandleCommandAsync);
    }

    protected abstract Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext);

    private async Task ParseAndHandleCommandAsync(InvocationContext invocationContext)
    {
        string configPath = invocationContext.ParseResult.GetValueForOption(this.configOption)!;
        bool verbose = invocationContext.ParseResult.GetValueForOption(this.verboseOption);

        LogLevel logLevel = verbose ? LogLevel.Trace : LogLevel.Information;

        ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder
            .AddConsoleFormatter<SimplerConsoleFormatter, ConsoleFormatterOptions>()
            .AddConsole(options => options.FormatterName = SimplerConsoleFormatter.FormatterName)
            .SetMinimumLevel(logLevel));

        ILogger logger = loggerFactory.CreateLogger(string.Empty);

        logger.LogDebug("Parsing configuration");

        // TODO: Pass a logger to the token so it can verbose log getting tokens.
        var tokenCredential = new AzureCliCredential();

        IDictionary<string, Func<StoreConfiguration, SecretStore>> secretStoreFactories =
            GetDefaultSecretStoreFactories(tokenCredential, logger);

        // TODO: Pass a logger to RotationConfiguration so it can verbose log when reading from files.
        RotationConfiguration rotationConfiguration = RotationConfiguration.From(configPath, secretStoreFactories);

        await HandleCommandAsync(logger, rotationConfiguration, invocationContext);
    }

    private static IDictionary<string, Func<StoreConfiguration, SecretStore>> GetDefaultSecretStoreFactories(
        AzureCliCredential tokenCredential, ILogger logger)
    {
        return new Dictionary<string, Func<StoreConfiguration, SecretStore>>
        {
            [RandomStringGenerator.MappingKey] = RandomStringGenerator.GetSecretStoreFactory(logger),
            [KeyVaultSecretStore.MappingKey] = KeyVaultSecretStore.GetSecretStoreFactory(tokenCredential, logger),
            [KeyVaultCertificateStore.MappingKey] = KeyVaultCertificateStore.GetSecretStoreFactory(tokenCredential, logger),
            [ManualActionStore.MappingKey] = ManualActionStore.GetSecretStoreFactory(logger, new ConsoleValueProvider()),
            [ServiceAccountPersonalAccessTokenStore.MappingKey] = ServiceAccountPersonalAccessTokenStore.GetSecretStoreFactory(tokenCredential, new SecretProvider(logger), logger),
            [ServiceConnectionParameterStore.MappingKey] = ServiceConnectionParameterStore.GetSecretStoreFactory(tokenCredential, logger),
            [AadApplicationSecretStore.MappingKey] = AadApplicationSecretStore.GetSecretStoreFactory(tokenCredential, logger),
            [AzureWebsiteStore.MappingKey] = AzureWebsiteStore.GetSecretStoreFactory(tokenCredential, logger),
            [AadApplicationSecretStore.MappingKey] = AadApplicationSecretStore.GetSecretStoreFactory(tokenCredential, logger),
        };
    }
}
