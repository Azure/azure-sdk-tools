using System;
using System.Collections.Generic;
using System.Linq;

namespace SwaggerApiParser.Specs
{
    public class SchemaCache
    {
        public Dictionary<string, Dictionary<string, Schema>> Cache;
        public Dictionary<string, Schema> ResolvedCache;
        public Dictionary<string, Dictionary<string, Parameter>> ParametersCache;

        public SchemaCache()
        {
            this.Cache = new Dictionary<string, Dictionary<string, Schema>>();
            this.ResolvedCache = new Dictionary<string, Schema>();
            this.ParametersCache = new Dictionary<string, Dictionary<string, Parameter>>();
        }

        public void AddSchema(string swaggerFilePath, string key, Schema value)
        {
            this.Cache.TryGetValue(swaggerFilePath, out var swaggerSchema);
            if (swaggerSchema == null)
            {
                swaggerSchema = new Dictionary<string, Schema>();
                this.Cache.TryAdd(swaggerFilePath, swaggerSchema);
            }

            swaggerSchema.TryAdd(key, value);
        }

        public void AddParameter(string swaggerFilePath, string key, Parameter parameter)
        {
            this.ParametersCache.TryGetValue(swaggerFilePath, out var parameterCache);
            if (parameterCache == null)
            {
                parameterCache = new Dictionary<string, Parameter>();
                this.ParametersCache.TryAdd(swaggerFilePath, parameterCache);
            }

            parameterCache.TryAdd(key, parameter);
        }

        public static string GetRefKey(string Ref)
        {
            var key = Ref.Split("/").Last();
            return key;
        }

        public static string RemoveCrossFileReferenceFromRef(string Ref)
        {
            var idx = Ref.IndexOf("#", StringComparison.Ordinal);
            var key = Ref[idx..];
            return key;
        }

        public static string GetResolvedCacheRefKey(string Ref, string currentSwaggerFilePath)
        {
            return RemoveCrossFileReferenceFromRef(Ref) + Utils.GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);
        }

        private Schema GetSchemaFromResolvedCache(string Ref, string currentSwaggerFilePath)
        {
            var resolvedKey = GetResolvedCacheRefKey(Ref, currentSwaggerFilePath);
            this.ResolvedCache.TryGetValue(resolvedKey, out var resolvedSchema);
            return resolvedSchema;
        }

        private Schema GetSchemaFromCache(string Ref, string currentSwaggerFilePath)
        {
            // try get from resolved cache.


            var referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);


            this.Cache.TryGetValue(referenceSwaggerFilePath, out var swaggerSchema);
            if (swaggerSchema == null)
            {
                return null;
            }

            var key = GetRefKey(Ref);
            swaggerSchema.TryGetValue(key, out var ret);

            if (ret == null)
            {
                throw new Exception($"Reference not found. $ref: {Ref}");
            }

            return ret;
        }

        public Parameter GetParameterFromCache(string Ref, string currentSwaggerFilePath)
        {
            // try get from resolved cache.


            var referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);


            this.ParametersCache.TryGetValue(referenceSwaggerFilePath, out var parameterCache);
            if (parameterCache == null)
            {
                return null;
            }

            var key = GetRefKey(Ref);
            parameterCache.TryGetValue(key, out var ret);

            if (ret == null)
            {
                throw new Exception($"Reference not found. $ref: {Ref}");
            }

            return ret;
        }

        public Parameter GetResolvedParameter(Parameter parameter, string currentSwaggerFilePath)
        {
            if (parameter.IsRefObject())
            {
                return this.GetParameterFromCache(parameter.@ref, currentSwaggerFilePath);
            }

            return parameter;
        }

        public Schema GetResolvedSchema(Schema root, string currentSwaggerFilePath, LinkedList<string> refChain = null)
        {
            refChain ??= new LinkedList<string>();
            if (root == null)
            {
                return null;
            }

            root.properties ??= new Dictionary<string, Schema>();
            root.allOfProperities ??= new Dictionary<string, Schema>();

            if (root.IsRefObject())
            {
                if (refChain.Contains(root.@ref))
                {
                    return root;
                }

                var resolvedSchema = this.GetSchemaFromResolvedCache(root.@ref, currentSwaggerFilePath);
                if (resolvedSchema != null)
                {
                    return resolvedSchema;
                }

                // If refChain already has resolve refKey. Circular reference. return root.
                if (refChain.Contains(GetResolvedCacheRefKey(root.@ref, currentSwaggerFilePath)))
                {
                    root.originalRef = root.@ref;
                    root.@ref = null;
                    return root;
                }

                // get from original schema cache.
                refChain.AddLast(GetResolvedCacheRefKey(root.@ref, currentSwaggerFilePath));
                var schema = this.GetSchemaFromCache(root.@ref, currentSwaggerFilePath);
                var ret = this.GetResolvedSchema(schema, Utils.GetReferencedSwaggerFile(root.@ref, currentSwaggerFilePath), refChain);
                // write back resolved cache
                this.ResolvedCache.TryAdd(GetResolvedCacheRefKey(root.@ref, currentSwaggerFilePath), schema);
                refChain.RemoveLast();

                if (ret == null)
                {
                    return null;
                }

                ret.originalRef = root.@ref;
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

            if (root.items != null && root.items.@ref != null && !refChain.Contains(root.items.@ref))
            {
                root.items = GetResolvedSchema(root.items, currentSwaggerFilePath, refChain);
            }

            if (root.properties != null)
            {
                foreach (var rootProperty in root.properties)
                {
                    if (rootProperty.Value == null)
                    {
                        continue;
                    }

                    if (!refChain.Contains(rootProperty.Value.@ref) && !refChain.Contains(rootProperty.Value.@ref) && !refChain.Contains(rootProperty.Value.items?.@ref))
                    {
                        root.properties[rootProperty.Key] = this.GetResolvedSchema(rootProperty.Value, currentSwaggerFilePath, refChain);
                    }
                }
            }

            return root;
        }
    }
}
