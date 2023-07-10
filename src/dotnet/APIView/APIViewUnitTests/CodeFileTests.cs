using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using Xunit;

namespace APIViewUnitTests
{
    public class CodeFileTests
    {
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
                    Assert.Equal(88, item.Count());
                });
        }
    }
}
