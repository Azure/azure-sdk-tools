using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.CommandOptions;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class InvocationTests
    {
        /*
should print help

--help
start --help
coolion
         * */
        public Task WorkCallback(object obj)
        {
            return Task.CompletedTask;
        }

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
            await rootCommand.InvokeAsync(input);

            Assert.True(obj is StartOptions);

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
            await rootCommand.InvokeAsync(input);

            Assert.True(obj is StartOptions);
            Assert.Equal(new string[] { "--urls", "https://localhost:8002" }, ((StartOptions)obj).AdditionalArgs);
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
            await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigOptions);
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
            await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigShowOptions);
            Assert.Equal("path/to/assets.json", ((ConfigShowOptions)obj).AssetsJsonPath);
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
            await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigCreateOptions);
            Assert.Equal("path/to/assets.json", ((ConfigCreateOptions)obj).AssetsJsonPath);
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
            await rootCommand.InvokeAsync(input);

            Assert.True(obj is ConfigLocateOptions);
            Assert.Equal("path/to/assets.json", ((ConfigLocateOptions)obj).AssetsJsonPath);
        }
    }
}
