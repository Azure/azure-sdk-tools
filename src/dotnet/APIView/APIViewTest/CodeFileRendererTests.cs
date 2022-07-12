using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ApiView;
using APIView;
using APIView.Model;
using Xunit;

namespace APIViewUnitTests
{
    public class CodeFileRendererTests
    {
        [Fact]
        public void Render_SingleLevelTokens_ReturnsSingleLevelTree_1()
        {
            // Arrange
            CodeFile codeFile = new CodeFile();
            CodeFileToken[] token = new CodeFileToken[] {
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("KeywordLine_One", CodeFileTokenKind.Keyword),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLineOne", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLineOne", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd)
            };

            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();

            // Act
            var result = codeFileRenderer.Render(codeFile);

            // Assert
            Assert.Equal(2, result.CodeLines.Length);
            Assert.Equal(" KeywordLine_One:", result.CodeLines[0].DisplayString);
            Assert.Equal(1, result.CodeLines[0].LineNumber);
            Assert.Equal("HeadingLineOne", result.CodeLines[1].DisplayString);
            Assert.Equal(2, result.CodeLines[1].LineNumber);
            Assert.Equal(0, result.CodeLines[1].IndentSize);

            Assert.Single(result.Sections);
            Assert.Equal("HeadingLineOne", result.Sections[0].Data.DisplayString);
            Assert.Equal(2, result.Sections[0].Data.LineNumber);
            Assert.Equal(0, result.Sections[0].Data.IndentSize);

            Assert.Equal(" LiteralLineOne:", result.Sections[0].Children.ToList()[0].Data.DisplayString);
            Assert.Equal(3, result.Sections[0].Children.ToList()[0].Data.LineNumber);
            Assert.Equal(1, result.Sections[0].Children.ToList()[0].Data.IndentSize);
        }

        //[Fact]
        //public void Render_SingleLevelTokens_ReturnsSingleLevelTree_2()
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("KeywordLine_One", CodeFileTokenKind.Keyword),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLineOne", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLineOne", CodeFileTokenKind.Literal),
        //        new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("3.0", CodeFileTokenKind.Literal),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLineTwo", CodeFileTokenKind.Literal),
        //        new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("4.0", CodeFileTokenKind.Literal),
        //        new CodeFileToken("", CodeFileTokenKind.Newline)
        //    };
        //
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //
        //    // Assert
        //    Assert.Equal(4, result.Length);
        //    Assert.Equal(" KeywordLine_One:", result[0].DisplayString);
        //    Assert.Equal(0, result[0].IndentSize);
        //    Assert.Equal("HeadingLineOne", result[1].DisplayString);
        //    Assert.Equal("headinglineone-heading", result[1].LineClass);
        //    Assert.Equal(0, result[1].IndentSize);
        //    Assert.Equal(" LiteralLineOne: 3.0", result[2].DisplayString);
        //    Assert.Equal("headinglineone-content", result[2].LineClass);
        //    Assert.Equal(1, result[2].IndentSize);
        //    Assert.Equal(" LiteralLineTwo: 4.0", result[3].DisplayString);
        //    Assert.Equal("headinglineone-content", result[3].LineClass);
        //    Assert.Equal(1, result[3].IndentSize);
        //}
        //
        //[Fact]
        //public void Render_SingleLevelTokens_ReturnsSingleLevelTree_3()
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken("HeadingLine_1", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_11", CodeFileTokenKind.Literal),
        //        new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("3.0", CodeFileTokenKind.Literal),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_12", CodeFileTokenKind.Literal),
        //        new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("4.0", CodeFileTokenKind.Literal),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken("HeadingLine_2", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_21", CodeFileTokenKind.Literal),
        //        new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("3.0", CodeFileTokenKind.Literal),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_22", CodeFileTokenKind.Literal),
        //        new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("4.0", CodeFileTokenKind.Literal),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //    };
        //
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //
        //    // Assert
        //    Assert.Equal(6, result.Length);
        //    Assert.Equal("HeadingLine_1", result[0].DisplayString);
        //    Assert.Equal("headingline_1-heading", result[0].LineClass);
        //    Assert.Equal(0, result[0].IndentSize);
        //    Assert.Equal(" LiteralLine_11: 3.0", result[1].DisplayString);
        //    Assert.Equal("headingline_1-content", result[1].LineClass);
        //    Assert.Equal(1, result[1].IndentSize);
        //    Assert.Equal(" LiteralLine_12: 4.0", result[2].DisplayString);
        //    Assert.Equal("headingline_1-content", result[2].LineClass);
        //    Assert.Equal(1, result[2].IndentSize);
        //    Assert.Equal("HeadingLine_2", result[3].DisplayString);
        //    Assert.Equal("headingline_2-heading", result[3].LineClass);
        //    Assert.Equal(0, result[3].IndentSize);
        //    Assert.Equal(" LiteralLine_21: 3.0", result[4].DisplayString);
        //    Assert.Equal("headingline_2-content", result[4].LineClass);
        //    Assert.Equal(1, result[4].IndentSize);
        //    Assert.Equal(" LiteralLine_22: 4.0", result[5].DisplayString);
        //    Assert.Equal("headingline_2-content", result[5].LineClass);
        //    Assert.Equal(1, result[5].IndentSize);
        //}
        //
        //[Fact]
        //public void Render_TwoLevelTokens_ReturnsTwoLevelTree_1()
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("KeywordLine_1", CodeFileTokenKind.Keyword),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLine_1", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_1", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLine_11", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_111", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd)
        //    };
        //
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //    // Assert
        //    Assert.Equal(5, result.Length);
        //    Assert.Equal(" KeywordLine_1:", result[0].DisplayString);
        //    Assert.Equal(0, result[0].IndentSize);
        //    Assert.Equal("HeadingLine_1", result[1].DisplayString);
        //    Assert.Equal("headingline_1-heading", result[1].LineClass);
        //    Assert.Equal(0, result[1].IndentSize);
        //    Assert.Equal(" LiteralLine_1:", result[2].DisplayString);
        //    Assert.Equal("headingline_1-content", result[2].LineClass);
        //    Assert.Equal(1, result[2].IndentSize);
        //    Assert.Equal("HeadingLine_11", result[3].DisplayString);
        //    Assert.Equal("headingline_11-heading headingline_1-content", result[3].LineClass);
        //    Assert.Equal(1, result[3].IndentSize);
        //    Assert.Equal(" LiteralLine_111:", result[4].DisplayString);
        //    Assert.Equal("headingline_11-content", result[4].LineClass);
        //    Assert.Equal(2, result[4].IndentSize);
        //}
        //
        //[Fact]
        //public void Render_MixedLevelToken_ReturnsMixedLevelTree()
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken("HeadingLine_1", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_1", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLine_11", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_111", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLine_111", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_1111", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken("HeadingLine_112", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_1121", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLine_1121", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_11211", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_2", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_3", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLine_12", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLine_121", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
        //    };
        //
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //    // Assert
        //    Assert.Equal(14, result.Length);
        //    Assert.Equal("HeadingLine_1", result[0].DisplayString);
        //    Assert.Equal("headingline_1-heading", result[0].LineClass);
        //    Assert.Equal(0, result[0].IndentSize);
        //    Assert.Equal(" LiteralLine_1:", result[1].DisplayString);
        //    Assert.Equal("headingline_1-content", result[1].LineClass);
        //    Assert.Equal(1, result[1].IndentSize);
        //    Assert.Equal("HeadingLine_11", result[2].DisplayString);
        //    Assert.Equal("headingline_11-heading headingline_1-content", result[2].LineClass);
        //    Assert.Equal(1, result[2].IndentSize);
        //    Assert.Equal(" LiteralLine_111:", result[3].DisplayString);
        //    Assert.Equal("headingline_11-content", result[3].LineClass);
        //    Assert.Equal(2, result[3].IndentSize);
        //    Assert.Equal("HeadingLine_111", result[4].DisplayString);
        //    Assert.Equal("headingline_111-heading headingline_11-content", result[4].LineClass);
        //    Assert.Equal(2, result[4].IndentSize);
        //    Assert.Equal(" LiteralLine_1111:", result[5].DisplayString);
        //    Assert.Equal("headingline_111-content", result[5].LineClass);
        //    Assert.Equal(3, result[5].IndentSize);
        //    Assert.Equal("HeadingLine_112", result[6].DisplayString);
        //    Assert.Equal("headingline_112-heading headingline_11-content", result[6].LineClass);
        //    Assert.Equal(2, result[6].IndentSize);
        //    Assert.Equal(" LiteralLine_1121:", result[7].DisplayString);
        //    Assert.Equal("headingline_112-content", result[7].LineClass);
        //    Assert.Equal(3, result[7].IndentSize);
        //    Assert.Equal("HeadingLine_1121", result[8].DisplayString);
        //    Assert.Equal("headingline_1121-heading headingline_112-content", result[8].LineClass);
        //    Assert.Equal(3, result[8].IndentSize);
        //    Assert.Equal(" LiteralLine_11211:", result[9].DisplayString);
        //    Assert.Equal("headingline_1121-content", result[9].LineClass);
        //    Assert.Equal(4, result[9].IndentSize);
        //    Assert.Equal(" LiteralLine_2:", result[10].DisplayString);
        //    Assert.Equal("headingline_1-content", result[10].LineClass);
        //    Assert.Equal(1, result[10].IndentSize);
        //    Assert.Equal(" LiteralLine_3:", result[11].DisplayString);
        //    Assert.Equal("headingline_1-content", result[11].LineClass);
        //    Assert.Equal(1, result[11].IndentSize);
        //    Assert.Equal("HeadingLine_12", result[12].DisplayString);
        //    Assert.Equal("headingline_12-heading headingline_1-content", result[12].LineClass);
        //    Assert.Equal(1, result[12].IndentSize);
        //    Assert.Equal(" LiteralLine_121:", result[13].DisplayString);
        //    Assert.Equal("headingline_12-content", result[13].LineClass);
        //    Assert.Equal(2, result[13].IndentSize);
        //}
        //
        //[Theory]
        //[InlineData("9yherjuA-85hfh_:/utut{tut}.it", "yherjua-85hfh_ututtutit")]
        //[InlineData("75867-AzMM*jdf  &jfr%joru--_@#AH DY85jfy", "-azmmjdfjfrjoru--_ahdy85jfy")]
        //public void Render_WithInvalidClass_ReturnsValidClassNames(string invalidClassPrefix, string validClassPrefix)
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken(invalidClassPrefix, CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLineOne", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline)
        //    };
        //
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //    // Assert
        //    Assert.Equal(2, result.Length);
        //    Assert.Equal(invalidClassPrefix, result[0].DisplayString);
        //    Assert.Equal($"{validClassPrefix}-heading", result[0].LineClass);
        //    Assert.Equal(" LiteralLineOne:", result[1].DisplayString);
        //    Assert.Equal($"{validClassPrefix}-content", result[1].LineClass);
        //}
        //
        //[Theory]
        //[InlineData("974Sary-uyre:%4*()-yrhw&7856!hfyr@", "sary-uyre:4-yrhw7856hfyr")]
        //public void Render_WithInvalidId_ReturnsValidIdNames(string invalidId, string validId)
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("KeywordLine_One", CodeFileTokenKind.Keyword),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline)
        //    };
        //    token[2].DefinitionId = invalidId;
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //    // Assert
        //    Assert.Single(result);
        //    Assert.Equal(" KeywordLine_One:", result[0].DisplayString);
        //    Assert.Equal(validId, result[0].ElementId);
        //}
        //
        //
        //[Theory]
        //[InlineData("dupicate-id-test")]
        //public void Render_WithDuplicateIds_ReturnsUniqueIdNames(string duplicateId)
        //{
        //    // Arrange
        //    CodeFile codeFile = new CodeFile();
        //    CodeFileToken[] token = new CodeFileToken[] {
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("KeywordLine_One", CodeFileTokenKind.Keyword),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken("HeadingLineOne", CodeFileTokenKind.FoldableSectionHeading),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLineOne", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline),
        //        new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
        //        new CodeFileToken("LiteralLineTwo", CodeFileTokenKind.Literal),
        //        new CodeFileToken(":", CodeFileTokenKind.Punctuation),
        //        new CodeFileToken("", CodeFileTokenKind.Newline)
        //    };
        //    token[2].DefinitionId = duplicateId;
        //    token[8].DefinitionId = duplicateId;
        //    token[12].DefinitionId = duplicateId;
        //    codeFile.Tokens = token;
        //    CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        //
        //    // Act
        //    var result = codeFileRenderer.Render(codeFile);
        //
        //    // Assert
        //    Assert.Equal(4, result.Length);
        //    Assert.Equal(" KeywordLine_One:", result[0].DisplayString);
        //    Assert.Equal(duplicateId, result[0].ElementId);
        //    Assert.Equal("HeadingLineOne", result[1].DisplayString);
        //    Assert.Equal("headinglineone-heading", result[1].LineClass);
        //    Assert.Equal(" LiteralLineOne:", result[2].DisplayString);
        //    Assert.Equal("headinglineone-content", result[2].LineClass);
        //    Assert.Equal($"{duplicateId}_1", result[2].ElementId);
        //    Assert.Equal(" LiteralLineTwo:", result[3].DisplayString);
        //    Assert.Equal("headinglineone-content", result[3].LineClass);
        //    Assert.Equal($"{duplicateId}_2", result[3].ElementId);
        //}

    }
}
