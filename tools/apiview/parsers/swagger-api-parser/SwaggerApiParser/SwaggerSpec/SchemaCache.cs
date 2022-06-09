using System.Collections.Generic;
using System.Linq;

namespace SwaggerApiParser;

public class SchemaCache
{
    public Dictionary<string, BaseSchema> Cache;

    public SchemaCache()
    {
        this.Cache = new Dictionary<string, BaseSchema>();
    }

    public BaseSchema GetBaseSchemaFromRef(string Ref)
    {
        var key = Ref.Split("/").Last();
        this.Cache.TryGetValue(key, out var ret);
        return ret;
    }

    public BaseSchema GetResolvedSchema(BaseSchema root)
    {
        if (root == null)
        {
            return null;
        }

        root.properties ??= new Dictionary<string, BaseSchema>();
        root.allOfProperities ??= new Dictionary<string, BaseSchema>();

        if (root.IsRefObj())
        {
            var schema = this.GetBaseSchemaFromRef(root.Ref);
            var ret = this.GetResolvedSchema(schema);
            ret.originalRef = root.Ref;
            return ret;
        }

        if (root.allOf != null)
        {
            foreach (var allOfItem in root.allOf)
            {
                var resolvedChild = this.GetResolvedSchema(allOfItem);
                if (resolvedChild == null)
                {
                    continue;
                }

                // should be in allOf property
                foreach (var prop in resolvedChild.properties)
                {
                    root.allOfProperities[prop.Key] = prop.Value;
                }
            }
        }

        if (root.properties != null)
        {
            foreach (var rootProperty in root.properties)
            {
                root.properties[rootProperty.Key] = this.GetResolvedSchema(rootProperty.Value);
            }
        }

        return root;
    }
}
