using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIView.TreeToken
{
    public class APITreeNode
    {
        /// <summary>
        /// Indicates a node is an assembly kind
        /// </summary>
        public static string ASSEMBLY = "Assembly";
        /// <summary>
        /// Indicates a node is a InternalsVisibleTo kind
        /// </summary>
        public static string INTERNALS_VISIBLE_TO = "InternalsVisibleTo";
        /// <summary>
        /// Indicates a node is a Dependencies kind
        /// </summary>
        public static string DEPENDENCIES = "Dependencies";
        /// <summary>
        /// Indicates a node is a Namespace kind
        /// </summary>
        public static string NAMESPACE = "Namespace";
        /// <summary>
        /// Indicates a node is a Type kind
        /// </summary>
        public static string TYPE = "Type";
        /// <summary>
        /// Indicates a node is a Member kind
        /// </summary>
        public static string MEMBER = "Member";
        /// <summary>
        /// Indicates a node is hidden
        /// </summary>
        public static string HIDDEN = "Hidden";
        /// <summary>
        /// Indicates that a node should be hidden from Navigation
        /// </summary>
        public static string HIDE_FROM_NAV = "HideFromNav";
        /// <summary>
        /// Property key to use to make a nodes kind more specific.
        /// </summary>
        public static string SUB_KIND = "SubKind";

        /// <summary>
        /// Id of the node, which should be unique at the node level. i.e. unique among its siblings.
        /// This was previously represented by the DefinitionId for the main Token of the node.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The type of node it is. Using any of the following `assembly`, `class`, `delegate`, `enum`, `interface`,
        /// `method` , `namespace`, `package`, `struct`, `type` will get you the corresponding default icons
        /// for the page navigation.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// The name of the tree node which will be used as label for the API Navigation.
        /// Generally use the name of the module (class, method).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Tokens which are rendered after all child nodes.
        /// Depending on the language this would include the closing curly brace and/or empty lines.
        /// You can simulate an empty line by adding an empty token (Content token with no value) and a LineBreak token.
        /// </summary>
        public List<StructuredToken> BottomTokens
        {
            get { return BottomTokensObj.Count > 0 ? BottomTokensObj : null; }
            set { BottomTokensObj = value ?? new List<StructuredToken>(); }
        }
        /// <summary>
        /// The nodes immediate children.
        /// For a namespace this would be classes, for a class this would be the class constructors and methods.
        /// Children are rendered after TopTokens but before BottomTokens, and are automatically indented.
        /// </summary>
        public List<APITreeNode> Children
        {
            get { return ChildrenObj.Count > 0 ? ChildrenObj : null; }
            set { ChildrenObj = value ?? new List<APITreeNode>(); }
        }
        /// <summary>
        /// Properties of a node.
        /// <list type="bullet">
        /// <item>
        /// <description>SubKind: Similar to kind, use this to make the node more specific.
        /// e.g. `Kind = 'Type'`, and `SubKind = 'class'`. We also use this to make the navigation icon it will override kind.</description>
        /// </item>
        /// <item>
        /// <description>IconName: Use this only if you are looking to add a custom icon different from language wide defaults. New additions will need to be supported APIView side.</description>
        /// </item>
        /// </list>
        /// </summary>
        public Dictionary<string, string> Properties
        {
            get { return PropertiesObj.Count > 0 ? PropertiesObj : null; }
            set { PropertiesObj = value ?? new Dictionary<string, string>(); }
        }

        /// <summary>
        /// Behavioral boolean properties.
        /// <list type="bullet">
        /// <item>
        /// <description>Deprecated: Mark a node as deprecated</description>
        /// </item>
        /// <item>
        /// <description>Hidden: Mark a node as Hidden</description>
        /// </item>
        /// <item>
        /// <description>HideFromNavigation: Indicate that a node should be hidden from the page navigation.</description>
        /// </item>
        /// <item>
        /// <description>SkipDiff: Indicate that a node should not be used in computation of diff.</description>
        /// </item>
        /// <item>
        /// <description>CrossLangDefId: The cross language definitionId for the node.</description>
        /// </item>
        /// </list>
        /// </summary>
        public HashSet<string> Tags
        {
            get { return TagsObj.Count > 0 ? TagsObj : null; }
            set { TagsObj = value ?? new HashSet<string>(); }
        }
        
        /// <summary>
        /// The main data of the node. This is all the tokens that actually define the node.
        /// When rendering, TopTokens are rendered first, followed by any Children, and then finally BottomTokens
        /// </summary>
        public List<StructuredToken> TopTokens
        {
            get { return TopTokensObj.Count > 0 ? TopTokensObj : null; }
            set { TopTokensObj = value ?? new List<StructuredToken>(); }
        }
        
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

        [JsonIgnore]
        public DiffKind DiffKind { get; set; } = DiffKind.NoneDiff;

        [JsonIgnore]
        public List<StructuredToken> TopDiffTokens { get; set; } = new List<StructuredToken>();

        [JsonIgnore]
        public List<StructuredToken> BottomDiffTokens { get; set; } = new List<StructuredToken>();

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
