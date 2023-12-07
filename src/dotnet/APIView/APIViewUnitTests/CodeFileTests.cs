using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApiView;
using APIViewWeb.Hubs;
using APIViewWeb.Managers;
using APIViewWeb;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;
using Xunit.Abstractions;

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

            _codeFileManager = new CodeFileManager(
            languageServices: languageServices, codeFileRepository: blobCodeFileRepository,
            originalsRepository: blobOriginalRepository, devopsArtifactRepository: devopsArtifactRepository);
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
                    Assert.Equal(88, item.Count());
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
    }
}
