using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.TestProxy.Common;
using Xunit;

namespace Azure.Sdk.Tools.TestProxy.Tests
{
    public class ShortHashGeneratorTests
    {

        [Theory]
        [InlineData("sdk/t/assets.json", "sdk/t/assets.json", true)]
        [InlineData("1", "1", true)]
        [InlineData("11", "12", false)]
        [InlineData("sdk/communication/azure-communication-tables/assets.json", "sdk/communication/azure-communication-tableś/assets.json", false)]
        [InlineData("sdk/communication/assets.json", "sdk/ommunication/assets.json", false)]
        public void TestShortHashGenerator(string inputString1, string inputString2, bool shouldMatch)
        {
            String shortHash1 = ShortHashGenerator.GenerateShortHash(inputString1);
            String shortHash2 = ShortHashGenerator.GenerateShortHash(inputString2);
            if (shouldMatch)
            {
                Assert.Equal(shortHash1, shortHash2);
            }
            else
            {
                Assert.NotEqual(shortHash1, shortHash2);
            }
        }

        [Theory]
        [InlineData("sdk/t/assets.json", 1)]
        [InlineData("1", 5)]
        [InlineData("11", 10)]
        [InlineData("sdk/communication/azure-communication-tables/assets.json", 20)]
        [InlineData("abcdeghijklmnop", 27)]
        public void TestShortHashGeneratorDifferentLength(string inputString1, int hashLen)
        {
            String shortHash1 = ShortHashGenerator.GenerateShortHash(inputString1, hashLen);
            Assert.Equal(shortHash1.Length, hashLen);
        }

        [Theory]
        [InlineData("sdk/t/assets.json", 0)]
        [InlineData("sdk/t/assets.json", 29)]

        public void TestShortHashGeneratorInvalidRequestedLength(string inputString1, int hashLen)
        {
            Action action = () => ShortHashGenerator.GenerateShortHash(inputString1, hashLen);
            ArgumentException argumentException = Assert.Throws<ArgumentException>(action);
            Assert.Equal("returnHashLength must be > 1 and <= 28", argumentException.Message);
        }

        [Theory]
        [InlineData("sdk/t/assets.json", 28)]
        public void TestShortHashGeneratorShorterReturnLength(string inputString1, int hashLen)
        {
            Action action = () => ShortHashGenerator.GenerateShortHash(inputString1, hashLen);
            ArgumentException argumentException = Assert.Throws<ArgumentException>(action);
            Assert.StartsWith($"GenerateShortHash of {inputString1} does not produce a return string", argumentException.Message);
        }
    }
}
