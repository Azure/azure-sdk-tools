using System.Runtime.InteropServices;
using Azure.Tools.GeneratorAgent.Security;
using NUnit.Framework;

namespace Azure.Tools.GeneratorAgent.Tests.Security
{
    [TestFixture]
    public class SecureProcessConfigurationTests
    {
        [Test]
        public void PowerShellExecutable_ShouldReturnPwsh()
        {
            Assert.That(SecureProcessConfiguration.PowerShellExecutable, Is.EqualTo("pwsh"));
        }

        [Test]
        public void NodeExecutable_ShouldReturnNode()
        {
            Assert.That(SecureProcessConfiguration.NodeExecutable, Is.EqualTo("node"));
        }

        [Test]
        public void DotNetExecutable_ShouldReturnDotnet()
        {
            Assert.That(SecureProcessConfiguration.DotNetExecutable, Is.EqualTo("dotnet"));
        }

        [Test]
        public void NpmExecutable_OnWindows_ShouldReturnPowerShell()
        {
            string result = SecureProcessConfiguration.NpmExecutable;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(result, Is.EqualTo("pwsh"));
            }
            else
            {
                Assert.That(result, Is.EqualTo("npm"));
            }
        }

        [Test]
        public void NpxExecutable_OnWindows_ShouldReturnPowerShell()
        {
            string result = SecureProcessConfiguration.NpxExecutable;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(result, Is.EqualTo("pwsh"));
            }
            else
            {
                Assert.That(result, Is.EqualTo("npx"));
            }
        }

        [Test]
        public void AllowedCommands_ShouldContainRequiredCommands()
        {
            var allowedCommands = SecureProcessConfiguration.AllowedCommands;

            Assert.That(allowedCommands, Contains.Item("pwsh"));
            Assert.That(allowedCommands, Contains.Item("node"));
            Assert.That(allowedCommands, Contains.Item("npm"));
            Assert.That(allowedCommands, Contains.Item("npm.cmd"));
            Assert.That(allowedCommands, Contains.Item("npx"));
            Assert.That(allowedCommands, Contains.Item("npx.cmd"));
            Assert.That(allowedCommands, Contains.Item("dotnet"));
            Assert.That(allowedCommands, Contains.Item("-Command"));
        }

        [Test]
        public void AllowedCommands_ShouldHaveCorrectCount()
        {
            var allowedCommands = SecureProcessConfiguration.AllowedCommands;

            Assert.That(allowedCommands.Count, Is.EqualTo(10));
        }

        [Test]
        public void IsCommandAllowed_WithValidCommand_ShouldReturnTrue()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("pwsh"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("node"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("npm"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("npx"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("dotnet"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("git"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("bash"), Is.True);
        }

        [Test]
        public void IsCommandAllowed_WithInvalidCommand_ShouldReturnFalse()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("malicious"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("cmd"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("rm"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("sh"), Is.False);
        }

        [Test]
        public void IsCommandAllowed_WithNullCommand_ShouldReturnFalse()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed(null!), Is.False);
        }

        [Test]
        public void IsCommandAllowed_WithEmptyCommand_ShouldReturnFalse()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed(""), Is.False);
        }

        [Test]
        public void IsCommandAllowed_WithWhitespaceCommand_ShouldReturnFalse()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("   "), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("\t"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("\n"), Is.False);
        }

        [Test]
        public void IsCommandAllowed_WithCaseVariations_ShouldReturnTrue()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("PWSH"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("NODE"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("NPM"), Is.True);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("Dotnet"), Is.True);
        }

        [Test]
        public void IsCommandAllowed_WithCommandArguments_ShouldReturnFalse()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("pwsh -Command"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("npm install"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("node script.js"), Is.False);
        }

        [Test]
        public void IsCommandAllowed_WithPathToCommand_ShouldReturnFalse()
        {
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("/usr/bin/node"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("C:\\Program Files\\nodejs\\node.exe"), Is.False);
            Assert.That(SecureProcessConfiguration.IsCommandAllowed("./node"), Is.False);
        }
    }
}
