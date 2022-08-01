﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace APIView.Model
{
    public class TreeNode<T> : IEnumerable<TreeNode<T>>
    {
        public T Data { get; set; }
        public TreeNode<T> Parent { get; set; }
        public ICollection<TreeNode<T>> Children { get; set; }

        public bool IsRoot { get { return Parent == null; } }

        public bool IsLeaf { get { return Children.Count == 0; } }

        public int Level 
        { 
            get 
            { 
                if (this.IsRoot) 
                    return 0;
                return Parent.Level + 1;
            }
        }

        public TreeNode(T codeLine)
        {
            this.Data = codeLine;
            this.Children = new LinkedList<TreeNode<T>>();
        }

        public TreeNode<T> AddChild(T child)
        {
            TreeNode<T> childNode = new TreeNode<T>(child);
            childNode.Parent = this;
            this.Children.Add(childNode);
            return childNode;
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
