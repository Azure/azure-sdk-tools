using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using A;
using ApiView;
using APIView.Model.V2;
using APIView.TreeToken;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using SharpCompress.Common;
using Xunit;
using Xunit.Abstractions;

namespace APIViewUnitTests
{
    public class CodeFileHelpersTests
    {
        private readonly ITestOutputHelper _output;

        public CodeFileHelpersTests(ITestOutputHelper output)
        {

            _output = output;
        }

        [Fact]
        public void ComputeAPITreeDiff_Generates_Accurate_TreeDiff()
        {
            /*var expectedResult =  new List<(string id, string diffKind)>
            { 
                ("1A", "Unchanged"),
                ("2A", "Unchanged"),
                ("2B", "Unchanged"),
                ("3A", "Removed"),
                ("3B", "Unchanged"),
                ("3C", "Unchanged"),
                ("2C", "Removed"),
                ("2D", "Unchanged"),
                ("3A", "Unchanged"),
                ("3B", "Unchanged"),
                ("3C", "Added"),
                ("2E", "Added"),
            };
            var diffForest = CodeFileHelpers.ComputeAPIForestDiff(apiForestA, apiForestB);
            var result = (TraverseForest(diffForest, false))[0];

            Assert.Equal(expectedResult.Count, result.Count);
            for (int i = 0; i < expectedResult.Count; i++)
            {
                Assert.Equal(expectedResult[i].id, result[i].id);
                Assert.Equal(expectedResult[i].diffKind, result[i].diffKind);
            }

            diffForest = CodeFileHelpers.ComputeAPIForestDiff(apiForestD, apiForestC);
            var result2 = TraverseForest(diffForest, false);

            expectedResult = new List<(string id, string diffKind)>
            {
                ("1A", "Unchanged"),
                ("2A", "Added"),
                ("2B", "Unchanged"),
                ("3A", "Added"),
                ("2C", "Unchanged")
            };

            Assert.Equal(expectedResult.Count, result2[0].Count);
            for (int i = 0; i < expectedResult.Count; i++)
            {
                Assert.Equal(expectedResult[i].id, result2[0][i].id);
                Assert.Equal(expectedResult[i].diffKind, result2[0][i].diffKind);
            }

            expectedResult = new List<(string id, string diffKind)>
            {
                ("1B", "Unchanged"),
                ("2A", "Added"),
                ("2B", "Unchanged"),
                ("3A", "Removed"),
                ("2C", "Removed")
            };

            Assert.Equal(expectedResult.Count, result2[1].Count);
            for (int i = 0; i < expectedResult.Count; i++)
            {
                Assert.Equal(expectedResult[i].id, result2[1][i].id);
                Assert.Equal(expectedResult[i].diffKind, result2[1][i].diffKind);
            }*/
        }

        [Fact]
        public void ComputeTokenDiff_Verify_API_only_Change_dummy_data()
        {
            var activeLines = new List<ReviewLine>();
            activeLines.Add(new ReviewLine()
            {
                LineId = "1A",
                Tokens = new List<ReviewToken>()
                {
                    new ReviewToken("namespace", TokenKind.Keyword),
                    new ReviewToken("test.core", TokenKind.Text),
                    new ReviewToken("{", TokenKind.Punctuation){HasSuffixSpace = false}
                },
                Children = new List<ReviewLine>()
                {
                    new ReviewLine()
                    {
                        LineId = "2A",
                        Tokens = new List<ReviewToken>()
                        {
                            new ReviewToken("public", TokenKind.Keyword),
                            new ReviewToken("class", TokenKind.Keyword),
                            new ReviewToken("TestClass", TokenKind.Text),
                            new ReviewToken("{", TokenKind.Punctuation){HasSuffixSpace = false}
                        },
                        Children = new List<ReviewLine>()
                        {
                            new ReviewLine()
                            {
                                LineId = "3A",
                                Tokens = new List<ReviewToken>()
                                {
                                    new ReviewToken("public", TokenKind.Keyword),
                                    new ReviewToken("void", TokenKind.Keyword),
                                    new ReviewToken("TestMethod", TokenKind.Text),
                                    new ReviewToken("()", TokenKind.Punctuation)
                                }
                            }
                        }
                    },
                    new ReviewLine()
                    {
                        LineId = "2BA",
                        Tokens = new List<ReviewToken>()
                        {
                            new ReviewToken("}", TokenKind.Punctuation){HasSuffixSpace = false}
                        },
                        IsContextEndLine = true
                    }
                }

            });
            activeLines.Add(new ReviewLine()
            {
                LineId = "1B",
                Tokens = new List<ReviewToken>()
                {
                    new ReviewToken("}", TokenKind.Punctuation){HasSuffixSpace = false}
                },
                IsContextEndLine = true
            });

            var diffLines = new List<ReviewLine>();
            diffLines.Add(new ReviewLine()
            {
                LineId = "1A",
                Tokens = new List<ReviewToken>()
                {
                    new ReviewToken("namespace", TokenKind.Keyword),
                    new ReviewToken("test.core", TokenKind.Text),
                    new ReviewToken("{", TokenKind.Punctuation){HasSuffixSpace = false}
                },
                Children = new List<ReviewLine>()
                {
                    new ReviewLine()
                    {
                        LineId = "2A",
                        Tokens = new List<ReviewToken>()
                        {
                            new ReviewToken("public", TokenKind.Keyword),
                            new ReviewToken("class", TokenKind.Keyword),
                            new ReviewToken("TestClass1", TokenKind.Text),
                            new ReviewToken("{", TokenKind.Punctuation){HasSuffixSpace = false}
                        },
                        Children = new List<ReviewLine>()
                        {
                            new ReviewLine()
                            {
                                LineId = "3A",
                                Tokens = new List<ReviewToken>()
                                {
                                    new ReviewToken("public", TokenKind.Keyword),
                                    new ReviewToken("void", TokenKind.Keyword),
                                    new ReviewToken("TestMethod", TokenKind.Text),
                                    new ReviewToken("()", TokenKind.Punctuation)
                                }
                            }
                        }
                    },
                    new ReviewLine()
                    {
                        LineId = "2BA",
                        Tokens = new List<ReviewToken>()
                        {
                            new ReviewToken("}", TokenKind.Punctuation){HasSuffixSpace = false}
                        },
                        IsContextEndLine = true
                    }
                }

            });
            diffLines.Add(new ReviewLine()
            {
                LineId = "1B",
                Tokens = new List<ReviewToken>()
                {
                    new ReviewToken("}", TokenKind.Punctuation){HasSuffixSpace = false}
                },
                IsContextEndLine = true
            });
            var resultLines = CodeFileHelpers.FindDiff(activeLines, diffLines);
            int modifiedCount = 0;
            foreach (var l in resultLines)
            {
                modifiedCount += ModifiedLineCount(l);
            }
            Assert.Equal(4, modifiedCount);
        }

        private int ModifiedLineCount(ReviewLine line)
        {
            int count = 0;
            if (line.DiffKind == DiffKind.Added || line.DiffKind == DiffKind.Removed)
            {
                count++;
            }
            foreach (var child in line.Children)
            {
                count += ModifiedLineCount(child);
            }
            return count;
        }

        [Fact]
        public async void VerifyRenderedCodeFile()
        {
            var testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0.json");
            FileInfo fileInfo = new FileInfo(testCodeFilePath);
            var codeFile = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            CodePanelRawData codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = codeFile
            };
            //Verify total lines in rendered output
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.Equal(false, result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);

            //Expected top level nodes 16
            Assert.Equal(16, result.NodeMetaData["root"].ChildrenNodeIdsInOrder.Count);

            //Verify rendered result has no diff when comparing same API code files
            FileInfo fileInfoB = new FileInfo(testCodeFilePath);
            var codeFileB = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            codePanelRawData.diffRevisionCodeFile = codeFileB;
            result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.Equal(false, result.HasDiff);

            //Verify system generated comments
            Assert.Equal(15, result.NodeMetaData.Values.Select(v => v.DiagnosticsObj.Count).Sum());
        }

        [Fact]
        public async void VerifyCompareDiffApiSurface()
        {
            var testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0.json");
            FileInfo fileInfo = new FileInfo(testCodeFilePath);
            var codeFile = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            CodePanelRawData codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = codeFile
            };
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.Equal(false, result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);

            //Expected top level nodes 16
            Assert.Equal(16, result.NodeMetaData["root"].ChildrenNodeIdsInOrder.Count);

            //Verify rendered result has no diff when comparing different API code files
            testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.1.json");
            FileInfo fileInfoB = new FileInfo(testCodeFilePath);
            var codeFileB = await CodeFile.DeserializeAsync(fileInfoB.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            codePanelRawData.diffRevisionCodeFile = codeFileB;
            result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.Equal(true, result.HasDiff);
            // Thre is only one API line change difference between 12.9.1 and 12.9.0
            Assert.Equal(1, result.NodeMetaData.Values.Count(m => m.IsNodeWithDiff));
        }

        [Fact]
        public async void VerifyAttributeLineChangeOnly()
        {
            var testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0.json");
            FileInfo fileInfo = new FileInfo(testCodeFilePath);
            var codeFile = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            CodePanelRawData codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = codeFile
            };
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.Equal(false, result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);

            //Expected top level nodes 16
            Assert.Equal(16, result.NodeMetaData["root"].ChildrenNodeIdsInOrder.Count);

            //Verify rendered result has no diff when comparing different API code files
            testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0_altered.json");
            FileInfo fileInfoB = new FileInfo(testCodeFilePath);
            var codeFileB = await CodeFile.DeserializeAsync(fileInfoB.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            codePanelRawData.diffRevisionCodeFile = codeFileB;
            result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.Equal(true, result.HasDiff);
            // Thre is only one API line change difference between 12.9.1 and 12.9.0
            Assert.Equal(2, result.NodeMetaData.Values.Count(m => m.IsNodeWithDiff));
            var modifiedLines = result.NodeMetaData.Values.Where(m => m.IsNodeWithDiff);
            var changedTexts = new HashSet<string> (modifiedLines.Select(l => l.CodeLines.FirstOrDefault().ToString().Trim()));
            Assert.Contains("[Flags]", changedTexts);
            Assert.Contains("[FlagsModified]", changedTexts);
        }

        [Fact]
        public async void VerifySameApiwithDependencyVersionChange()
        {
            var testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0.json");
            FileInfo fileInfo = new FileInfo(testCodeFilePath);
            var codeFile = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            CodePanelRawData codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = codeFile
            };

            //Verify rendered result has no diff when comparing different API code files
            testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0_dep_diff.json");
            FileInfo fileInfoB = new FileInfo(testCodeFilePath);
            var codeFileB = await CodeFile.DeserializeAsync(fileInfoB.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            codePanelRawData.diffRevisionCodeFile = codeFileB;
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            //Dependency version change(marked as skip from diff )should not be considered as diff
            Assert.Equal(false, result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);
        }
    }
}
