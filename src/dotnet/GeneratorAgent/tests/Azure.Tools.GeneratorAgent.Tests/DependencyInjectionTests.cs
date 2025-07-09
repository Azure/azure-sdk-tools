using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Azure.Tools.GeneratorAgent.Tests.Composition
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void Configure_ShouldRegisterRequiredServices()
        {
            // Act
            IServiceProvider serviceProvider = DependencyInjection.Configure();

            // Assert
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            Assert.NotNull(configuration);

            AppSettings appSettings = serviceProvider.GetRequiredService<AppSettings>();
            Assert.NotNull(appSettings);

            IGeneratorAgentService agentService = serviceProvider.GetRequiredService<IGeneratorAgentService>();
            Assert.NotNull(agentService);
        }

        [Fact]
        public void Configure_ShouldLoadConfiguration()
        {
            // Act
            IServiceProvider serviceProvider = DependencyInjection.Configure();
            AppSettings appSettings = serviceProvider.GetRequiredService<AppSettings>();

            // Assert
            Assert.NotNull(appSettings.Model);
            Assert.NotNull(appSettings.AgentName);
            Assert.NotNull(appSettings.ProjectEndpoint);
            Assert.NotNull(appSettings.AgentInstructions);
        }

        [Fact]
        public void Configure_ShouldCreateNewInstanceForEachScope()
        {
            // Arrange
            IServiceProvider serviceProvider = DependencyInjection.Configure();

            // Act
            IGeneratorAgentService instance1;
            IGeneratorAgentService instance2;

            using (IServiceScope scope1 = serviceProvider.CreateScope())
            {
                instance1 = scope1.ServiceProvider.GetRequiredService<IGeneratorAgentService>();
            }

            using (IServiceScope scope2 = serviceProvider.CreateScope())
            {
                instance2 = scope2.ServiceProvider.GetRequiredService<IGeneratorAgentService>();
            }

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.NotSame(instance1, instance2);
        }
    }
}