using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using Microsoft.TeamFoundation.Build.WebApi;
using Xunit;

namespace APIViewUnitTests
{
    public class ProgramTests
    {
        public static IEnumerable<object[]> ExactFormattingFiles
        {
            get
            {
                var assembly = typeof(CodeFileBuilderTests).Assembly;
                return assembly.GetManifestResourceNames()
                    .Where(r => r.Contains("ExactFormatting"))
                    .Select(r => new object[] { r })
                    .ToArray();
            }
        }

        private string InputPath => Path.Combine(Path.GetTempPath(), "input.dll");

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

            Assert.ThrowsAsync<ArgumentException>(async () => await Program.RunAsync(args));
        }

        [Fact]
        public void RunAsyncThrowsWithMissingInputFile()
        {
            var args = new string[]
            {
                "missing_file.dll",
                OutputPath
            };

            Assert.ThrowsAsync<FileNotFoundException>(async () => await Program.RunAsync(args));
        }

        [Fact]
        public void RunAsyncThrowsWithMissingOutputFolder()
        {
            var args = new string[]
            {
                InputPath,
                Path.Combine("missing_folder", "output.json")
            };

            Assert.ThrowsAsync<FolderNotFoundException>(async () => await Program.RunAsync(args));
        }

        [Theory]
        [MemberData(nameof(ExactFormattingFiles))]
        public async Task RunAsyncGeneratesOutputFile(string name)
        {
            var manifestResourceStream = typeof(CodeFileBuilderTests).Assembly.GetManifestResourceStream(name);
            using var streamReader = new StreamReader(manifestResourceStream);
            var code = streamReader.ReadToEnd();

            using (var fileStream = new FileStream(InputPath, FileMode.Create, FileAccess.Write))
            {
                await Common.BuildDllAsync(fileStream, code);
            }

            var args = new string[] { InputPath, OutputPath };

            await Program.RunAsync(args);

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
