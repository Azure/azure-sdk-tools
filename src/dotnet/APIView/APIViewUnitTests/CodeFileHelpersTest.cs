using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Assert.False(result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);

            //Expected top level nodes 16
            Assert.Equal(16, result.NodeMetaData["root"].ChildrenNodeIdsInOrder.Count);

            //Verify rendered result has no diff when comparing same API code files
            FileInfo fileInfoB = new FileInfo(testCodeFilePath);
            var codeFileB = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            codePanelRawData.diffRevisionCodeFile = codeFileB;
            result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.False(result.HasDiff);

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
            Assert.False(result.HasDiff);
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
            Assert.True(result.HasDiff);
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
            Assert.False(result.HasDiff);
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
            Assert.True(result.HasDiff);
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
            Assert.False(result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);
        }

        [Fact]
        public async void VerifySameApiwithOnlyDocChange()
        {
            var testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0.json");
            FileInfo fileInfo = new FileInfo(testCodeFilePath);
            var codeFile = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            CodePanelRawData codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = codeFile
            };

            //Verify rendered result has no diff when comparing different API code files
            testCodeFilePath = Path.Combine("SampleTestFiles", "azure.data.tables.12.9.0_doc_change.json");
            FileInfo fileInfoB = new FileInfo(testCodeFilePath);
            var codeFileB = await CodeFile.DeserializeAsync(fileInfoB.Open(FileMode.Open, FileAccess.Read, FileShare.Read));
            codePanelRawData.diffRevisionCodeFile = codeFileB;
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            //Doc only change should not flag a revision as different
            Assert.False(result.HasDiff);
            Assert.Equal(478, result.NodeMetaData.Count);
        }

        [Fact]
        public async void VerifyNavigationNodeCount()
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
            var navTreeNodeCount = result.NodeMetaData.Values.Count(n => n.NavigationTreeNode != null);
            Assert.Equal(42, navTreeNodeCount);
        }


        [Fact]
        public async void VerifyPackageReviewLineCount()
        {
            var testCodeFilePath = Path.Combine("SampleTestFiles", "azure-core-1.47.0-sources4.json");
            FileInfo fileInfo = new FileInfo(testCodeFilePath);
            var codeFile = await CodeFile.DeserializeAsync(fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read));

            CodePanelRawData codePanelRawData = new CodePanelRawData()
            {
                activeRevisionCodeFile = codeFile
            };
            var result = await CodeFileHelpers.GenerateCodePanelDataAsync(codePanelRawData);
            Assert.NotNull(result);
            Assert.False(result.HasDiff);
            Assert.Equal(2159, result.NodeMetaData.Count);
        }

    }
}
