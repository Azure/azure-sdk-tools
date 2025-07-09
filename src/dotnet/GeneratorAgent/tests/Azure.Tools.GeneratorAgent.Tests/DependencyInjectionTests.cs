using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Configuration;
using Azure.Tools.GeneratorAgent.Services;
using Azure.Tools.GeneratorAgent.Interfaces;
using Azure.Tools.GeneratorAgent.Logger;
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

            IAppSettings appSettings = serviceProvider.GetRequiredService<IAppSettings>();
            Assert.NotNull(appSettings);

            var agentService = serviceProvider.GetRequiredService<GeneratorAgentService>();
            Assert.NotNull(agentService);
        }

        [Fact]
        public void Configure_ShouldLoadConfiguration()
        {
            // Act
            IServiceProvider serviceProvider = DependencyInjection.Configure();
            IAppSettings appSettings = serviceProvider.GetRequiredService<IAppSettings>();

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
            GeneratorAgentService instance1;
            GeneratorAgentService instance2;

            using (IServiceScope scope1 = serviceProvider.CreateScope())
            {
                instance1 = scope1.ServiceProvider.GetRequiredService<GeneratorAgentService>();
            }

            using (IServiceScope scope2 = serviceProvider.CreateScope())
            {
                instance2 = scope2.ServiceProvider.GetRequiredService<GeneratorAgentService>();
            }

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.NotSame(instance1, instance2);
        }
    }
}