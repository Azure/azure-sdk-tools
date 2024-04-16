using System;
using System.Collections.Generic;
using System.Linq;
using APIView.Model;
using APIViewWeb.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace APIViewUnitTests
{
    public class CodeFileHelpersTests
    {
        private readonly ITestOutputHelper _output;
        List<APITreeNode> apiForestA = new List<APITreeNode>();
        List<APITreeNode> apiForestB = new List<APITreeNode>();
        List<APITreeNode> apiForestC = new List<APITreeNode>();
        List<APITreeNode> apiForestD = new List<APITreeNode>();

        List<StructuredToken> beforeTokens = new List<StructuredToken>();
        List<StructuredToken> afterTokens = new List<StructuredToken>();
        List<StructuredToken> diffTokens = new List<StructuredToken>();

        public CodeFileHelpersTests(ITestOutputHelper output) 
        {
            // Numbers indicate tree node level, letters indicate tree node position among siblings
            // First part is node name, second part is parent node name e.g `2A,1A` means node `2A` is child of node `1A`
            // Start with level 1,

            List<string> dataListA = new List<string> { "1A", "2A,1A", "2B,1A", "2C,1A", "2D,1A","3A,2B", "3B,2B", "3C,2B", "3A,2D", "3B,2D" };
            List<string> dataListB = new List<string> { "1A", "2A,1A", "2B,1A", "2D,1A", "2E,1A", "3B,2B", "3C,2B", "3A,2D", "3B,2D", "3C,2D" };
            List<string> dataListC = new List<string> { "1A", "2A,1A", "2B,1A", "2C,1A", "3A,2B" };
            List<string> dataListD = new List<string> { "1B", "2A,1B", "2B,1B" };
            List<string> dataListE = new List<string> { "1A", "2B,1A", "2C,1A" };
            List<string> dataListF = new List<string> { "1B", "2B,1B", "2C,1B", "3A,2B" };

            apiForestA.AddRange(this.BuildTestTree(dataListA));
            apiForestB.AddRange(this.BuildTestTree(dataListB));
            apiForestC.AddRange(this.BuildTestTree(dataListC));
            apiForestC.AddRange(this.BuildTestTree(dataListD));
            apiForestD.AddRange(this.BuildTestTree(dataListE));
            apiForestD.AddRange(this.BuildTestTree(dataListF));

            this.BuildTestTokenList();

            _output = output;
        }

        [Fact]
        public void ComputeAPITreeDiff_Generates_Accurate_TreeDiff()
        {
            var expectedResult =  new List<(string id, string diffKind)>
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
            }
        }

        [Fact]
        public void ComputeTokenDiff_Generates_Accurate_Result()
        {
            var result = CodeFileHelpers.ComputeTokenDiff(beforeTokens, afterTokens);
            Assert.Equal(diffTokens.Count, result.Count);

            for (int i = 0; i < diffTokens.Count; i++)
            {
                Assert.Equal(diffTokens[i].Value, result[i].Value);
                Assert.Equal(diffTokens[i].Id, result[i].Id);

                if (diffTokens[i].Properties.ContainsKey(CodeFileHelpers.DIFF_PROPERTY))
                {
                    Assert.Equal(diffTokens[i].Properties[CodeFileHelpers.DIFF_PROPERTY], result[i].Properties[CodeFileHelpers.DIFF_PROPERTY]);
                }
            }
        }
        private List<APITreeNode> BuildTestTree(List<string> data, string parentId = null)
        {
            List<APITreeNode> forest = new List<APITreeNode>();

            foreach (var item in data)
            {
                var parts = item.Split(',');

                if ((parts.Length == 1 && parentId == null) || (parts.Length > 1 && parts[1] == parentId))
                {
                    APITreeNode node = new APITreeNode { Id = parts[0] };
                    node.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, "NoneDiff");
                    node.Children.AddRange(BuildTestTree(data, node.Id));
                    forest.Add(node);
                }
            }
            return forest;
        }
        

        private void BuildTestTokenList()
        {
            List<StructuredToken> before = new List<StructuredToken>();
            List<StructuredToken> after = new List<StructuredToken>();
            List<StructuredToken> diff = new List<StructuredToken>();

            var valueBefore = "public";
            var valueAfter = valueBefore;
            var tokenBefore = new StructuredToken(valueBefore);
            var tokenAfter = new StructuredToken(valueAfter);
            var tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.UNCHANGED);
            before.Add(tokenBefore);
            before.Add(StructuredToken.CreateSpaceToken());
            after.Add(tokenAfter);
            after.Add(StructuredToken.CreateSpaceToken());
            diff.Add(tokenDiff);
            diff.Add(StructuredToken.CreateSpaceToken());

            valueBefore = "EnvironmentCredential";
            valueAfter = valueBefore;
            tokenBefore = new StructuredToken(valueBefore);
            tokenAfter = new StructuredToken(valueAfter);
            tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.UNCHANGED);
            before.Add(tokenBefore);
            after.Add(tokenAfter);
            diff.Add(tokenDiff);

            valueBefore = "(";
            valueAfter = valueBefore;
            tokenBefore = new StructuredToken(valueBefore);
            tokenAfter = new StructuredToken(valueAfter);
            tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.UNCHANGED);
            before.Add(tokenBefore);
            after.Add(tokenAfter);
            diff.Add(tokenDiff);

            valueBefore = "EnvironmentCredentialOptions";
            valueAfter = "TokenCredentialOptions";
            tokenBefore = new StructuredToken(valueBefore);
            tokenBefore.Id = "Azure.Identity.EnvironmentCredentialOptions";
            tokenAfter = new StructuredToken(valueAfter);
            tokenAfter.Id = "Azure.Identity.TokenCredentialOptions";
            tokenDiff = new StructuredToken(valueBefore);
            tokenDiff.Id = "Azure.Identity.EnvironmentCredentialOptions";
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.REMOVED);
            diff.Add(tokenDiff);
            tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Id = "Azure.Identity.TokenCredentialOptions";
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.ADDED);
            diff.Add(tokenDiff);
            diff.Add(StructuredToken.CreateSpaceToken());
            before.Add(tokenBefore);
            before.Add(StructuredToken.CreateSpaceToken());
            after.Add(tokenAfter);
            after.Add(StructuredToken.CreateSpaceToken());

            valueBefore = "options";
            valueAfter = valueBefore;
            tokenBefore = new StructuredToken(valueBefore);
            tokenAfter = new StructuredToken(valueAfter);
            tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.UNCHANGED);
            before.Add(tokenBefore);
            after.Add(tokenAfter);
            diff.Add(tokenDiff);

            valueBefore = ")";
            valueAfter = valueBefore;
            tokenBefore = new StructuredToken(valueBefore);
            tokenAfter = new StructuredToken(valueAfter);
            tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.UNCHANGED);
            before.Add(tokenBefore);
            after.Add(tokenAfter);
            diff.Add(tokenDiff);

            valueBefore = ";";
            valueAfter = valueBefore;
            tokenBefore = new StructuredToken(valueBefore);
            tokenAfter = new StructuredToken(valueAfter);
            tokenDiff = new StructuredToken(valueAfter);
            tokenDiff.Properties.Add(CodeFileHelpers.DIFF_PROPERTY, CodeFileHelpers.UNCHANGED);
            before.Add(tokenBefore);
            after.Add(tokenAfter);
            diff.Add(tokenDiff);

            this.beforeTokens = before;
            this.afterTokens = after;
            this.diffTokens = diff;
        }
        private List<List<(string id, string diffKind)>> TraverseForest(List<APITreeNode> forest, bool print = false)
        {
            var result = new List<List<(string id, string diffKind)>>();
            foreach (var tree in forest)
            {
                var treeNodeResult = new List<(string id, string diffKind)>();
                TraverseTree(tree, treeNodeResult, print);
                result.Add(treeNodeResult);
            }
            return result;
        }

        private void TraverseTree(APITreeNode node, List<(string id, string diffKind)> result, bool print = false, int level = 0)
        {
            if (print)
            { 
                var output = String.Empty;
                if (level > 1)
                {
                    var offset = level - 1;
                    var offsetIndicator = Enumerable.Repeat("    ", offset).ToList();
                    output = string.Join("", offsetIndicator);
                }
                var levelIndicator = Enumerable.Repeat("----", level).ToList();
                output += string.Join("", levelIndicator) + node.Id;

                _output.WriteLine(output);
            }
            result.Add((node.Id, node.Properties["DiffKind"]));
            foreach (var child in node.Children)
            {
                TraverseTree(child, result, print, level + 1);
            }
        }
    }
}
