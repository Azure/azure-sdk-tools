using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.CommandOptions;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class InvocationTests
    {
        [Theory]
        [InlineData("start", "-i", "-d")]
        [InlineData("start")]
        [InlineData("start", "--insecure", "-d")]
        [InlineData("start", "--dump")]
        [InlineData("start", "--dump", "--", "--urls", "https://localhost:8002")]
        public async Task TestBasicServerInvocations(params string[] input)
        {
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is StartOptions);
            Assert.Equal(0, exitCode);

            if (input.Contains("-i")|| input.Contains("--insecure"))
            {
                Assert.True(((StartOptions)obj).Insecure);
            }
            else
            {
                Assert.False(((StartOptions)obj).Insecure);
            }

            if (input.Contains("--dump") || input.Contains("-d"))
            {
                Assert.True(((StartOptions)obj).Dump);
            }
            else
            {
                Assert.False(((StartOptions)obj).Dump);
            }
        }

        [Theory]
        [InlineData("push", "-a", "path/to/assets.json")]
        [InlineData("push", "--assets-json-path", "path/to/assets.json")]
        public async Task TestAssetsOptions(params string[] input)
        {
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.Equal(0, exitCode);
            Assert.True(obj is PushOptions);
            Assert.Equal("path/to/assets.json", ((PushOptions)obj).AssetsJsonPath);
        }

        [Theory]
        [InlineData("start", "-l", "C:/repo/sdk-for-python")]
        [InlineData("start", "--storage-location", "C:/repo/sdk-for-python")]
        public async Task TestStorageLocationOptions(params string[] input)
        {
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.Equal(0, exitCode);
            Assert.True(obj is StartOptions);
            Assert.Equal("C:/repo/sdk-for-python", ((StartOptions)obj).StorageLocation);
        }

        [Theory]
        [InlineData("push", "-a", "path/to/assets.json", "-p", "BlobStore")]
        [InlineData("push", "-a", "path/to/assets.json", "--storage-plugin", "BlobStore")]
        public async Task TestStoragePluginOptions(params string[] input)
        {
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.Equal(0, exitCode);
            Assert.True(obj is PushOptions);
            Assert.Equal("BlobStore", ((PushOptions)obj).StoragePlugin);
        }

        [Fact]
        public async Task TestServerInvocationsHonorUnmatched()
        {
            string[] input = new string[] { "start", "--dump", "--", "--urls", "https://localhost:8002" };

            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is StartOptions);
            Assert.Equal(new string[] { "--urls", "https://localhost:8002" }, ((StartOptions)obj).AdditionalArgs);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task TestConfig()
        {
            string[] input = new string[] { "config" };

            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigOptions);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task TestConfigShow()
        {
            string[] input = new string[] { "config", "show", "-a", "path/to/assets.json" };

            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigShowOptions);
            Assert.Equal("path/to/assets.json", ((ConfigShowOptions)obj).AssetsJsonPath);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task TestConfigCreate()
        {
            string[] input = new string[] { "config", "create", "-a", "path/to/assets.json" };

            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigCreateOptions);
            Assert.Equal("path/to/assets.json", ((ConfigCreateOptions)obj).AssetsJsonPath);
            Assert.Equal(0, exitCode);
        }

        [Fact]
        public async Task TestConfigLocate()
        {
            string[] input = new string[] { "config", "locate", "-a", "path/to/assets.json" };

            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigLocateOptions);
            Assert.Equal("path/to/assets.json", ((ConfigLocateOptions)obj).AssetsJsonPath);
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("config", "invalid-verb")]
        [InlineData("totally-invalid-verb")]

        public async Task TestInvalidVerbCombinations(params string[] input)
        {
            var output = new StringWriter();
            System.Console.SetOut(output);
            var obj = string.Empty;
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = "Invoked";

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.NotEqual("Invoked", obj);
            Assert.Equal(1, exitCode);
        }

        [Theory]
        [InlineData("push", "-a", "path/to/assets.json")]
        public async Task TestPushOptions(params string[] input)
        {
            var output = new StringWriter();
            System.Console.SetOut(output);
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is PushOptions);
            Assert.Equal("path/to/assets.json", ((PushOptions)obj).AssetsJsonPath);
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("restore", "-a", "path/to/assets.json")]

        public async Task TestRestoreOptions(params string[] input)
        {
            var output = new StringWriter();
            System.Console.SetOut(output);
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            Assert.True(obj is RestoreOptions);
            Assert.Equal("path/to/assets.json", ((RestoreOptions)obj).AssetsJsonPath);
            Assert.Equal(0, exitCode);
        }

        [Theory]
        [InlineData("reset", "-a", "path/to/assets.json")]
        [InlineData("reset", "-y", "-a", "path/to/assets.json")]
        [InlineData("reset", "--yes", "-a", "path/to/assets.json")]
        public async Task TestResetOptions(params string[] input)
        {
            var output = new StringWriter();
            System.Console.SetOut(output);
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);

            if (input.Contains("--yes") || input.Contains("-y"))
            {
                Assert.True(((ResetOptions)obj).ConfirmReset);
            }
            else
            {
                Assert.False(((ResetOptions)obj).ConfirmReset);
            }

            Assert.True(obj is ResetOptions);
            Assert.Equal("path/to/assets.json", ((ResetOptions)obj).AssetsJsonPath);
            Assert.Equal(0, exitCode);
        }


        [Fact]
        public async Task TestPushOptionsErrorsWithNoPath()
        {
            string[] input = new string[] { "push" };

            var output = new StringWriter();
            System.Console.SetOut(output);
            var obj = new object();
            var rootCommand = OptionsGenerator.GenerateCommandLineOptions((DefaultOptions) =>
            {
                obj = DefaultOptions;

                return Task.CompletedTask;
            });
            var exitCode = await rootCommand.InvokeAsync(input);
            Assert.Equal(1, exitCode);
        }
    }
}
