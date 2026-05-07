// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Azure.Sdk.Tools.Cli.Helpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class NpmOptionsTests
    {
        private string tempDir;

        [SetUp]
        public void Setup()
        {
            tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }

        [Test]
        public void ShortName_WithoutPrefix_ReturnsNpm()
        {
            var options = new NpmOptions(prefix: null, args: ["tsp-client", "init"]);

            Assert.That(options.ShortName, Is.EqualTo("npm"));
        }

        [Test]
        public void ShortName_WithPrefix_ReturnsNpmDashDirectoryName()
        {
            var prefixPath = Path.Combine(tempDir, "my-tool");
            Directory.CreateDirectory(prefixPath);

            var options = new NpmOptions(prefix: prefixPath, args: ["run", "build"]);

            Assert.That(options.ShortName, Is.EqualTo("npm-my-tool"));
        }

        [Test]
        public void Prefix_WithPrefixConstructor_SetsPrefixProperty()
        {
            var options = new NpmOptions(prefix: tempDir, args: ["run", "build"]);

            Assert.That(options.Prefix, Is.EqualTo(tempDir));
        }

        [Test]
        public void Prefix_WithArgsOnlyConstructor_PrefixIsNull()
        {
            var options = new NpmOptions(args: ["install"]);

            Assert.That(options.Prefix, Is.Null);
        }

        [Test]
        public void Constructor_WithEmptyPrefix_BuildsExecDashDashArgs()
        {
            var options = new NpmOptions(prefix: "", args: ["run", "build"]);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(options.Args, Does.Contain("exec"));
                Assert.That(options.Args, Does.Contain("--"));
                Assert.That(options.Args, Does.Contain("run"));
                Assert.That(options.Args, Does.Contain("build"));
                Assert.That(options.Args, Has.No.Member("--prefix"));
            }
            else
            {
                Assert.That(options.Args, Is.EqualTo(new[] { "exec", "--", "run", "build" }));
            }
        }

        [Test]
        public void Constructor_WithPrefix_BinaryNotInNodeModulesBin_FallsBackToNpmExec()
        {
            var options = new NpmOptions(prefix: tempDir, args: ["tsp-client", "init"]);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(options.Args, Does.Contain("exec"));
                Assert.That(options.Args, Does.Contain("--prefix"));
                Assert.That(options.Args, Does.Contain(tempDir));
                Assert.That(options.Args, Does.Contain("--"));
                Assert.That(options.Args, Does.Contain("tsp-client"));
                Assert.That(options.Args, Does.Contain("init"));
            }
            else
            {
                Assert.That(options.Args, Is.EqualTo(new[] { "exec", "--prefix", tempDir, "--", "tsp-client", "init" }));
            }
        }

        [Test]
        public void Constructor_WithPrefix_BinaryExistsInNodeModulesBin_ResolvesDirectly()
        {
            var binDir = Path.Combine(tempDir, "node_modules", ".bin");
            Directory.CreateDirectory(binDir);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Create a .cmd shim like npm does on Windows
                var cmdPath = Path.Combine(binDir, "tsp-client.cmd");
                File.WriteAllText(cmdPath, "@echo off");

                var options = new NpmOptions(prefix: tempDir, args: ["tsp-client", "init"]);

                // On Windows, ProcessOptions wraps with cmd.exe /C
                Assert.That(options.Command, Is.EqualTo(ProcessOptions.CMD));
                Assert.That(options.Args, Does.Contain(cmdPath));
                Assert.That(options.Args, Does.Contain("init"));
                Assert.That(options.Args, Has.No.Member("tsp-client"));
                Assert.That(options.Args, Has.No.Member("exec"));
            }
            else
            {
                var binPath = Path.Combine(binDir, "tsp-client");
                File.WriteAllText(binPath, "#!/bin/sh");

                var options = new NpmOptions(prefix: tempDir, args: ["tsp-client", "init"]);

                Assert.That(options.Command, Is.EqualTo(binPath));
                Assert.That(options.Args, Is.EqualTo(new[] { "init" }));
            }
        }

        [Test]
        public void Constructor_WithPrefix_BinaryResolved_ArgsSkipBinaryName()
        {
            var binDir = Path.Combine(tempDir, "node_modules", ".bin");
            Directory.CreateDirectory(binDir);

            var shimName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "my-tool.cmd" : "my-tool";
            File.WriteAllText(Path.Combine(binDir, shimName), "stub");

            var options = new NpmOptions(prefix: tempDir, args: ["my-tool", "--flag", "value"]);

            // The binary name should be stripped; only remaining args are passed
            Assert.That(options.Args, Does.Contain("--flag"));
            Assert.That(options.Args, Does.Contain("value"));
            Assert.That(options.Args, Has.No.Member("exec"));
            Assert.That(options.Args, Has.No.Member("--prefix"));
        }

        [Test]
        public void Constructor_ArgsOnly_PassesArgsDirectlyToNpm()
        {
            var options = new NpmOptions(args: ["install"]);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(options.Args, Does.Contain("install"));
            }
            else
            {
                Assert.That(options.Args, Is.EqualTo(new[] { "install" }));
            }
        }

        [Test]
        public void Constructor_WithWorkingDirectory_SetsWorkingDirectory()
        {
            var options = new NpmOptions(
                prefix: null,
                args: ["run", "build"],
                workingDirectory: tempDir);

            Assert.That(options.WorkingDirectory, Is.EqualTo(tempDir));
        }

        [Test]
        public void Constructor_WithTimeout_SetsTimeout()
        {
            var timeout = TimeSpan.FromMinutes(5);

            var options = new NpmOptions(
                prefix: null,
                args: ["run", "build"],
                timeout: timeout);

            Assert.That(options.Timeout, Is.EqualTo(timeout));
        }

        [Test]
        public void Constructor_WithDefaultTimeout_UsesTwoMinutes()
        {
            var options = new NpmOptions(prefix: null, args: ["run", "build"]);

            Assert.That(options.Timeout, Is.EqualTo(TimeSpan.FromMinutes(2)));
        }

        [Test]
        public void Constructor_WithLogOutputStreamFalse_SetsLogOutputStream()
        {
            var options = new NpmOptions(
                prefix: null,
                args: ["run", "build"],
                logOutputStream: false);

            Assert.That(options.LogOutputStream, Is.False);
        }

        [Test]
        public void Constructor_WithNullPrefix_BuildsExecDashDashArgs()
        {
            var options = new NpmOptions(prefix: null, args: ["tsp-client", "init"]);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.That(options.Args, Does.Contain("exec"));
                Assert.That(options.Args, Does.Contain("--"));
                Assert.That(options.Args, Does.Contain("tsp-client"));
                Assert.That(options.Args, Does.Contain("init"));
                Assert.That(options.Args, Has.No.Member("--prefix"));
            }
            else
            {
                Assert.That(options.Args, Is.EqualTo(new[] { "exec", "--", "tsp-client", "init" }));
            }
        }

        [Test]
        public void Constructor_WithPrefix_BinaryWithNoExtraArgs_ResolvesWithEmptyArgs()
        {
            var binDir = Path.Combine(tempDir, "node_modules", ".bin");
            Directory.CreateDirectory(binDir);

            var shimName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "my-tool.cmd" : "my-tool";
            File.WriteAllText(Path.Combine(binDir, shimName), "stub");

            var options = new NpmOptions(prefix: tempDir, args: ["my-tool"]);

            // Only the binary name was provided, so remaining args should be empty
            Assert.That(options.Args, Has.No.Member("my-tool"));
            Assert.That(options.Args, Has.No.Member("exec"));
        }
    }
}
