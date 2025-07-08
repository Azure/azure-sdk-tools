using Azure.Tools.GeneratorAgent.Composition;
using Azure.Tools.GeneratorAgent.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Azure.Tools.GeneratorAgent.Tests.Composition
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void Configure_ShouldRegisterRequiredServices()
        {
            IServiceProvider serviceProvider = DependencyInjection.Configure();

            IGeneratorAgentService agentService = serviceProvider.GetRequiredService<IGeneratorAgentService>();
            Assert.NotNull(agentService);
        }

        [Fact]
        public void Configure_ShouldCreateNewInstanceForEachScope()
        {
            IServiceProvider serviceProvider = DependencyInjection.Configure();
            
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