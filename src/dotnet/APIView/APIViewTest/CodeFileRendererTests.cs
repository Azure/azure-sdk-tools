using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
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
            var codeLines = result.CodeLines;
            var sections = result.Sections;
            var sectionsAsList = sections[0].ToList();

            // Assert
            Assert.Equal(2, codeLines.Length);
            Assert.Equal(" KeywordLine_One:", codeLines[0].DisplayString);
            Assert.Equal(1, codeLines[0].LineNumber);
            Assert.Null(codeLines[0].SectionKey);
            Assert.Equal("HeadingLineOne", codeLines[1].DisplayString);
            Assert.Equal(2, codeLines[1].LineNumber);
            Assert.Equal(0, codeLines[1].SectionKey);


            Assert.Single(sections);
            Assert.Collection(sectionsAsList,
                item => {
                    Assert.Equal("HeadingLineOne", item.Data.DisplayString);
                    Assert.Equal(2, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLineOne:", item.Data.DisplayString);
                    Assert.Equal(3, item.Data.LineNumber);
                });
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
                new CodeFileToken("", CodeFileTokenKind.Newline),
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd)
            };
        
            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        
            // Act
            var result = codeFileRenderer.Render(codeFile);
            var codeLines = result.CodeLines;
            var sections = result.Sections;
            var sectionsAsList = sections[0].ToList();


            // Assert
            Assert.Equal(2, codeLines.Length);
            Assert.Equal(" KeywordLine_One:", codeLines[0].DisplayString);
            Assert.Equal(1, codeLines[0].LineNumber);
            Assert.Null(codeLines[0].SectionKey);
            Assert.Equal("HeadingLineOne", codeLines[1].DisplayString);
            Assert.Equal(2, codeLines[1].LineNumber);
            Assert.Equal(0, codeLines[1].SectionKey);

            Assert.Single(sections);
            Assert.Collection(sectionsAsList,
                item => {
                    Assert.Equal("HeadingLineOne", item.Data.DisplayString);
                    Assert.Equal(2, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLineOne: 3.0", item.Data.DisplayString);
                    Assert.Equal(3, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLineTwo: 4.0", item.Data.DisplayString);
                    Assert.Equal(4, item.Data.LineNumber);
                });
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
                new CodeFileToken(null, CodeFileTokenKind.FoldableSectionContentEnd)
            };
        
            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        
            // Act
            var result = codeFileRenderer.Render(codeFile);
            var codeLines = result.CodeLines;
            var sections = result.Sections;
            var sectionsAsList1 = sections[0].ToList();
            var sectionsAsList2 = sections[1].ToList();


            // Assert
            Assert.Equal(2, codeLines.Length);
            Assert.Equal("HeadingLine_1", codeLines[0].DisplayString);
            Assert.Equal(1, codeLines[0].LineNumber);
            Assert.Equal(0, codeLines[0].SectionKey);
            Assert.Equal("HeadingLine_2", codeLines[1].DisplayString);
            Assert.Equal(4, codeLines[1].LineNumber);
            Assert.Equal(1, codeLines[1].SectionKey);

            Assert.Equal(2, sections.Count);
            Assert.Collection(sectionsAsList1,
                item => {
                    Assert.Equal("HeadingLine_1", item.Data.DisplayString);
                    Assert.Equal(1, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLine_11: 3.0", item.Data.DisplayString);
                    Assert.Equal(2, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLine_12: 4.0", item.Data.DisplayString);
                    Assert.Equal(3, item.Data.LineNumber);
                });
            Assert.Collection(sectionsAsList2,
                item => {
                    Assert.Equal("HeadingLine_2", item.Data.DisplayString);
                    Assert.Equal(4, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLine_21: 3.0", item.Data.DisplayString);
                    Assert.Equal(5, item.Data.LineNumber);
                },
                item => {
                    Assert.Equal(" LiteralLine_22: 4.0", item.Data.DisplayString);
                    Assert.Equal(6, item.Data.LineNumber);
                });
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
            var codeLines = result.CodeLines;
            var sections = result.Sections;
            var sectionsAsList = sections[0].ToList();

            // Assert
            Assert.Equal(2, codeLines.Length);
            Assert.Equal(" KeywordLine_1:", codeLines[0].DisplayString);
            Assert.Equal(1, codeLines[0].LineNumber);
            Assert.Null(codeLines[0].SectionKey);
            Assert.Equal("HeadingLine_1", codeLines[1].DisplayString);
            Assert.Equal(2, codeLines[1].LineNumber);
            Assert.Equal(0, codeLines[1].SectionKey);

            Assert.Single(sections);

            Assert.Collection(sectionsAsList,
                item => {
                    Assert.Equal("HeadingLine_1", item.Data.DisplayString);
                    Assert.Equal(2, item.Data.LineNumber);
                    Assert.Equal(0, item.Level);
                },
                item => {
                    Assert.Equal(" LiteralLine_1:", item.Data.DisplayString);
                    Assert.Equal(3, item.Data.LineNumber);
                    Assert.Equal(1, item.Level);
                },
                item => {
                    Assert.Equal("HeadingLine_11", item.Data.DisplayString);
                    Assert.Equal(4, item.Data.LineNumber);
                    Assert.Equal(1, item.Level);
                },
                item => {
                    Assert.Equal(" LiteralLine_111:", item.Data.DisplayString);
                    Assert.Equal(5, item.Data.LineNumber);
                    Assert.Equal(2, item.Level);
                });
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
                new CodeFileToken("", CodeFileTokenKind.Newline)
            };
        
            codeFile.Tokens = token;
            CodeFileRenderer codeFileRenderer = new CodeFileRenderer();
        
            // Act
            var result = codeFileRenderer.Render(codeFile);
            var codeLines = result.CodeLines;
            var sections = result.Sections;
            var sectionsAsList = sections[0].ToList();

            // Assert
            Assert.Equal(2, codeLines.Length);
            Assert.Single(sections);
            Assert.Equal("HeadingLine_1", codeLines[0].DisplayString);
            Assert.Equal(1, codeLines[0].LineNumber);
            Assert.Equal(0, codeLines[0].SectionKey);

            Assert.Collection(sectionsAsList,
            item => {
                Assert.Equal("HeadingLine_1", item.Data.DisplayString);
                Assert.Equal(1, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_1:", item.Data.DisplayString);
                Assert.Equal(2, item.Data.LineNumber);
            },
            item => {
                Assert.Equal("HeadingLine_11", item.Data.DisplayString);
                Assert.Equal(3, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_111:", item.Data.DisplayString);
                Assert.Equal(4, item.Data.LineNumber);
            },
            item => {
                Assert.Equal("HeadingLine_111", item.Data.DisplayString);
                Assert.Equal(5, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_1111:", item.Data.DisplayString);
                Assert.Equal(6, item.Data.LineNumber);
            },
            item => {
                Assert.Equal("HeadingLine_112", item.Data.DisplayString);
                Assert.Equal(7, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_1121:", item.Data.DisplayString);
                Assert.Equal(8, item.Data.LineNumber);
            },
            item => {
                Assert.Equal("HeadingLine_1121", item.Data.DisplayString);
                Assert.Equal(9, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_11211:", item.Data.DisplayString);
                Assert.Equal(10, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_2:", item.Data.DisplayString);
                Assert.Equal(11, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_3:", item.Data.DisplayString);
                Assert.Equal(12, item.Data.LineNumber);
            },
            item => {
                Assert.Equal("HeadingLine_12", item.Data.DisplayString);
                Assert.Equal(13, item.Data.LineNumber);
            },
            item => {
                Assert.Equal(" LiteralLine_121:", item.Data.DisplayString);
                Assert.Equal(14, item.Data.LineNumber);
            });
        }
    }
}
