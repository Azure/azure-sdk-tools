using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using Microsoft.TeamFoundation.Build.WebApi;
using Xunit;

namespace APIViewUnitTests
{
    public class AppTests
    {
        private string InputPath => Path.Combine("SampleTestFiles", "APIView.dll");

        private string OutputPath => Path.Combine(Path.GetTempPath(), "output.json");

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public void RunAsyncThrowsWithIncorrectNumberOfArgs(int length)
        {
            var args = new string[length];

            for (int i = 0; i < length; i++)
            {
                args[i] = InputPath;
            }

            Assert.ThrowsAsync<ArgumentException>(async () => await App.RunAsync(args));
        }

        [Fact]
        public void RunAsyncThrowsWithMissingInputFile()
        {
            var args = new string[]
            {
                "missing_file.dll",
                OutputPath
            };

            Assert.ThrowsAsync<FileNotFoundException>(async () => await App.RunAsync(args));
        }

        [Fact]
        public void RunAsyncThrowsWithMissingOutputFolder()
        {
            var args = new string[]
            {
                InputPath,
                Path.Combine("missing_folder", "output.json")
            };

            Assert.ThrowsAsync<FolderNotFoundException>(async () => await App.RunAsync(args));
        }

        [Fact]
        public async Task RunAsyncGeneratesOutputFile()
        {
            var args = new string[] { InputPath, OutputPath };

            await App.RunAsync(args);

            var output = await File.ReadAllTextAsync(OutputPath);

            var assemblySymbol = CompilationFactory.GetCompilation(InputPath);
            var codeNode = new CodeFileBuilder().Build(assemblySymbol, false, null);
            using var stream = new MemoryStream();

            await codeNode.SerializeAsync(stream);

            var expectedOutput = Encoding.UTF8.GetString(stream.ToArray());

            Assert.Equal(expectedOutput, output);
        }
    }
}
