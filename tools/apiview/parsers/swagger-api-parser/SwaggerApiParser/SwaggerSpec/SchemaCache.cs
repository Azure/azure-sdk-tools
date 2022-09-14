using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SwaggerApiParser;

public class SchemaCache
{
    public Dictionary<string, Dictionary<string, BaseSchema>> Cache;
    public Dictionary<string, BaseSchema> ResolvedCache;

    public SchemaCache()
    {
        this.Cache = new Dictionary<string, Dictionary<string, BaseSchema>>();
        this.ResolvedCache = new Dictionary<string, BaseSchema>();
    }

    public void AddSchema(string swaggerFilePath, string key, BaseSchema value)
    {
        this.Cache.TryGetValue(swaggerFilePath, out var swaggerSchema);
        if (swaggerSchema == null)
        {
            swaggerSchema = new Dictionary<string, BaseSchema>();
            this.Cache.TryAdd(swaggerFilePath, swaggerSchema);
        }

        swaggerSchema.TryAdd(key, value);
    }

    private static string GetReferencedSwaggerFile(string Ref, string currentSwaggerFilePath)
    {
        if (string.IsNullOrEmpty(Ref))
        {
            return currentSwaggerFilePath;
        }

        var idx = Ref.IndexOf("#", StringComparison.Ordinal);
        var relativePath = Ref[..idx];
        if (relativePath == "")
        {
            relativePath = ".";
        }
        else
        {
            currentSwaggerFilePath = Path.GetDirectoryName(currentSwaggerFilePath);
        }

        var referenceSwaggerFilePath = Path.GetFullPath(relativePath, currentSwaggerFilePath!);
        return referenceSwaggerFilePath;
    }

    private static string GetRefKey(string Ref)
    {
        var key = Ref.Split("/").Last();
        return key;
    }

    private BaseSchema GetSchemaFromResolvedCache(string Ref, string currentSwaggerFilePath)
    {
        var resolvedKey = Ref + currentSwaggerFilePath;
        this.ResolvedCache.TryGetValue(resolvedKey, out var resolvedSchema);
        return resolvedSchema;
    }

    private BaseSchema GetSchemaFromCache(string Ref, string currentSwaggerFilePath)
    {
        // try get from resolved cache.


        var referenceSwaggerFilePath = GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);


        this.Cache.TryGetValue(referenceSwaggerFilePath, out var swaggerSchema);
        if (swaggerSchema == null)
        {
            return null;
            throw new Exception($"Swagger schema not found. swagger file path: {currentSwaggerFilePath}");
        }

        var key = GetRefKey(Ref);
        swaggerSchema.TryGetValue(key, out var ret);

        if (ret == null)
        {
            throw new Exception($"Reference not found. $ref: {Ref}");
        }

        return ret;
    }

    public BaseSchema GetResolvedSchema(BaseSchema root, string currentSwaggerFilePath, LinkedList<string> refChain = null)
    {
        refChain ??= new LinkedList<string>();
        if (root == null)
        {
            return null;
        }

        root.properties ??= new Dictionary<string, BaseSchema>();
        root.allOfProperities ??= new Dictionary<string, BaseSchema>();

        if (root.IsRefObj())
        {
            if (refChain.Contains(root.Ref))
            {
                return root;
            }

            var resolvedSchema = this.GetSchemaFromResolvedCache(root.Ref, currentSwaggerFilePath);
            if (resolvedSchema != null)
            {
                return resolvedSchema;
            }
            
            // get from original schema cache.
            refChain.AddLast(root.Ref);
            var schema = this.GetSchemaFromCache(root.Ref, currentSwaggerFilePath);
            var ret = this.GetResolvedSchema(schema, GetReferencedSwaggerFile(root.Ref, currentSwaggerFilePath), refChain);
            // write back resolved cache
            this.ResolvedCache.TryAdd(root.Ref + currentSwaggerFilePath, schema);
            refChain.RemoveLast();

            if (ret == null)
            {
                return null;
            }
            ret.originalRef = root.Ref;
            return ret;
        }

        if (root.allOf != null)
        {
            foreach (var allOfItem in root.allOf)
            {
                var resolvedChild = this.GetResolvedSchema(allOfItem, currentSwaggerFilePath, refChain);
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

        if (root.items != null && root.items.Ref != null && !refChain.Contains(root.items.Ref))
        {
            root.items = GetResolvedSchema(root.items, currentSwaggerFilePath, refChain);
        }

        if (root.properties != null)
        {
            foreach (var rootProperty in root.properties)
            {
                if (!refChain.Contains(rootProperty.Value.Ref) && !refChain.Contains(rootProperty.Value.Ref) && !refChain.Contains(rootProperty.Value.items?.Ref))
                {
                    root.properties[rootProperty.Key] = this.GetResolvedSchema(rootProperty.Value, currentSwaggerFilePath, refChain);
                }
            }
        }

        return root;
    }
}
