using System;
using System.Collections.Generic;
using SwaggerApiParser.Specs;

namespace SwaggerApiParser.SwaggerApiView
{
    public class SwaggerApiViewDefinitions : SortedDictionary<String, Definition>, INavigable, ITokenSerializable
    {
        public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
        {
            iteratorPath ??= new IteratorPath();
            iteratorPath.Add("Definitions");
            NavigationItem ret = new NavigationItem() { Text = "Definitions", NavigationId = iteratorPath.CurrentPath() };
            List<NavigationItem> children = new List<NavigationItem>();
            foreach (var kv in this)
            {
                children.Add(new NavigationItem() { Text = kv.Key, NavigationId = iteratorPath.CurrentNextPath(kv.Key) });
            }

            ret.ChildItems = children.ToArray();
            iteratorPath.Pop();
            return ret;
        }

        public CodeFileToken[] TokenSerialize(SerializeContext context)
        {
            List<CodeFileToken> ret = new List<CodeFileToken>();
            ret.Add(TokenSerializer.FoldableContentStart());
            foreach (var kv in this)
            {
                context.IteratorPath.Add(kv.Key);

                ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.Literal) { DefinitionId = context.IteratorPath.CurrentPath() });
                ret.Add(TokenSerializer.Colon());
                ret.Add(TokenSerializer.NewLine());
                ret.AddRange(TokenSerializer.KeyValueTokens("Description", kv.Value.description));

                List<SchemaTableItem> tableItems = null;
                string[] columns = new[] { "Field", "Type/Format", "Keywords", "Description" };
                tableItems = kv.Value.TokenSerializePropertyIntoTableItems(context, tableItems, false);

                var tableRet = new List<CodeFileToken>();
                var tableRows = new List<CodeFileToken>();

                foreach (var tableItem in tableItems)
                {
                    string[] serializedFields = new[] { "Field", "TypeFormat", "Keywords", "Description" };
                    tableRows.AddRange(tableItem.TokenSerializeWithOptions(serializedFields, context));
                }

                if (tableRows.Count > 0)
                {
                    tableRet.AddRange(TokenSerializer.TokenSerializeAsTableFormat(tableItems.Count, columns.Length, columns, tableRows.ToArray(), context.IteratorPath.CurrentNextPath("table")));
                }
                tableRet.Add(TokenSerializer.NewLine());

                ret.AddRange(tableRet);
                context.IteratorPath.Pop();
            }

            ret.Add(TokenSerializer.FoldableContentEnd());
            return ret.ToArray();
        }
    }
}
