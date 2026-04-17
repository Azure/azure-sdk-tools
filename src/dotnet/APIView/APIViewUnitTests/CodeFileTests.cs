using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView.Model.V2;
using APIView.TreeToken;
using APIViewWeb;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace APIViewUnitTests
{
    public class CodeFileTests
    {
        private readonly ICodeFileManager _codeFileManager;

        public CodeFileTests()
        {
            IEnumerable<LanguageService> languageServices = new List<LanguageService>();
            IDevopsArtifactRepository devopsArtifactRepository = new Mock<IDevopsArtifactRepository>().Object;
            IBlobCodeFileRepository blobCodeFileRepository = new Mock<IBlobCodeFileRepository>().Object;
            IBlobOriginalsRepository blobOriginalRepository = new Mock<IBlobOriginalsRepository>().Object;

            var logger = new Mock<ILogger<CodeFileManager>>().Object;
            _codeFileManager = new CodeFileManager(languageServices, blobCodeFileRepository, blobOriginalRepository,
                devopsArtifactRepository, logger);
        }

        [Fact]
        public async Task Deserialize_Splits_CodeFile_Into_Sections()
        {
            // Arrange
            CodeFile codeFile = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "TokenFileWithSectionsRevision2.json");
            FileInfo fileInfo = new FileInfo(filePath);
            FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            //Act
            codeFile = await CodeFile.DeserializeAsync(fileStream, true);

            //Assert
            Assert.Equal(41, codeFile.Tokens.Count());
            Assert.Collection(codeFile.LeafSections,
                item => {
                    Assert.Equal(35, item.Count());
                },
                item => {
                    Assert.Equal(88, item.Count());
                },
                item => {
                    Assert.Equal(40, item.Count());
                },
                item => {
                    Assert.Equal(87, item.Count());
                });
        }

        [Fact]
        public async Task AreCodeFilesTheSame_Returns_True_For_Same_CodeFile()
        {
            // Arrange
            CodeFile codeFileA = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "TokenFileWithSectionsRevision2.json");
            FileInfo fileInfo = new FileInfo(filePath);
            FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream, true);

            //Act
            bool result = _codeFileManager.AreCodeFilesTheSame(codeFileA, codeFileA);

            //Assert
            Assert.True(result);
        }

        [Fact]
        public async Task AreCodeFilesTheSame_Returns_False_For_Different_CodeFile()
        {
            // Arrange
            CodeFile codeFileA = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "TokenFileWithSectionsRevision2.json");
            FileInfo fileInfo = new FileInfo(filePath);
            FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream, true);

            CodeFile codeFileB = new CodeFile();
            var filePathB = Path.Combine("SampleTestFiles", "TokenFileWithSectionsRevision3.json");
            FileInfo fileInfoB = new FileInfo(filePath);
            FileStream fileStreamB = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStreamB, true);

            //Act
            bool result = _codeFileManager.AreCodeFilesTheSame(codeFileA, codeFileB);

            //Assert
            Assert.False(result);
        }

        [Fact]
        public async Task TestCodeFileConversion()
        {
            var codeFileA = new CodeFile();
            var codeFileB = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "Azure.Template.cpp.json");
            var fileInfo = new FileInfo(filePath);
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream);

            codeFileB = new CodeFile();
            filePath = Path.Combine("SampleTestFiles", "Azure.Template.cpp_new.json");
            fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStream);

            codeFileA.ConvertToTreeTokenModel();
            bool result = CodeFileHelpers.AreCodeFilesSame(codeFileA, codeFileB);
            Assert.True(result);
        }

        [Fact]
        public async Task TestCodeFileComparisonWithSkippedLines()
        {
            var codeFileA = new CodeFile();
            var codeFileB = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "app-conf.json");
            var fileInfo = new FileInfo(filePath);
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream);

            filePath = Path.Combine("SampleTestFiles", "app-conf_without_skip_diff.json");
            fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStream);

            bool isSame = CodeFileHelpers.AreCodeFilesSame(codeFileA, codeFileB);
            Assert.True(isSame);

            var diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            Assert.False(FindAnyDiffLine(diff));
        }

        private bool FindAnyDiffLine(List<ReviewLine> lines)
        {
            if(lines == null || lines.Count == 0)
            {
                return false;
            }

            foreach (var line in lines)
            {
                if (line.DiffKind != DiffKind.NoneDiff || FindAnyDiffLine(line.Children))
                {
                    return true;
                }
            }
            return false;
        }

        [Fact]
        public async Task TestCodeFileComparisonWithChangeInSkippedLines()
        {
            var codeFileA = new CodeFile();
            var codeFileB = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "app-conf.json");
            var fileInfo = new FileInfo(filePath);
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream);

            filePath = Path.Combine("SampleTestFiles", "app-conf-change-in-skipped-diff.json");
            fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStream);

            bool isSame = CodeFileHelpers.AreCodeFilesSame(codeFileA, codeFileB);
            Assert.True(isSame);

            var diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            Assert.False(FindAnyDiffLine(diff));
        }

        [Fact]
        public async Task VerifyPythonDiff()
        {
            var codeFileA = new CodeFile();
            var codeFileB = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "azure-schemaregistry_python.json");
            var fileInfo = new FileInfo(filePath);
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream);
            filePath = Path.Combine("SampleTestFiles", "azure-schemaregistry_python_diff.json");
            fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStream);
            bool isSame = CodeFileHelpers.AreCodeFilesSame(codeFileA, codeFileB);
            Assert.False(isSame);

            var diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            Assert.True(FindAnyDiffLine(diff));

            //Verify first line in diff view is the global text
            Assert.True(diff.First().LineId == "GLOBAL");

            //Verify that last line of namespace line's children is empty line
            var namespaceLine = diff.First(l=>l.LineId == "azure.schemaregistry");
            Assert.Equal("namespace azure.schemaregistry", namespaceLine.ToString());
            var lastClass = namespaceLine.Children.Last();
            Assert.True(lastClass.Children.Last().Tokens.Count == 0);

        }

        [Fact]
        public async Task VerifyPythonGroupedDiff()
        {
            var codeFileA = new CodeFile();
            var codeFileB = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "azure-schemaregistry_python-with-overload_relatedto-NEW.json");
            var fileInfo = new FileInfo(filePath);
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream);
            filePath = Path.Combine("SampleTestFiles", "azure-schemaregistry_python-with-overload_relatedto-OLD.json");
            fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStream);
            bool isSame = CodeFileHelpers.AreCodeFilesSame(codeFileA, codeFileB);
            Assert.False(isSame);

            var diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            Assert.True(FindAnyDiffLine(diff));

            //Verify first line in diff view is the global text
            Assert.True(diff.First().LineId == "GLOBAL");

            //Verify that last line of namespace line's children is empty line
            var namespaceLine = diff.First(l => l.LineId == "azure.schemaregistry");
            Assert.Equal("namespace azure.schemaregistry", namespaceLine.ToString());

            // Make sure first 3 lines under schema registry with LineID are  "{def azure.schemaregistry.foo(}"
            int i = 0, line = 0;
            while (line < namespaceLine.Children.Count && i < 3)
            {
                if (!string.IsNullOrEmpty(namespaceLine.Children[line].LineId))
                {
                    Assert.True(namespaceLine.Children[line].ToString() == "def azure.schemaregistry.foo(");
                    i++;
                }
                line++;
            }
        }

        [Fact]
        public async Task VerifyJavaDiffInContextEnd()
        {
            var codeFileA = new CodeFile();
            var codeFileB = new CodeFile();
            var filePath = Path.Combine("SampleTestFiles", "azure-storage-file-share_active.json");
            var fileInfo = new FileInfo(filePath);
            var fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileA = await CodeFile.DeserializeAsync(fileStream);
            filePath = Path.Combine("SampleTestFiles", "azure-storage-file-share_baseline.json");
            fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFileB = await CodeFile.DeserializeAsync(fileStream);
            bool isSame = CodeFileHelpers.AreCodeFilesSame(codeFileA, codeFileB);
            Assert.False(isSame);

            var diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            Assert.True(FindAnyDiffLine(diff));

            //Verify that all added or removed class or start of context has a corresponding end of context marked as added or removed.
            foreach (var namespaceLine in diff.Where(l => l.Tokens.Any(t=>t.Value == "package")))
            {
                bool foundAddedContext = false;
                bool foundRemovedContext = false;
                string previousLineId = "";
                foreach(var objectDefLine in namespaceLine.Children)
                {
                    //If previous line is start of context and added line then current line should also be marked as added
                    if (foundAddedContext)
                    {
                        Assert.True(objectDefLine.DiffKind == DiffKind.Added && objectDefLine.RelatedToLine == previousLineId);
                        foundAddedContext = false;
                        foundRemovedContext = false;
                        previousLineId = "";
                        continue;
                    }

                    if (foundRemovedContext)
                    {
                        Assert.True(objectDefLine.DiffKind == DiffKind.Removed);
                        foundAddedContext = false;
                        foundRemovedContext = false;
                        previousLineId = "";
                        continue;
                    }

                    if (objectDefLine.DiffKind == DiffKind.Added && objectDefLine.IsContextEndLine != true)
                    {
                        foundAddedContext = true;
                        foundRemovedContext = false;
                        previousLineId = objectDefLine.LineId;
                    }

                    if (objectDefLine.DiffKind == DiffKind.Removed && objectDefLine.IsContextEndLine != true)
                    {
                        foundAddedContext = false;
                        foundRemovedContext = true;
                        previousLineId = objectDefLine.LineId;
                    }
                }
            }
        }

        [Fact]
        public async Task VerifyAzureAiEvaluationDiff()
        {
            (CodeFile codeFileA, CodeFile codeFileB) = await LoadCodeFilesPairAsync(
                "azure-ai-evaluation-1.11.0.json",
                "azure-ai-evaluation-1.9.0.json");

            string expectedDiffPath = Path.Combine("SampleTestFiles", "azure-ai-evaluation-diff.txt");
            string expectedDiff = await File.ReadAllTextAsync(expectedDiffPath);

            Stopwatch sw = Stopwatch.StartNew();
            List<ReviewLine> diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            TimeSpan diffTime = sw.Elapsed;
            string actualDiff = ConvertDiffToText(diff);

            Assert.True(FindAnyDiffLine(diff), "Diff should contain changes");
            Assert.Equal(expectedDiff.Replace("\r\n", "\n"), actualDiff.Replace("\r\n", "\n"));
            Assert.True(diffTime < TimeSpan.FromSeconds(30), $"Diff calculation too slow: {diffTime.TotalSeconds:F2}s");
        }

        [Fact]
        public async Task VerifyAzureDataAppConfigurationDiff()
        {
            (CodeFile codeFileA, CodeFile codeFileB) = await LoadCodeFilesPairAsync(
                "azure.data.appconfiguration.1.7.0.json",
                "azure.data.appconfiguration.1.5.0.json");

            string expectedDiffPath = Path.Combine("SampleTestFiles", "azure.data.appconfiguration.diff.txt");
            string expectedDiff = await File.ReadAllTextAsync(expectedDiffPath);

            Stopwatch sw = Stopwatch.StartNew();
            List<ReviewLine> diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            TimeSpan diffTime = sw.Elapsed;
            string actualDiff = ConvertDiffToText(diff);

            Assert.True(FindAnyDiffLine(diff), "Diff should contain changes");
            Assert.Equal(expectedDiff.Replace("\r\n", "\n"), actualDiff.Replace("\r\n", "\n"));
            Assert.True(diffTime < TimeSpan.FromSeconds(30), $"Diff calculation too slow: {diffTime.TotalSeconds:F2}s");
        }

        [Fact]
        public async Task VerifyAzureAiAgentsDiff()
        {
            (CodeFile codeFileA, CodeFile codeFileB) = await LoadCodeFilesPairAsync(
                "azure-ai-agents-1.0.0b1.json",
                "azure-ai-agents-1.0.0b1-old.json");

            string expectedDiffPath = Path.Combine("SampleTestFiles", "azure-ai-agents-diff.txt");
            string expectedDiff = await File.ReadAllTextAsync(expectedDiffPath);

            Stopwatch sw = Stopwatch.StartNew();
            List<ReviewLine> diff = CodeFileHelpers.FindDiff(codeFileA.ReviewLines, codeFileB.ReviewLines);
            TimeSpan diffTime = sw.Elapsed;

            string actualDiff = ConvertDiffToText(diff);

            Assert.True(FindAnyDiffLine(diff), "Diff should contain changes");
            Assert.Equal(expectedDiff.Replace("\r\n", "\n"), actualDiff.Replace("\r\n", "\n"));
            Assert.True(diffTime < TimeSpan.FromSeconds(30), $"Diff calculation too slow: {diffTime.TotalSeconds:F2}s");
        }

        private static string ConvertDiffToText(List<ReviewLine> diffLines)
        {
            StringBuilder sb = new();
            ConvertDiffLinesToText(diffLines, sb, 0);
            return sb.ToString();
        }

        private static void ConvertDiffLinesToText(List<ReviewLine> lines, StringBuilder sb, int indent)
        {
            string indentSpaces = "  ".PadRight(indent * 2);

            foreach (ReviewLine line in lines)
            {
                string lineText = line.ToString();

                if (string.IsNullOrWhiteSpace(lineText) && line.Children.Count == 0)
                {
                    sb.AppendLine();
                    continue;
                }

                string prefix = line.DiffKind switch
                {
                    DiffKind.Added => "+ ",
                    DiffKind.Removed => "- ",
                    _ => "  "
                };

                if (!string.IsNullOrWhiteSpace(lineText) || line.Children.Count > 0)
                {
                    sb.Append(indentSpaces);
                    sb.Append(prefix);
                    sb.AppendLine(lineText);
                }

                if (line.Children.Count > 0)
                {
                    ConvertDiffLinesToText(line.Children, sb, indent + 1);
                }
            }
        }

        private async Task<(CodeFile fileA, CodeFile fileB)> LoadCodeFilesPairAsync(string fileAName, string fileBName)
        {
            CodeFile fileA;
            CodeFile fileB;

            string filePathA = Path.Combine("SampleTestFiles", fileAName);
            await using (var streamA = new FileInfo(filePathA).OpenRead())
            {
                fileA = await CodeFile.DeserializeAsync(streamA);
            }

            string filePathB = Path.Combine("SampleTestFiles", fileBName);
            await using (var streamB = new FileInfo(filePathB).OpenRead())
            {
                fileB = await CodeFile.DeserializeAsync(streamB);
            }

            return (fileA, fileB);
        }


    }
}
