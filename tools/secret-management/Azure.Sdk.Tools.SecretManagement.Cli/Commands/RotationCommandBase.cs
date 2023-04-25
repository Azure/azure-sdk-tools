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

namespace Azure.Sdk.Tools.SecretManagement.Cli.Commands;

public abstract class RotationCommandBase : Command
{
    private readonly Option<string[]> nameOption = new(new[] { "--name", "-n" })
    {
        Arity = ArgumentArity.ZeroOrMore,
        Description = "Name of the plan to rotate.",
    };

    private readonly Option<string[]> tagsOption = new(new[] { "--tags", "-t" })
    {
        IsRequired = false,
        Description = "Tags to filter the plans to rotate.",
    };

    private readonly Option<string> configOption = new(new[] { "--config", "-c" })
    {
        IsRequired = false,
        Description = "Configuration root path. Defaults to current working directory.",
    };

    private readonly Option<bool> verboseOption = new(new[] { "--verbose", "-v" })
    {
        IsRequired = false,
        Description = "Verbose output",
    };

    protected RotationCommandBase(string name, string description) : base(name, description)
    {
        AddOption(this.nameOption);
        AddOption(this.tagsOption);
        AddOption(this.configOption);
        AddOption(this.verboseOption);
        this.SetHandler(ParseAndHandleCommandAsync);
    }

    protected abstract Task HandleCommandAsync(ILogger logger, RotationConfiguration rotationConfiguration,
        InvocationContext invocationContext);

    private async Task ParseAndHandleCommandAsync(InvocationContext invocationContext)
    {
        string[] names = invocationContext.ParseResult.GetValueForOption(this.nameOption)
            ?? Array.Empty<string>();

        string[] tags = invocationContext.ParseResult.GetValueForOption(this.tagsOption)
            ?? Array.Empty<string>();

        bool verbose = invocationContext.ParseResult.GetValueForOption(this.verboseOption);

        string configPath = invocationContext.ParseResult.GetValueForOption(this.configOption)
            ?? Environment.CurrentDirectory;

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
        RotationConfiguration rotationConfiguration = RotationConfiguration.From(names, tags, configPath, secretStoreFactories);

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
