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
        public void Constructor_WithPrefix_NoPackageJson_BuildsExecWithPrefix()
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
        public void Constructor_WithPrefix_PackageJsonWithDependencies_IncludesPackageFlags()
        {
            File.WriteAllText(
                Path.Combine(tempDir, "package.json"),
                """
                {
                    "dependencies": {
                        "@azure-tools/typespec-client-generator-cli": "^1.0.0",
                        "@azure-tools/typespec-autorest": "^0.40.0"
                    }
                }
                """);

            var options = new NpmOptions(prefix: tempDir, args: ["tsp-client", "init"]);

            Assert.That(options.Args, Does.Contain("--package=@azure-tools/typespec-client-generator-cli"));
            Assert.That(options.Args, Does.Contain("--package=@azure-tools/typespec-autorest"));
            Assert.That(options.Args, Does.Contain("--prefix"));
            Assert.That(options.Args, Does.Contain(tempDir));
            Assert.That(options.Args, Does.Contain("--"));
            Assert.That(options.Args, Does.Contain("tsp-client"));
            Assert.That(options.Args, Does.Contain("init"));
        }

        [Test]
        public void Constructor_WithPrefix_PackageJsonWithEmptyDependencies_FallsBackToNoPackageFlags()
        {
            File.WriteAllText(
                Path.Combine(tempDir, "package.json"),
                """
                {
                    "dependencies": {}
                }
                """);

            var options = new NpmOptions(prefix: tempDir, args: ["tsp-client", "init"]);

            Assert.That(options.Args, Has.No.Member("--package="));
            Assert.That(options.Args, Does.Contain("--prefix"));
            Assert.That(options.Args, Does.Contain(tempDir));
        }

        [Test]
        public void Constructor_WithPrefix_PackageJsonWithoutDependenciesKey_FallsBackToNoPackageFlags()
        {
            File.WriteAllText(
                Path.Combine(tempDir, "package.json"),
                """
                {
                    "name": "test-package",
                    "version": "1.0.0"
                }
                """);

            var options = new NpmOptions(prefix: tempDir, args: ["run", "build"]);

            var argsStr = string.Join(" ", options.Args);
            Assert.That(argsStr, Does.Not.Contain("--package="));
            Assert.That(options.Args, Does.Contain("--prefix"));
            Assert.That(options.Args, Does.Contain(tempDir));
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
        public void Constructor_WithPrefix_PackageJsonWithSingleDependency_IncludesSinglePackageFlag()
        {
            File.WriteAllText(
                Path.Combine(tempDir, "package.json"),
                """
                {
                    "dependencies": {
                        "typescript": "^5.0.0"
                    }
                }
                """);

            var options = new NpmOptions(prefix: tempDir, args: ["tsc"]);

            Assert.That(options.Args, Does.Contain("--package=typescript"));
            Assert.That(options.Args, Does.Contain("--"));
            Assert.That(options.Args, Does.Contain("tsc"));
        }

        [Test]
        public void Constructor_PackageFlagsAppearBeforeSeparator()
        {
            File.WriteAllText(
                Path.Combine(tempDir, "package.json"),
                """
                {
                    "dependencies": {
                        "pkg-a": "1.0.0",
                        "pkg-b": "2.0.0"
                    }
                }
                """);

            var options = new NpmOptions(prefix: tempDir, args: ["my-bin"]);

            var argsList = options.Args;
            var separatorIndex = argsList.IndexOf("--");
            var pkgAIndex = argsList.IndexOf("--package=pkg-a");
            var pkgBIndex = argsList.IndexOf("--package=pkg-b");

            Assert.That(separatorIndex, Is.GreaterThan(-1), "Args should contain '--' separator");
            Assert.That(pkgAIndex, Is.GreaterThan(-1), "Args should contain --package=pkg-a");
            Assert.That(pkgBIndex, Is.GreaterThan(-1), "Args should contain --package=pkg-b");
            Assert.That(pkgAIndex, Is.LessThan(separatorIndex), "--package flags should appear before '--'");
            Assert.That(pkgBIndex, Is.LessThan(separatorIndex), "--package flags should appear before '--'");
        }
    }
}
