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
                ChaosEnabled = true,
                Template = template
            };

            Action act = () => job.Render();
            act.Should().Throw<Exception>();

            job.Command = new List<string>{"sleep", "infinity"};
            job.Render();
            var lines = job.Rendered.ToList();
            lines.Count().Should().Be(3);
            lines[0].Should().Be("name: TestJob");
            lines[1].Should().Be("command: [\"sleep\",\"infinity\"]");
            lines[2].Should().Be("chaos: true");
        }
    }
}