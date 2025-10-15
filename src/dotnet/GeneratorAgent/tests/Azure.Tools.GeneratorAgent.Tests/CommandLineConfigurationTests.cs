using System.CommandLine;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests
{
    [TestFixture]
    public class CommandLineConfigurationTests
    {
        [Test]
        public void CreateRootCommand_WithValidHandler_ReturnsConfiguredCommand()
        {
            var config = new CommandLineConfiguration();
            
            Task<int> TestHandler(string? typeSpecDir, string? commitId, string outputDir) => Task.FromResult(0);
            
            var rootCommand = config.CreateRootCommand(TestHandler);

            Assert.Multiple(() =>
            {
                Assert.That(rootCommand, Is.Not.Null);
                Assert.That(rootCommand.Description, Is.EqualTo("Azure SDK Generator Agent"));
                Assert.That(rootCommand.Options.Count, Is.EqualTo(3));
            });
        }

        [Test]
        public void CreateRootCommand_HasRequiredOptions()
        {
            var config = new CommandLineConfiguration();
            
            Task<int> TestHandler(string? typeSpecDir, string? commitId, string outputDir) => Task.FromResult(0);
            
            var rootCommand = config.CreateRootCommand(TestHandler);
            var optionNames = rootCommand.Options.Select(o => o.Name).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(optionNames, Contains.Item("typespec-dir"));
                Assert.That(optionNames, Contains.Item("commit-id"));
                Assert.That(optionNames, Contains.Item("output-dir"));
            });
        }

        [Test]
        public void CreateRootCommand_TypeSpecDirIsRequired()
        {
            var config = new CommandLineConfiguration();
            
            Task<int> TestHandler(string? typeSpecDir, string? commitId, string outputDir) => Task.FromResult(0);
            
            var rootCommand = config.CreateRootCommand(TestHandler);
            var typeSpecOption = rootCommand.Options.FirstOrDefault(o => o.Name == "typespec-dir");

            Assert.That(typeSpecOption?.IsRequired, Is.True);
        }

        [Test]
        public void CreateRootCommand_OutputDirIsRequired()
        {
            var config = new CommandLineConfiguration();
            
            Task<int> TestHandler(string? typeSpecDir, string? commitId, string outputDir) => Task.FromResult(0);
            
            var rootCommand = config.CreateRootCommand(TestHandler);
            var outputOption = rootCommand.Options.FirstOrDefault(o => o.Name == "output-dir");

            Assert.That(outputOption?.IsRequired, Is.True);
        }

        [Test]
        public void CreateRootCommand_CommitIdIsOptional()
        {
            var config = new CommandLineConfiguration();
            
            Task<int> TestHandler(string? typeSpecDir, string? commitId, string outputDir) => Task.FromResult(0);
            
            var rootCommand = config.CreateRootCommand(TestHandler);
            var commitOption = rootCommand.Options.FirstOrDefault(o => o.Name == "commit-id");

            Assert.That(commitOption?.IsRequired, Is.False);
        }
    }
}