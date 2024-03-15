using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIView.TreeToken
{ 
    public class APITreeNode
    {
        public HashSet<string> Tags
        {
            get { return TagsObj.Count > 0 ? TagsObj : null; }
            set { TagsObj = value ?? new HashSet<string>(); }
        }
        public Dictionary<string, string> Properties
        {
            get { return PropertiesObj.Count > 0 ? PropertiesObj : null; }
            set { PropertiesObj = value ?? new Dictionary<string, string>(); }
        }
        public List<StructuredToken> TopTokens
        {
            get { return TopTokensObj.Count > 0 ? TopTokensObj : null; }
            set { TopTokensObj = value ?? new List<StructuredToken>(); }
        }
        public List<StructuredToken> BottomTokens
        {
            get { return BottomTokensObj.Count > 0 ? BottomTokensObj : null; }
            set { BottomTokensObj = value ?? new List<StructuredToken>(); }
        }
        public List<APITreeNode> Children
        {
            get { return ChildrenObj.Count > 0 ? ChildrenObj : null; }
            set { ChildrenObj = value ?? new List<APITreeNode>(); }
        }

        public string Name { get; set; }
        public string Id { get; set; }
        public string Kind { get; set; }
        [JsonIgnore]
        public HashSet<string> TagsObj { get; set; } = new HashSet<string>();
        [JsonIgnore]
        public Dictionary<string, string> PropertiesObj { get; set; } = new Dictionary<string, string>();
        [JsonIgnore]
        public List<StructuredToken> TopTokensObj { get; set; } = new List<StructuredToken>();
        [JsonIgnore]
        public List<StructuredToken> BottomTokensObj { get; set; } = new List<StructuredToken>();
        [JsonIgnore]
        public List<APITreeNode> ChildrenObj { get; set; } = new List<APITreeNode>();
         
        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + (Name != null ? Name.GetHashCode() : 0);
            hash = hash * 23 + (Id != null ? Id.GetHashCode() : 0);
            hash = hash * 23 + (Kind != null ? Kind.GetHashCode() : 0);
            return hash;
        }

        public override bool Equals(object obj)
        {
            var other = obj as APITreeNode;
            if (other == null)
            {
                return false;
            }
            return Name == other.Name && Id == other.Id && Kind == other.Kind;
        }

        public void SortChildren()
        {
            if (ChildrenObj != null)
            {
                if (Kind.Equals("Namespace") || Kind.Equals("Type") || Kind.Equals("Member"))
                {
                    ChildrenObj.Sort((x, y) => x.Name.CompareTo(y.Name));
                }
                foreach (var child in ChildrenObj)
                {
                    child.SortChildren();
                }
            }
        }
    }
}
