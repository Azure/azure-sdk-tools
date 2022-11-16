using System.Collections;
using System.Collections.Generic;

namespace APIView.Model
{
    public class TreeNode<T> : IEnumerable<TreeNode<T>>
    {
        public T Data { get; set; }
        public TreeNode<T> Parent { get; set; }
        public ICollection<TreeNode<T>> Children { get; set; }

        public bool IsRoot { get { return Parent == null; } }

        public bool IsLeaf { get { return Children.Count == 0; } }

        public bool WasDetachedLeafParent { get; set; }

        public int Level 
        { 
            get 
            { 
                if (this.IsRoot) 
                    return 0;
                return Parent.Level + 1;
            }
        }

        public int PositionAmongSiblings { get; set; } = 1;

        public TreeNode(T codeLine)
        {
            this.Data = codeLine;
            this.Children = new LinkedList<TreeNode<T>>();
        }

        public TreeNode<T> AddChild(T child)
        {
            TreeNode<T> childNode = new TreeNode<T>(child);
            childNode.Parent = this;
            childNode.PositionAmongSiblings = this.Children.Count + 1;
            this.Children.Add(childNode);
            return childNode;
        }

        public bool IsParentOf(TreeNode<T> childNode)
        {
            return this.Children.Contains(childNode);
        }

        public override string ToString()
        {
            return Data != null ? Data.ToString() : "Null Data";
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public IEnumerator<TreeNode<T>> GetEnumerator()
        {
            yield return this;
            foreach (var directChild in this.Children)
            {
                foreach (var anyChild in directChild)
                    yield return anyChild;
            }
        }

    }
}
