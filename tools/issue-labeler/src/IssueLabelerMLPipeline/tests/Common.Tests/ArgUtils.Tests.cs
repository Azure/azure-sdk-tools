using Actions.Core;
using Actions.Core.Services;
using Actions.Core.Summaries;

namespace Common.Tests
{
    public class ArgUtilsTests
    {
        private class TestCoreService : ICoreService
        {
            private readonly Dictionary<string, string?> _inputs = new();

            public void SetInput(string name, string? value)
            {
                _inputs[name] = value;
            }

            public string? GetInput(string name)
            {
                return _inputs.TryGetValue(name, out var value) ? value : null;
            }

            string ICoreService.GetInput(string name, InputOptions? options) => GetInput(name)!;

            Summary ICoreService.Summary => throw new NotImplementedException();
            bool ICoreService.IsDebug => throw new NotImplementedException();
            public void WriteNotice(string message) { }
            ValueTask ICoreService.ExportVariableAsync(string name, string value) { throw new NotImplementedException(); }
            void ICoreService.SetSecret(string secret) { throw new NotImplementedException(); }
            ValueTask ICoreService.AddPathAsync(string inputPath) { throw new NotImplementedException(); }
            string[] ICoreService.GetMultilineInput(string name, InputOptions? options) { throw new NotImplementedException(); }
            bool ICoreService.GetBoolInput(string name, InputOptions? options) { throw new NotImplementedException(); }
            ValueTask ICoreService.SetOutputAsync<T>(string name, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>? typeInfo) { throw new NotImplementedException(); }
            void ICoreService.SetCommandEcho(bool enabled) { throw new NotImplementedException(); }
            void ICoreService.SetFailed(string message) { throw new NotImplementedException(); }
            void ICoreService.WriteDebug(string message) { throw new NotImplementedException(); }
            void ICoreService.WriteError(string message, AnnotationProperties? properties) { throw new NotImplementedException(); }
            void ICoreService.WriteWarning(string message, AnnotationProperties? properties) { throw new NotImplementedException(); }
            void ICoreService.WriteNotice(string message, AnnotationProperties? properties) { throw new NotImplementedException(); }
            void ICoreService.WriteInfo(string message) { throw new NotImplementedException(); }
            void ICoreService.StartGroup(string name) { throw new NotImplementedException(); }
            void ICoreService.EndGroup() { throw new NotImplementedException(); }
            ValueTask<T> ICoreService.GroupAsync<T>(string name, Func<ValueTask<T>> action) { throw new NotImplementedException(); }
            ValueTask ICoreService.SaveStateAsync<T>(string name, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>? typeInfo) { throw new NotImplementedException(); }
            string ICoreService.GetState(string name) { throw new NotImplementedException(); }
        }

        private readonly TestCoreService _testCoreService;
        private readonly Action<string?, ICoreService> _showUsage;

        public ArgUtilsTests()
        {
            _testCoreService = new TestCoreService();
            _showUsage = (message, action) => { };
        }

        [Fact]
        public void TryGetString_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testInput", "testValue");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetString("testInput", out var value);

            Assert.True(result);
            Assert.Equal("testValue", value);
        }

        [Fact]
        public void TryGetFlag_ShouldReturnTrue_WhenInputIsTrue()
        {
            _testCoreService.SetInput("testFlag", "true");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetFlag("testFlag", out var value);

            Assert.True(result);
            Assert.True(value);
        }

        [Fact]
        public void TryGetRepo_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("TEST_REPO", "TEST_ORG/TEST_REPO");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetRepo("TEST_REPO", out var org, out var repo);

            Assert.True(result);
            Assert.Equal("TEST_ORG", org);
            Assert.Equal("TEST_REPO", repo);
        }

        [Fact]
        public void TryGetPath_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testPath", "C:\\test\\path");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetPath("testPath", out var path);

            Assert.True(result);
            Assert.Equal(Path.GetFullPath("C:\\test\\path"), path);
        }

        [Fact]
        public void TryGetStringArray_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testArray", "value1,value2,value3");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetStringArray("testArray", out var values);

            Assert.True(result);
            Assert.Equal(new[] { "value1", "value2", "value3" }, values);
        }

        [Fact]
        public void TryGetInt_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testInt", "42");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetInt("testInt", out var value);

            Assert.True(result);
            Assert.Equal(42, value);
        }

        [Fact]
        public void TryGetIntArray_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testIntArray", "1,2,3");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetIntArray("testIntArray", out var values);

            Assert.True(result);
            Assert.Equal(new[] { 1, 2, 3 }, values);
        }

        [Fact]
        public void TryGetFloat_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testFloat", "3.14");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetFloat("testFloat", out var value);

            Assert.True(result);
            Assert.Equal(3.14f, value);
        }

        [Fact]
        public void TryGetNumberRanges_ShouldReturnTrue_WhenInputIsValid()
        {
            _testCoreService.SetInput("testRanges", "1-3,5,7-9");
            var argUtils = new ArgUtils(_testCoreService, _showUsage);

            var result = argUtils.TryGetNumberRanges("testRanges", out var values);

            Assert.True(result);
            Assert.Equal(new List<ulong> { 1, 2, 3, 5, 7, 8, 9 }, values);
        }
    }
}
