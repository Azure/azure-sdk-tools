using System.Reflection;
using Azure.Tools.GeneratorAgent;
using Azure.Tools.GeneratorAgent.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

internal class ToolConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly string ToolDirectory;


    public ToolConfiguration()
    {
        ToolDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Unable to determine tool installation directory");

        _configuration = CreateConfigurationInternal();
    }

    public virtual IConfiguration Configuration => _configuration;

    public virtual ILoggerFactory CreateLoggerFactory()
    {
        return LoggerFactory.Create(builder =>
            builder
                .AddConfiguration(_configuration.GetSection("Logging"))
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information));
    }

    public virtual AppSettings CreateAppSettings(ILogger<AppSettings> logger)
    {
        return new AppSettings(_configuration, logger);
    }

    private IConfiguration CreateConfigurationInternal()
    {
        return new ConfigurationBuilder()
            .SetBasePath(ToolDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable(EnvironmentVariables.EnvironmentName) ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables(EnvironmentVariables.Prefix)
            .Build();
    }
}
