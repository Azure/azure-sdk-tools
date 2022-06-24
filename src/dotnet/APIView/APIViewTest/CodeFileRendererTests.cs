using System;
using System.Collections.Generic;
using System.Text;
using ApiView;
using APIView;
using Xunit;

namespace APIViewUnitTest
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
                new CodeFileToken("", CodeFileTokenKind.Newline)
            };

            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
            
            // Act
            var result = codeFileRenderer.Render(codeFile);

            // Assert
            Assert.Equal(3, result.Length);
            Assert.Equal(" KeywordLine_One:", result[0].DisplayString);
            Assert.Equal("HeadingLineOne", result[1].DisplayString);
            Assert.Equal("HeadingLineOne-heading", result[1].LineClass);
            Assert.Equal(" LiteralLineOne:", result[2].DisplayString);
            Assert.Equal("HeadingLineOne-content", result[2].LineClass);
        }

        [Fact]
        public void Render_SingleLevelTokens_ReturnsSingleLevelTree_2()
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
                new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
                new CodeFileToken("3.0", CodeFileTokenKind.Literal),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLineTwo", CodeFileTokenKind.Literal),
                new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
                new CodeFileToken("4.0", CodeFileTokenKind.Literal),
                new CodeFileToken("", CodeFileTokenKind.Newline)
            };

            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();

            // Act
            var result = codeFileRenderer.Render(codeFile);


            // Assert
            Assert.Equal(4, result.Length);
            Assert.Equal(" KeywordLine_One:", result[0].DisplayString);
            Assert.Equal("HeadingLineOne", result[1].DisplayString);
            Assert.Equal("HeadingLineOne-heading", result[1].LineClass);
            Assert.Equal(" LiteralLineOne: 3.0", result[2].DisplayString);
            Assert.Equal("HeadingLineOne-content", result[2].LineClass);
            Assert.Equal(" LiteralLineTwo: 4.0", result[3].DisplayString);
            Assert.Equal("HeadingLineOne-content", result[3].LineClass);
        }

        [Fact]
        public void Render_SingleLevelTokens_ReturnsSingleLevelTree_3()
        {
            // Arrange
            CodeFile codeFile = new CodeFile();
            CodeFileToken[] token = new CodeFileToken[] {
                new CodeFileToken("HeadingLine_1", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_11", CodeFileTokenKind.Literal),
                new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
                new CodeFileToken("3.0", CodeFileTokenKind.Literal),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_12", CodeFileTokenKind.Literal),
                new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
                new CodeFileToken("4.0", CodeFileTokenKind.Literal),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken("HeadingLine_2", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_21", CodeFileTokenKind.Literal),
                new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
                new CodeFileToken("3.0", CodeFileTokenKind.Literal),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_22", CodeFileTokenKind.Literal),
                new CodeFileToken(": ", CodeFileTokenKind.Punctuation),
                new CodeFileToken("4.0", CodeFileTokenKind.Literal),
                new CodeFileToken("", CodeFileTokenKind.Newline),
            };

            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();

            // Act
            var result = codeFileRenderer.Render(codeFile);


            // Assert
            Assert.Equal(6, result.Length);
            Assert.Equal("HeadingLine_1", result[0].DisplayString);
            Assert.Equal("HeadingLine_1-heading", result[0].LineClass);
            Assert.Equal(" LiteralLine_11: 3.0", result[1].DisplayString);
            Assert.Equal("HeadingLine_1-content", result[1].LineClass);
            Assert.Equal(" LiteralLine_12: 4.0", result[2].DisplayString);
            Assert.Equal("HeadingLine_1-content", result[2].LineClass);
            Assert.Equal("HeadingLine_2", result[3].DisplayString);
            Assert.Equal("HeadingLine_2-heading", result[3].LineClass);
            Assert.Equal(" LiteralLine_21: 3.0", result[4].DisplayString);
            Assert.Equal("HeadingLine_2-content", result[4].LineClass);
            Assert.Equal(" LiteralLine_22: 4.0", result[5].DisplayString);
            Assert.Equal("HeadingLine_2-content", result[5].LineClass);
        }

        [Fact]
        public void Render_TwoLevelTokens_ReturnsTwoLevelTree_1()
        {
            // Arrange
            CodeFile codeFile = new CodeFile();
            CodeFileToken[] token = new CodeFileToken[] {
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("KeywordLine_1", CodeFileTokenKind.Keyword),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLine_1", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_1", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLine_11", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_111", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd)
            };

            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();

            // Act
            var result = codeFileRenderer.Render(codeFile);

            // Assert
            Assert.Equal(5, result.Length);
            Assert.Equal(" KeywordLine_1:", result[0].DisplayString);
            Assert.Equal("HeadingLine_1", result[1].DisplayString);
            Assert.Equal("HeadingLine_1-heading", result[1].LineClass);
            Assert.Equal(" LiteralLine_1:", result[2].DisplayString);
            Assert.Equal("HeadingLine_1-content", result[2].LineClass);
            Assert.Equal("HeadingLine_11", result[3].DisplayString);
            Assert.Equal("HeadingLine_11-heading HeadingLine_1-content", result[3].LineClass);
            Assert.Equal(" LiteralLine_111:", result[4].DisplayString);
            Assert.Equal("HeadingLine_11-content", result[4].LineClass);
        }

        [Fact]
        public void Render_MixedLevelToken_ReturnsMixedLevelTree()
        {
            // Arrange
            CodeFile codeFile = new CodeFile();
            CodeFileToken[] token = new CodeFileToken[] {
                new CodeFileToken("HeadingLine_1", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_1", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLine_11", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_111", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLine_111", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_1111", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken("HeadingLine_112", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_1121", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLine_1121", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_11211", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_2", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_3", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken("HeadingLine_12", CodeFileTokenKind.FoldableSectionHeading),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentStart),
                new CodeFileToken(" ", CodeFileTokenKind.Whitespace),
                new CodeFileToken("LiteralLine_121", CodeFileTokenKind.Literal),
                new CodeFileToken(":", CodeFileTokenKind.Punctuation),
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd),
            };

            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();

            // Act
            var result = codeFileRenderer.Render(codeFile);

            // Assert
            Assert.Equal(14, result.Length);
            Assert.Equal("HeadingLine_1", result[0].DisplayString);
            Assert.Equal("HeadingLine_1-heading", result[0].LineClass);
            Assert.Equal(" LiteralLine_1:", result[1].DisplayString);
            Assert.Equal("HeadingLine_1-content", result[1].LineClass);
            Assert.Equal("HeadingLine_11", result[2].DisplayString);
            Assert.Equal("HeadingLine_11-heading HeadingLine_1-content", result[2].LineClass);
            Assert.Equal(" LiteralLine_111:", result[3].DisplayString);
            Assert.Equal("HeadingLine_11-content", result[3].LineClass);
            Assert.Equal("HeadingLine_111", result[4].DisplayString);
            Assert.Equal("HeadingLine_111-heading HeadingLine_11-content", result[4].LineClass);
            Assert.Equal(" LiteralLine_1111:", result[5].DisplayString);
            Assert.Equal("HeadingLine_111-content", result[5].LineClass);
            Assert.Equal("HeadingLine_112", result[6].DisplayString);
            Assert.Equal("HeadingLine_112-heading HeadingLine_11-content", result[6].LineClass);
            Assert.Equal(" LiteralLine_1121:", result[7].DisplayString);
            Assert.Equal("HeadingLine_112-content", result[7].LineClass);
            Assert.Equal("HeadingLine_1121", result[8].DisplayString);
            Assert.Equal("HeadingLine_1121-heading HeadingLine_112-content", result[8].LineClass);
            Assert.Equal(" LiteralLine_11211:", result[9].DisplayString);
            Assert.Equal("HeadingLine_1121-content", result[9].LineClass);
            Assert.Equal(" LiteralLine_2:", result[10].DisplayString);
            Assert.Equal("HeadingLine_1-content", result[10].LineClass);
            Assert.Equal(" LiteralLine_3:", result[11].DisplayString);
            Assert.Equal("HeadingLine_1-content", result[11].LineClass);
            Assert.Equal("HeadingLine_12", result[12].DisplayString);
            Assert.Equal("HeadingLine_12-heading HeadingLine_1-content", result[12].LineClass);
            Assert.Equal(" LiteralLine_121:", result[13].DisplayString);
            Assert.Equal("HeadingLine_12-content", result[13].LineClass);
        }

    }
}
