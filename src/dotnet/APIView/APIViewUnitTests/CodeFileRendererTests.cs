using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using ApiView;
using APIView;
using APIView.Model;
using Xunit;

namespace APIViewUnitTests
{
    public class CodeFileRendererTests
    {
        [Fact]
        public async Task Render_Sample_CodeFile_With_Sections_Returns_LeafLess_Tree()
        {
            // Arrange
            CodeFile codeFile = new CodeFile();
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
            var filePath = Path.Combine("SampleTestFiles", "SampleCodeFileWithSections.json");
            FileInfo fileInfo = new FileInfo(filePath);
            FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

            //Act
            codeFile = await CodeFile.DeserializeAsync(fileStream, true);
            var result = codeFileRenderer.Render(codeFile);
            var codeLines = result.CodeLines;
            var sections = result.Sections;

            Assert.Equal(5, codeLines.Length);
            Assert.Equal(3, sections.Count());
        }
    }
}
