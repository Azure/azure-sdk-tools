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

            IAppSettings appSettings = serviceProvider.GetRequiredService<IAppSettings>();
            Assert.NotNull(appSettings);

            var agent = serviceProvider.GetRequiredService<ErrorFixerAgent>();
            Assert.NotNull(agent);
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
        public async Task Configure_ShouldCreateNewInstanceForEachScope()
        {
            // Arrange
            IServiceProvider serviceProvider = DependencyInjection.Configure();

            // Act
            ErrorFixerAgent instance1;
            ErrorFixerAgent instance2;

            await using (AsyncServiceScope scope1 = serviceProvider.CreateAsyncScope())
            {
                instance1 = scope1.ServiceProvider.GetRequiredService<ErrorFixerAgent>();
            }

            await using (AsyncServiceScope scope2 = serviceProvider.CreateAsyncScope())
            {
                instance2 = scope2.ServiceProvider.GetRequiredService<ErrorFixerAgent>();
            }

            // Assert
            Assert.NotNull(instance1);
            Assert.NotNull(instance2);
            Assert.NotSame(instance1, instance2);
        }
    }
}