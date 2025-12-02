namespace Common.Tests
{
    public class ArgUtilsTests
    {
        private readonly Action<string?> _showUsage;
        private string? _lastUsageMessage;

        public ArgUtilsTests()
        {
            _showUsage = (message) => { _lastUsageMessage = message; };
        }

        private ArgUtils CreateArgUtils(params string[] args)
        {
            return new ArgUtils(_showUsage, new Queue<string>(args));
        }

        [Fact]
        public void TryGetString_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("testValue");

            var result = argUtils.TryGetString("testInput", out var value);

            Assert.True(result);
            Assert.Equal("testValue", value);
        }

        [Fact]
        public void TryGetFlag_ShouldReturnTrue_WhenInputIsTrue()
        {
            var argUtils = CreateArgUtils("true");

            var result = argUtils.TryGetFlag("testFlag", out var value);

            Assert.True(result);
            Assert.True(value);
        }

        [Fact]
        public void TryGetRepo_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("TEST_ORG/TEST_REPO");

            var result = argUtils.TryGetRepo("TEST_REPO", out var org, out var repo);

            Assert.True(result);
            Assert.Equal("TEST_ORG", org);
            Assert.Equal("TEST_REPO", repo);
        }

        [Fact]
        public void TryGetPath_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("C:\\test\\path");

            var result = argUtils.TryGetPath("testPath", out var path);

            Assert.True(result);
            Assert.Equal(Path.GetFullPath("C:\\test\\path"), path);
        }

        [Fact]
        public void TryGetStringArray_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("value1,value2,value3");

            var result = argUtils.TryGetStringArray("testArray", out var values);

            Assert.True(result);
            Assert.Equal(new[] { "value1", "value2", "value3" }, values);
        }

        [Fact]
        public void TryGetInt_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("42");

            var result = argUtils.TryGetInt("testInt", out var value);

            Assert.True(result);
            Assert.Equal(42, value);
        }

        [Fact]
        public void TryGetIntArray_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("1,2,3");

            var result = argUtils.TryGetIntArray("testIntArray", out var values);

            Assert.True(result);
            Assert.Equal(new[] { 1, 2, 3 }, values);
        }

        [Fact]
        public void TryGetFloat_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("3.14");

            var result = argUtils.TryGetFloat("testFloat", out var value);

            Assert.True(result);
            Assert.Equal(3.14f, value);
        }

        [Fact]
        public void TryGetNumberRanges_ShouldReturnTrue_WhenInputIsValid()
        {
            var argUtils = CreateArgUtils("1-3,5,7-9");

            var result = argUtils.TryGetNumberRanges("testRanges", out var values);

            Assert.True(result);
            Assert.Equal(new List<ulong> { 1, 2, 3, 5, 7, 8, 9 }, values);
        }
    }
}
