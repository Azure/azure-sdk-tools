using APIView.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        public const string DIFF_PROPERTY = "DiffKind";

        public static List<APITreeNode> ComputeAPIForestDiff(List<APITreeNode> activeAPIRevisionAPIForest, List<APITreeNode> diffAPIRevisionAPIForest)
        {
            List<APITreeNode> result = new List<APITreeNode>();
            List<Task> tasks = new List<Task>();
            ComputeAPITreeDiff(activeAPIRevisionAPIForest, diffAPIRevisionAPIForest, result, tasks);
            Task.WaitAll(tasks.ToArray());
            return result;
        }

        private static void ComputeAPITreeDiff(List<APITreeNode> activeAPIRevisionAPIForest, List<APITreeNode> diffAPIRevisionAPIForest, List<APITreeNode> result, List<Task> tasks)
        {           
            var activeAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNode>(activeAPIRevisionAPIForest);
            var diffAPIRevisionTreeNodesAtLevel = new HashSet<APITreeNode>(diffAPIRevisionAPIForest);

            var allNodesAtLevelSorted = activeAPIRevisionTreeNodesAtLevel.Union(diffAPIRevisionTreeNodesAtLevel).OrderBy(node => node.Id).ToList();
            var unChangedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Intersect(diffAPIRevisionTreeNodesAtLevel).ToList();
            var removedNodesAtLevel = activeAPIRevisionTreeNodesAtLevel.Except(diffAPIRevisionTreeNodesAtLevel).ToList();
            var addedNodesAtLevel = diffAPIRevisionTreeNodesAtLevel.Except(activeAPIRevisionTreeNodesAtLevel).ToList();

            foreach (var node in allNodesAtLevelSorted)
            {
                if (unChangedNodesAtLevel.Contains(node))
                {
                    var newEntry = CloneAPITreeNode(node);
                    newEntry.Properties[DIFF_PROPERTY] = "Unchanged";
                    result.Add(newEntry);
                }
                else if (removedNodesAtLevel.Contains(node))
                {
                   node.Properties[DIFF_PROPERTY] = "Removed";
                   result.Add(node);
                }
                else if (addedNodesAtLevel.Contains(node))
                {
                    node.Properties[DIFF_PROPERTY] = "Added";
                    result.Add(node);
                }
            }

            foreach (var node in unChangedNodesAtLevel)
            {
                var activeNode = activeAPIRevisionTreeNodesAtLevel.First(n => n.Equals(node));
                var diffNode = diffAPIRevisionTreeNodesAtLevel.First(n => n.Equals(node));
                var childrenResult = new List<APITreeNode>();
                ComputeAPITreeDiff(activeNode.Children, diffNode.Children, childrenResult, tasks);
                var resultNode = result.First(n => n.Equals(node));

                tasks.Add(Task.Run(() => ComputeTokenDiffForNode(activeNode, diffNode, resultNode)));
                resultNode.Children.AddRange(childrenResult); 
            }
        }

        public static void ComputeTokenDiffForNode(APITreeNode activeAPIRevisionTreeNode, APITreeNode diffAPIRevisionTreeNode, APITreeNode resultNode)
        {
            // To do Compute Token Diff

        }

        public static APITreeNode CloneAPITreeNode(APITreeNode node)
        {
            return new APITreeNode
            {
                Name = node.Name,
                Id = node.Id,
                Kind    = node.Kind,
                SubKind = node.SubKind,
            };
        }
    }
}
