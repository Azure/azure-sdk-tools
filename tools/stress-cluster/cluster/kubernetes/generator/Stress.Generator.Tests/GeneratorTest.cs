using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;

namespace Stress.Generator.Tests
{
    public class TestPrompter : IPrompter
    {
        public Queue<string> PromptValues;

        public TestPrompter()
        {
            PromptValues = new Queue<string>();
        }

        public void SetResponse(List<string> promptValues)
        {
            PromptValues = new Queue<string>();
            AddResponse(promptValues);
        }

        public void AddResponse(List<string> promptValues)
        {
            for (var i = 0; i < promptValues.Count; i++)
            {
                PromptValues.Enqueue(promptValues[i]);
                if (i == promptValues.Count - 1)
                {
                    PromptValues.Enqueue("n");
                }
                else
                {
                    PromptValues.Enqueue("y");
                }
            }
        }

        public void SetResponse(string promptValue)
        {
            PromptValues = new Queue<string>();
            AddResponse(promptValue);
        }

        public void AddResponse(string promptValue)
        {
            PromptValues.Enqueue(promptValue);
        }

        public string Prompt()
        {
            var response = PromptValues.Dequeue();
            Console.WriteLine($"{response} <-- test prompter");
            return response;
        }
    }

    public class GeneratorTests
    {
        [Fact]
        public void TestPrompt()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.SetResponse("stringvalue");
            generator.Prompt<string>().Should().Be("stringvalue");

            prompter.SetResponse("1.5");
            generator.Prompt<double>().Should().Be(1.5);
            prompter.SetResponse("1");
            generator.Prompt<double>().Should().Be(1);

            prompter.SetResponse("true");
            generator.Prompt<bool>().Should().Be(true);
            prompter.SetResponse("false");
            generator.Prompt<bool>().Should().Be(false);

            prompter.SetResponse("itemvalue1 itemvalue2 itemvalue3");
            List<string> list = generator.PromptList();
            list.Count.Should().Be(3);
            list[0].Should().Be("itemvalue1");
            list[1].Should().Be("itemvalue2");
            list[2].Should().Be("itemvalue3");
        }

        [Fact]
        public void TestGenerateResource()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            // Test resource name selection
            prompter.AddResponse("bash -c sleep 3600");
            prompter.AddResponse("true");

            var resource = generator.GenerateResource<JobWithoutAzureResourceDeployment>();
            resource.Command.Should().Equal(new List<string>{"bash", "-c", "sleep", "3600"});
            resource.ChaosEnabled.Should().Be(true);
        }

        [Fact]
        public void TestGenerateOptionalResource()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.AddResponse("bing.com");
            prompter.AddResponse("to");
            prompter.AddResponse("LossAction");
            prompter.AddResponse("0.5");
            prompter.AddResponse("y");
            prompter.AddResponse("0.2");
            prompter.AddResponse("n");

            NetworkChaos resource = generator.GenerateResource<NetworkChaos>();
            resource.ExternalTargets.Should().Equal(new List<string>{"bing.com"});
            resource.Action.Should().BeAssignableTo<NetworkChaos.LossAction>();
            var loss = resource.Action as NetworkChaos.LossAction;
            loss.Loss.Should().Be(0.5);
            loss.Correlation.Should().Be(0.2);
        }

        [Fact]
        public void TestMultipleChoiceWithIntSelection()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.AddResponse("TestStressPackage");
            prompter.AddResponse("TestStressNamespace");
            prompter.AddResponse("0");
            prompter.AddResponse("bash -c sleep 3600");
            prompter.AddResponse("true");
            prompter.AddResponse("n");

            var package = generator.GenerateResource<StressTestPackage>();
            package.Resources.Count.Should().Be(1);

            var job = package.Resources[0] as JobWithoutAzureResourceDeployment;
            job.Name.Should().Be("TestStressPackage");
            job.Command.Should().Equal(new List<string>{"bash", "-c", "sleep", "3600"});
            job.ChaosEnabled.Should().Be(true);
        }

        [Fact]
        public void TestGenerateInvalidValueRetry()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.AddResponse("bing.com");
            prompter.AddResponse("to");
            prompter.AddResponse("LossAction");
            prompter.AddResponse("invalid1");
            prompter.AddResponse("invalid2");
            prompter.AddResponse("0.5");
            prompter.AddResponse("invalidOptional1");
            prompter.AddResponse("invalidOptional2");
            prompter.AddResponse("y");
            prompter.AddResponse("0.2");
            prompter.AddResponse("n");

            NetworkChaos resource = generator.GenerateResource<NetworkChaos>();
            resource.ExternalTargets.Should().Equal(new List<string>{"bing.com"});
            resource.Action.Should().BeAssignableTo<NetworkChaos.LossAction>();
            var loss = resource.Action as NetworkChaos.LossAction;
            loss.Loss.Should().Be(0.5);
            loss.Correlation.Should().Be(0.2);
        }

        [Fact]
        public void TestGenerateInvalidMultipleChoiceRetry()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.AddResponse("TestStressPackage");
            prompter.AddResponse("TestStressNamespace");
            prompter.AddResponse("invalid1");
            prompter.AddResponse("invalid2");
            prompter.AddResponse("999");
            prompter.AddResponse("-1");
            prompter.AddResponse(nameof(JobWithoutAzureResourceDeployment));
            prompter.AddResponse("bash -c sleep 3600");
            prompter.AddResponse("invalidBool1");
            prompter.AddResponse("invalidBool2");
            prompter.AddResponse("true");
            prompter.AddResponse("invalidOptional1");
            prompter.AddResponse("n");

            var package = generator.GenerateResource<StressTestPackage>();
            package.Resources.Count.Should().Be(1);
            package.Name.Should().Be("TestStressPackage");
            package.Namespace.Should().Be("TestStressNamespace");

            var job = package.Resources[0] as JobWithoutAzureResourceDeployment;
            job.Name.Should().Be("TestStressPackage");
            job.Command.Should().Equal(new List<string>{"bash", "-c", "sleep", "3600"});
            job.ChaosEnabled.Should().Be(true);
        }

        [Fact]
        public void TestGeneratePackage()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.AddResponse("TestStressPackage");
            prompter.AddResponse("TestStressNamespace");
            prompter.AddResponse(nameof(JobWithoutAzureResourceDeployment));
            prompter.AddResponse("bash -c sleep 3600");
            prompter.AddResponse("true");
            prompter.AddResponse("y");
            prompter.AddResponse(nameof(NetworkChaos));
            prompter.AddResponse("bing.com");
            prompter.AddResponse("to");
            prompter.AddResponse("LossAction");
            prompter.AddResponse("0.5");
            prompter.AddResponse("n");
            prompter.AddResponse("n");

            var package = generator.GenerateResource<StressTestPackage>();
            package.Resources.Count.Should().Be(2);
            package.Name.Should().Be("TestStressPackage");
            package.Namespace.Should().Be("TestStressNamespace");

            var job = package.Resources[0] as JobWithoutAzureResourceDeployment;
            job.Name.Should().Be("TestStressPackage");
            job.Command.Should().Equal(new List<string>{"bash", "-c", "sleep", "3600"});
            job.ChaosEnabled.Should().Be(true);

            var chaos = package.Resources[1] as NetworkChaos;
            chaos.Name.Should().Be("TestStressPackage");
            chaos.ExternalTargets.Should().Equal(new List<string>{"bing.com"});
            chaos.Action.Should().BeAssignableTo<NetworkChaos.LossAction>();

            var loss = chaos.Action as NetworkChaos.LossAction;
            loss.Loss.Should().Be(0.5);
            loss.Correlation.Should().BeNull();
        }

        [Fact]
        public void TestGenerateNestedResources()
        {
            var prompter = new TestPrompter();
            var generator = new Generator(prompter);

            prompter.AddResponse("TestStressPackage");
            prompter.AddResponse("TestStressNamespace");
            prompter.AddResponse(nameof(NetworkChaos));
            prompter.AddResponse("bing.com");
            prompter.AddResponse("to");
            prompter.AddResponse("DelayAction");
            prompter.AddResponse("50ms");
            prompter.AddResponse("n");
            prompter.AddResponse("n");
            prompter.AddResponse("y");
            prompter.AddResponse("2");
            prompter.AddResponse("0.5");
            prompter.AddResponse("n");
            prompter.AddResponse("n");

            var package = generator.GenerateResource<StressTestPackage>();

            var chaos = package.Resources[0] as NetworkChaos;
            chaos.Name.Should().Be("TestStressPackage");
            chaos.ExternalTargets.Should().Equal(new List<string>{"bing.com"});
            chaos.Action.Should().BeAssignableTo<NetworkChaos.DelayAction>();

            var delay = chaos.Action as NetworkChaos.DelayAction;

            delay.Latency.Should().Be("50ms");
            delay.Correlation.Should().BeNull();
            delay.Jitter.Should().BeNull();

            delay.Reorder.Should().BeAssignableTo<NetworkChaos.ReorderSpec>();
            delay.Reorder.Gap.Should().Be(2);
            delay.Reorder.Reorder.Should().Be(0.5);
            delay.Reorder.Correlation.Should().BeNull();
        }
    }
}
