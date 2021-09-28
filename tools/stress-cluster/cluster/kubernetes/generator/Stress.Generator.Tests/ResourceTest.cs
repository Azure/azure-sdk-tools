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
image: (( Image ))
command: (( Command ))";

            var job = new Job{
                Name = "TestJob",
                Image = "TestImage",
            };
            job.Template = template.Split('\n').ToList();

            Action act = () => job.Render();
            act.Should().Throw<Exception>();

            job.Command = new List<string>{"sleep", "infinity"};
            job.Render();
            var lines = job.Rendered.ToList();
            lines.Count().Should().Be(3);
            lines[0].Should().Be("name: \"TestJob\"");
            lines[1].Should().Be("image: \"TestImage\"");
            lines[2].Should().Be("command: [\"sleep\",\"infinity\"]");
        }
    }
}