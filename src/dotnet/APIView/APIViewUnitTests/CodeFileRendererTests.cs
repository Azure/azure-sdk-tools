using System;
using System.IO;
using System.Linq;
using ApiView;
using APIViewWeb.Models;
using Xunit;

namespace APIViewUnitTests
{
    public class CodeFileRendererTests : IDisposable
    {
        CodeFile codeFile;
        FileStream fileStream;
        RenderedCodeFile renderedCodeFile;

        public CodeFileRendererTests()
        {
            codeFile = new CodeFile();
            string filePath = Path.Combine("SampleTestFiles", "TokenFileWithSectionsRevision2.json");
            FileInfo fileInfo = new FileInfo(filePath);
            fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            codeFile = CodeFile.DeserializeAsync(fileStream, true).Result;
            renderedCodeFile = new RenderedCodeFile(codeFile);
            renderedCodeFile.Render(false);
        }

        public void Dispose()
        {
            fileStream.Dispose();
        }

        [Fact]
        public void RenderedResult_Sections_Correct_CodeLines()
        {
            var codeLines = renderedCodeFile.RenderResult.CodeLines;

            Assert.Equal(5, codeLines.Length);
            Assert.Equal(1, codeLines[0].LineNumber);
            Assert.Equal(2, codeLines[1].LineNumber);
            Assert.Equal(15, codeLines[2].LineNumber);
            Assert.Equal(16, codeLines[3].LineNumber);
            Assert.Equal(60, codeLines[4].LineNumber);
        }

        [Fact]
        public void RenderedResult_Sections_Has_Detached_Leafs()
        {
            var sections = renderedCodeFile.RenderResult.Sections;
            Assert.Equal(3, sections.Count());
        }

        [Theory]
        [InlineData(0, 3, 14)]
        [InlineData(1, 17, 59)]
        [InlineData(2, 61, 87)]
        public void RenderedResult_Sections_Has_Correct_Lines_In_Sections(int sectionPosition, int firstLineNumber, int lastLineNumber)
        {
            var sectionKey = renderedCodeFile.RenderResult.Sections[sectionPosition].Data.SectionKey;
            var codeLines = renderedCodeFile.GetCodeLineSection((int)sectionKey);

            Assert.Equal(firstLineNumber, codeLines[0].LineNumber);
            Assert.Equal(lastLineNumber, codeLines[codeLines.Length - 1].LineNumber);

            int currLine = firstLineNumber;
            foreach (var line in codeLines)
            {
                Assert.Equal(currLine, line.LineNumber);
                currLine++;
            }
        }
    }
}
