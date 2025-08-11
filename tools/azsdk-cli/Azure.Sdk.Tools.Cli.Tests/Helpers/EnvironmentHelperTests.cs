// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers
{
    internal class EnvironmentHelperTests
    {
        private TestLogger<EnvironmentHelper> logger;
        private EnvironmentHelper environmentHelper;

        [SetUp]
        public void Setup()
        {
            logger = new TestLogger<EnvironmentHelper>();
            environmentHelper = new EnvironmentHelper(logger);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up any environment variables we set during testing
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_BOOLEAN", null);
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_STRING", null);
            Environment.SetEnvironmentVariable("AZSDKTOOLS_AGENT_TESTING", null);
        }

        [Test]
        public void GetBooleanVariable_WithValidTrueValue_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_BOOLEAN", "true");
            var newHelper = new EnvironmentHelper(logger); // Create new instance to pick up the env var

            // Act
            var result = newHelper.GetBooleanVariable("AZSDKTOOLS_TEST_BOOLEAN");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void GetBooleanVariable_WithValidFalseValue_ReturnsFalse()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_BOOLEAN", "false");
            var newHelper = new EnvironmentHelper(logger);

            // Act
            var result = newHelper.GetBooleanVariable("AZSDKTOOLS_TEST_BOOLEAN");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        [TestCase("1", true)]
        [TestCase("0", false)]
        [TestCase("yes", true)]
        [TestCase("no", false)]
        [TestCase("YES", true)]
        [TestCase("NO", false)]
        [TestCase("on", true)]
        [TestCase("off", false)]
        [TestCase("enabled", true)]
        [TestCase("disabled", false)]
        public void GetBooleanVariable_WithVariousStringValues_ReturnsExpectedResult(string value, bool expected)
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_BOOLEAN", value);
            var newHelper = new EnvironmentHelper(logger);

            // Act
            var result = newHelper.GetBooleanVariable("AZSDKTOOLS_TEST_BOOLEAN");

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void GetBooleanVariable_WithInvalidValue_ReturnsFalse()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_BOOLEAN", "invalid");
            var newHelper = new EnvironmentHelper(logger);

            // Act
            var result = newHelper.GetBooleanVariable("AZSDKTOOLS_TEST_BOOLEAN", true);

            // Assert
            Assert.That(result, Is.False); // Should return false for invalid values
        }

        [Test]
        public void GetBooleanVariable_WithNonExistentVariable_ReturnsDefault()
        {
            // Act
            var result = environmentHelper.GetBooleanVariable("AZSDKTOOLS_NONEXISTENT", true);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void GetStringVariable_WithValidValue_ReturnsValue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST_STRING", "test-value");
            var newHelper = new EnvironmentHelper(logger);

            // Act
            var result = newHelper.GetStringVariable("AZSDKTOOLS_TEST_STRING");

            // Assert
            Assert.That(result, Is.EqualTo("test-value"));
        }

        [Test]
        public void GetStringVariable_WithNonExistentVariable_ReturnsDefault()
        {
            // Act
            var result = environmentHelper.GetStringVariable("AZSDKTOOLS_NONEXISTENT", "default-value");

            // Assert
            Assert.That(result, Is.EqualTo("default-value"));
        }

        [Test]
        public void GetEnvironmentVariables_ReturnsOnlyAzsdktoolsVariables()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST1", "value1");
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST2", "value2");
            Environment.SetEnvironmentVariable("OTHER_VAR", "should-not-appear");
            var newHelper = new EnvironmentHelper(logger);

            // Act
            var result = newHelper.GetEnvironmentVariables();

            // Assert
            Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.ContainsKey("AZSDKTOOLS_TEST1"), Is.True);
            Assert.That(result.ContainsKey("AZSDKTOOLS_TEST2"), Is.True);
            Assert.That(result.ContainsKey("OTHER_VAR"), Is.False);
            Assert.That(result["AZSDKTOOLS_TEST1"], Is.EqualTo("value1"));
            Assert.That(result["AZSDKTOOLS_TEST2"], Is.EqualTo("value2"));

            // Cleanup
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST1", null);
            Environment.SetEnvironmentVariable("AZSDKTOOLS_TEST2", null);
            Environment.SetEnvironmentVariable("OTHER_VAR", null);
        }

        [Test]
        public void GetEnvironmentVariables_IsCaseInsensitive()
        {
            // Arrange
            Environment.SetEnvironmentVariable("azsdktools_lowercase", "value");
            var newHelper = new EnvironmentHelper(logger);

            // Act
            var result = newHelper.GetEnvironmentVariables();

            // Assert
            Assert.That(result.ContainsKey("azsdktools_lowercase"), Is.True);
            Assert.That(result["azsdktools_lowercase"], Is.EqualTo("value"));

            // Cleanup
            Environment.SetEnvironmentVariable("azsdktools_lowercase", null);
        }
    }
}