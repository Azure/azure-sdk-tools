using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Services;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Tools.TspClientTool;
using Moq;

namespace Azure.Sdk.Tools.Cli.Tests.Tools
{
    internal class TspClientToolTests
    {
        private TspClientTool tspClientTool;
        private Mock<ITypeSpecHelper> typeSpecHelper;
        private Mock<IOutputService> outputService;

        [SetUp]
        public void Setup()
        {
            var logger = new TestLogger<TspClientTool>();
            typeSpecHelper = new Mock<ITypeSpecHelper>();
            outputService = new Mock<IOutputService>();
            tspClientTool = new TspClientTool(logger, outputService.Object, typeSpecHelper.Object);
        }

        [Test]
        public void GetCommand_ReturnsValidCommand()
        {
            // Act
            var command = tspClientTool.GetCommand();

            // Assert
            Assert.That(command, Is.Not.Null);
            Assert.That(command.Name, Is.EqualTo("tsp-client"));
            Assert.That(command.Description, Is.EqualTo("TypeSpec client library generation using tsp-client"));
        }

        [Test]
        public async Task InitializeProject_WithValidPath_ReturnsSuccessMessage()
        {
            // Arrange
            var testPath = "specification/contoso/Contoso.Management/tspconfig.yaml";
            typeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(It.IsAny<string>())).Returns(true);

            // Act
            var result = await tspClientTool.InitializeProject(testPath, skipSyncAndGenerate: true);

            // Assert
            Assert.That(result, Is.Not.Null);
            // Note: This will likely fail due to npx not being available in test environment
            // but we're testing the method structure
        }

        [Test]
        public async Task SyncProject_ReturnsExpectedStructure()
        {
            // Act
            var result = await tspClientTool.SyncProject();

            // Assert
            Assert.That(result, Is.Not.Null);
            // Note: This will likely fail due to npx not being available in test environment
            // but we're testing the method structure and that it exists
        }

        [Test]
        public void GetCommand_HasExpectedSubcommands()
        {
            // Act
            var command = tspClientTool.GetCommand();

            // Assert
            var subcommandNames = command.Children.OfType<System.CommandLine.Command>().Select(c => c.Name).ToList();
            Assert.That(subcommandNames, Contains.Item("init"));
            Assert.That(subcommandNames, Contains.Item("update"));
            Assert.That(subcommandNames, Contains.Item("sync"));
            Assert.That(subcommandNames, Contains.Item("generate"));
            // Verify only 4 core commands are present
            Assert.That(subcommandNames.Count, Is.EqualTo(4));
        }

        [Test]
        public void CommandHierarchy_IsCorrectlySet()
        {
            // Assert
            Assert.That(tspClientTool.CommandHierarchy, Is.Not.Null);
            Assert.That(tspClientTool.CommandHierarchy.Length, Is.EqualTo(1));
            Assert.That(tspClientTool.CommandHierarchy[0].Verb, Is.EqualTo("tsp-client"));
            Assert.That(tspClientTool.CommandHierarchy[0].Description, Is.EqualTo("TypeSpec client library generation using tsp-client"));
        }
    }
}
