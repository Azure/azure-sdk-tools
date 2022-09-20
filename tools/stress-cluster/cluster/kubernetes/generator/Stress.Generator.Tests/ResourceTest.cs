using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace Stress.Generator.Tests
{
    public class ResourceTests
    {
        [Fact]
        public void TestRender()
        {
            var template =
@"name: (( Name ))
command: (( Command ))
chaos: (( ChaosEnabled ))";

            var job = new JobWithoutAzureResourceDeployment{
                Name = "TestJob",
                Command = new List<string>{"sleep", "infinity"},
                ChaosEnabled = true,
                Template = template
            };

            job.Render();
            var lines = job.Rendered.ToList();
            lines.Count().Should().Be(3);
            lines[0].Should().Be("name: TestJob");
            lines[1].Should().Be("command: [\"sleep\",\"infinity\"]");
            lines[2].Should().Be("chaos: true");
        }

        [Fact]
        public void TestRenderUnsetProperty()
        {
            var template =
@"name: (( Name ))
command: (( Command ))
chaos: (( ChaosEnabled ))";

            var job = new JobWithoutAzureResourceDeployment{
                Name = "TestJob",
                ChaosEnabled = true,
                Template = template
            };

            Action act = () => job.Render();

            job.Render();
            var lines = job.Rendered.ToList();
            lines.Count().Should().Be(2);
            lines[0].Should().Be("name: TestJob");
            lines[1].Should().Be("chaos: true");
        }

        [Fact]
        public void TestRenderMissingPropertyShouldFail()
        {
            var template =
@"name: (( Name ))
na: (( DoesNotExist ))";

            var job = new JobWithoutAzureResourceDeployment{
                Name = "TestJob",
                Template = template
            };

            Action act = () => job.Render();
            act.Should().Throw<Exception>();
        }


        [Fact]
        public void TestRenderMultiplePropertiesSameLine()
        {
            var template =
@"name: (( TemplateInclude )).(( Name ))
chaos: (( ChaosEnabled ))";

            var job = new JobWithoutAzureResourceDeployment{
                Name = "TestJob",
                ChaosEnabled = true,
                Template = template
            };

            job.Render();
            var lines = job.Rendered.ToList();
            lines.Count().Should().Be(2);
            lines[0].Should().Be("name: env-job-template.TestJob");
            lines[1].Should().Be("chaos: true");
        }

        [Fact]
        public void TestRenderNestedProperties()
        {
            var template =
@"name: (( Name ))
(( Action ))
";
            var net = new NetworkChaos{
                Name = "TestNetworkChaos",
                Action = new NetworkChaos.DelayAction{
                    Latency = "50ms",
                    Reorder = new NetworkChaos.ReorderSpec{
                        Gap = 1,
                        Reorder = 0.2,
                    }
                },
                Template = template
            };

            net.Render();
            var lines = net.Rendered.ToList();
            lines.Count().Should().Be(2);
            lines[0].Should().Be("name: TestNetworkChaos");

            var delayRender = lines[1].Split('\n');
            delayRender.Count().Should().Be(8);
            delayRender[0].Should().StartWith("  #");
            delayRender[1].Should().Be("  action: delay");
            delayRender[2].Should().Be("  delay:");
            delayRender[3].Should().Be("    latency: 50ms");
            delayRender[4].Should().StartWith("    #");
            delayRender[5].Should().Be("    reorder:");
            delayRender[6].Should().Be("      gap: 1");
            delayRender[7].Should().Be("      reorder: 0.2");
        }
    }
}