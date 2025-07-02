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
        public void ValidateTypeSpecProject_WithValidPath_ReturnsSuccess()
        {
            // Arrange
            var testPath = "specification/contoso/Contoso.Management";
            typeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testPath)).Returns(true);
            typeSpecHelper.Setup(x => x.IsTypeSpecProjectForMgmtPlane(testPath)).Returns(true);
            typeSpecHelper.Setup(x => x.GetTypeSpecProjectRelativePath(testPath)).Returns(testPath);

            // Act
            var result = tspClientTool.ValidateTypeSpecProject(testPath);

            // Assert
            Assert.That(result.Message, Is.EqualTo("Valid TypeSpec project"));
            Assert.That(result.ResponseError, Is.Null);
            Assert.That(result.Result, Is.Not.Null);
        }

        [Test]
        public void ValidateTypeSpecProject_WithInvalidPath_ReturnsFailure()
        {
            // Arrange
            var testPath = "invalid/path";
            typeSpecHelper.Setup(x => x.IsValidTypeSpecProjectPath(testPath)).Returns(false);

            // Act
            var result = tspClientTool.ValidateTypeSpecProject(testPath);

            // Assert
            Assert.That(result.Message, Is.EqualTo("Invalid TypeSpec project"));
            Assert.That(result.ResponseError, Is.Null);
        }

        [Test]
        public void ValidateTypeSpecProject_WithNullPath_ReturnsError()
        {
            // Act
            var result = tspClientTool.ValidateTypeSpecProject(null);

            // Assert
            Assert.That(result.ResponseError, Is.EqualTo("Project path cannot be null or empty"));
        }

        [Test]
        public void ValidateTypeSpecProject_WithEmptyPath_ReturnsError()
        {
            // Act
            var result = tspClientTool.ValidateTypeSpecProject("");

            // Assert
            Assert.That(result.ResponseError, Is.EqualTo("Project path cannot be null or empty"));
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
