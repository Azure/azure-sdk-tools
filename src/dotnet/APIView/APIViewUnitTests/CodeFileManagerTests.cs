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
    [InlineData(new[] { "line1", "line2", "line3" }, false, "")] // No duplicates
    [InlineData(new[] { "line1", "line2", "line1", "line3" }, true, "line1")] // Has duplicates
    [InlineData(new[] { "line1", "line2", "line1", "line2", "line3" }, true, "line1, line2")] // Multiple duplicates, returns all
    [InlineData(new[] { "", "line1", "", "line2" }, false, "")] // Empty line IDs ignored
    [InlineData(new[] { "Line1", "line1", "LINE1" }, false, "")] // Case sensitive - no duplicates
    public void AreLineIdsDuplicate_VariousScenarios_ReturnsExpectedResult(string[] lineIds, bool expectedResult, string expectedDuplicateId)
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(lineIds);
        bool result = _codeFileManager.AreLineIdsDuplicate(codeFile, out string duplicateLineId);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedDuplicateId, duplicateLineId);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithNullLineIds_ReturnsFalse()
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(new[] { null, "line1", null, "line2" });
        bool result = _codeFileManager.AreLineIdsDuplicate(codeFile, out string duplicateLineId);
        Assert.False(result);
        Assert.Empty(duplicateLineId);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithMixedEmptyAndValidIds_IgnoresEmptyIds()
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(new[] { "", "line1", null, "line1", "" });
        bool result = _codeFileManager.AreLineIdsDuplicate(codeFile, out string duplicateLineId);
        Assert.True(result);
        Assert.Equal("line1", duplicateLineId);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithMultipleDuplicates_ReturnsAllDuplicateIds()
    {
        CodeFile codeFile = CreateCodeFileWithLineIds(["line1", "line2", "line3", "line1", "line2", "line4"]);
        bool result = _codeFileManager.AreLineIdsDuplicate(codeFile, out string duplicateLineId);
        Assert.True(result);
        Assert.Equal("line1, line2", duplicateLineId);
    }

    [Fact]
    public void AreLineIdsDuplicate_WithNoApiLines_ReturnsFalse()
    {
        CodeFile codeFile = new() { Language = "C#", Name = "Test", PackageName = "TestPackage" };
        bool result = _codeFileManager.AreLineIdsDuplicate(codeFile, out string duplicateLineId);
        Assert.False(result);
        Assert.Empty(duplicateLineId);
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
