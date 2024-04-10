using APIView.Model;
using APIViewWeb.LeanModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIViewWeb.Helpers
{
    public class CodeFileHelpers
    {
        public static async Task<List<APITreeNode>> ComputeAPITreeDiff(List<APITreeNode> activeAPITree, List<APITreeNode> diffAPITree)
        {
            await Task.CompletedTask;
            return new List<APITreeNode>();
        }
    }
}
