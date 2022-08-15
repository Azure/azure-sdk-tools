using System;
using System.Collections.Generic;
using APIView;

namespace SwaggerApiParser;

public class SwaggerApiViewDefinitions : Dictionary<String, Definition>, INavigable, ITokenSerializable
{
    public NavigationItem BuildNavigationItem(IteratorPath iteratorPath = null)
    {
        iteratorPath ??= new IteratorPath();
        NavigationItem ret = new NavigationItem() {Text = "Definitions"};
        iteratorPath.Add("Definitions");
        List<NavigationItem> children = new List<NavigationItem>();
        foreach (var kv in this)
        {
            children.Add(new NavigationItem() {Text = kv.Key, NavigationId = iteratorPath.CurrentNextPath(kv.Key)});
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
            ret.Add(new CodeFileToken(kv.Key, CodeFileTokenKind.Literal) {DefinitionId = context.IteratorPath.CurrentNextPath(kv.Key)});
            ret.Add(TokenSerializer.Colon());
            ret.Add(TokenSerializer.NewLine());
            ret.AddRange(TokenSerializer.KeyValueTokens("Description", kv.Value.description));
            
            List<SchemaTableItem> tableItems = null;
            string[] columns = new[] {"Field", "Type/Format", "Keywords", "Description"};
            kv.Value.TokenSerializePropertyIntoTableItems(context, ref tableItems, false);

            var tableRet = new List<CodeFileToken>();
            var tableRows = new List<CodeFileToken>();
            
            // remove first model level.
            tableItems.RemoveAt(0);
            foreach (var tableItem in tableItems)
            {
                string[] serializedFields = new[] {"Field", "TypeFormat", "Keywords", "Description"};
                tableRows.AddRange(tableItem.TokenSerializeWithOptions(serializedFields));
            }

            tableRet.AddRange(TokenSerializer.TokenSerializeAsTableFormat(tableItems.Count, columns.Length, columns, tableRows.ToArray()));
            tableRet.Add(TokenSerializer.NewLine());


            ret.AddRange(tableRet);
        }

        ret.Add(TokenSerializer.FoldableContentEnd());
        return ret.ToArray();
    }
}
