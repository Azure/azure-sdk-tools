using System.Collections.Generic;
using System.Linq;
using ApiView;
using APIView.Model.V2;
using APIViewWeb;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Moq;
using Xunit;

namespace APIViewUnitTests;

public class CodeFileManagerTests
{
    private readonly ICodeFileManager _codeFileManager;

    public CodeFileManagerTests()
    {
        IEnumerable<LanguageService> languageServices = new List<LanguageService>();
        IDevopsArtifactRepository devopsArtifactRepository = new Mock<IDevopsArtifactRepository>().Object;
        IBlobCodeFileRepository blobCodeFileRepository = new Mock<IBlobCodeFileRepository>().Object;
        IBlobOriginalsRepository blobOriginalRepository = new Mock<IBlobOriginalsRepository>().Object;

        _codeFileManager = new CodeFileManager(
            languageServices,
            blobCodeFileRepository,
            blobOriginalRepository,
            devopsArtifactRepository);
    }

    [Theory]
    [InlineData(new[] { "line1", "line2", "line3" },  new string[] { })] // No duplicates
    [InlineData(new[] { "line1", "line2", "line1", "line3" },  new[] { "line1" })] // Has duplicates
    [InlineData(new[] { "line1", "line2", "line1", "line2", "line3" }, new[] { "line1", "line2" })] // Multiple duplicates, returns all
    [InlineData(new[] { "", "line1", "", "line2" },  new string[] { })] // Empty line IDs ignored
    [InlineData(new[] { "Line1", "line1", "LINE1" },  new string[] { })] // Case sensitive - no duplicates
    public void AreLineIdsDuplicate_VariousScenarios_ReturnsExpectedResult(string[] lineIds, string[] expectedDuplicateId)
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(lineIds);
        List<string> result = _codeFileManager.GetDuplicateLineIds(codeFile);
        Assert.Equal(expectedDuplicateId, result);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithNullLineIds_ReturnsFalse()
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(new[] { null, "line1", null, "line2" });
        List<string> result = _codeFileManager.GetDuplicateLineIds(codeFile);
        Assert.Empty(result);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithMixedEmptyAndValidIds_IgnoresEmptyIds()
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(new[] { "", "line1", null, "line1", "" });
        List<string> result = _codeFileManager.GetDuplicateLineIds(codeFile);
        Assert.True(result.Count > 0);
        Assert.Equal("line1", result[0]);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithMultipleDuplicates_ReturnsAllDuplicateIds()
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(new[] { "line1", "line2", "line3", "line1", "line2", "line4" });
        List<string> result = _codeFileManager.GetDuplicateLineIds(codeFile);
        Assert.Equal(new[] { "line1", "line2" }, result);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithNoApiLines_ReturnsFalse()
    {
        CodeFile codeFile = new() { Language = "C#", Name = "Test", PackageName = "TestPackage" };
        List<string> result = _codeFileManager.GetDuplicateLineIds(codeFile);
        Assert.Empty(result);
    }


    private CodeFile CreateCodeFileWithLineIds(string[] lineIds)
    {
        List<ReviewLine> reviewLines = lineIds.Select((id, index) => new ReviewLine
        {
            LineId = id, Tokens = new List<ReviewToken> { new() { Value = $"Text for line {index}" } }
        }).ToList();

        CodeFile codeFile = new()
        {
            Language = "C#", Name = "TestFile", PackageName = "TestPackage", ReviewLines = reviewLines
        };

        return codeFile;
    }
}
