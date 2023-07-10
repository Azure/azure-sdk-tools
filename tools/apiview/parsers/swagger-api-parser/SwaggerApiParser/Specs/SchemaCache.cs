using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SwaggerApiParser.Specs
{
    public class SchemaCache
    {
        public Dictionary<string, Dictionary<string, Schema>> Cache;
        public Dictionary<string, Schema> ResolvedCache;
        public Dictionary<string, Dictionary<string, Parameter>> ParametersCache;
        public Dictionary<string, Dictionary<string, Response>> ResponsesCache;

        public SchemaCache()
        {
            this.Cache = new Dictionary<string, Dictionary<string, Schema>>();
            this.ResolvedCache = new Dictionary<string, Schema>();
            this.ParametersCache = new Dictionary<string, Dictionary<string, Parameter>>();
            this.ResponsesCache = new Dictionary<string, Dictionary<string, Response>>();
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

        public void AddResponse(string swaggerFilePath, string key, Response response)
        {
            this.ResponsesCache.TryGetValue(swaggerFilePath, out var responseCache);
            if (responseCache == null)
            {
                responseCache = new Dictionary<string, Response>();
                this.ResponsesCache.TryAdd(swaggerFilePath, responseCache);
            }

            responseCache.TryAdd(key, response);
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

        public Schema GetSchemaFromCache(string Ref, string currentSwaggerFilePath, bool resolveSwaggerPath = true)
        {
            var referenceSwaggerFilePath = currentSwaggerFilePath;
            if (resolveSwaggerPath)
            {
                referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);
            }

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

        public Parameter GetParameterFromCache(string Ref, string currentSwaggerFilePath, bool resolveSwaggerPath = true)
        {
            var referenceSwaggerFilePath = currentSwaggerFilePath;
            if (resolveSwaggerPath)
            {
                referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);
            }
            
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

        public Response GetResponseFromCache(string Ref, string currentSwaggerFilePath, bool resolveSwaggerPath = true)
        {
            var referenceSwaggerFilePath = currentSwaggerFilePath;
            if (resolveSwaggerPath)
            {
                referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(Ref, currentSwaggerFilePath);
            }

            this.ResponsesCache.TryGetValue(referenceSwaggerFilePath, out var responseCache);
            if (responseCache == null)
            {
                return null;
            }

            var key = GetRefKey(Ref);
            responseCache.TryGetValue(key, out var ret);

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

        public Schema GetResolvedSchema(Schema root, string currentSwaggerFilePath, LinkedList<string> refChain = null, Dictionary<string, Definition> definitions = null)
        {
            string schemaKey = string.Empty;
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
                    Utils.AddSchemaToRootDefinition(resolvedSchema, definitions);
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
                if (root.@ref != null)
                {
                    this.ResolvedCache.TryAdd(GetResolvedCacheRefKey(root.@ref, currentSwaggerFilePath), schema);
                }
                
                refChain.RemoveLast();

                if (ret == null)
                {
                    return null;
                }

                ret.originalRef = root.@ref;
                Utils.AddSchemaToRootDefinition(ret, definitions);
                return ret;
            }

            if (root.allOf != null)
            {
                foreach (var allOfItem in root.allOf)
                {
                    var @ref = root.@ref ?? root.originalRef;
                    var resolvedChild = this.GetResolvedSchema(allOfItem, Utils.GetReferencedSwaggerFile(@ref, currentSwaggerFilePath), refChain);
                    if (resolvedChild == null)
                    {
                        continue;
                    }

                    Utils.AddSchemaToRootDefinition(resolvedChild, definitions);

                    // should be in allOf property
                    foreach (var prop in resolvedChild.properties)
                    {
                        root.allOfProperities[prop.Key] = prop.Value;
                    }
                }
            }

            if (root.items != null && root.items.@ref != null && !refChain.Contains(root.items.@ref))
            {
                var items = GetResolvedSchema(root.items, currentSwaggerFilePath, refChain);
                root.items = items;
                Utils.AddSchemaToRootDefinition(items, definitions);
            }

            if (root.additionalProperties.ValueKind == JsonValueKind.Object) 
            {
                var additionalProperties = JsonSerializer.Deserialize<Dictionary<string, object>>(root.additionalProperties);
                if (additionalProperties.ContainsKey("$ref"))
                {
                    var schema = new Schema();
                    schema.@ref = additionalProperties["$ref"].ToString();
                    var refKey = GetRefKey(schema.@ref);
                    root.properties[refKey] = schema;
                }
            }

            if (root.properties != null)
            {
                foreach (var rootProperty in root.properties)
                {
                    if (rootProperty.Value == null)
                    {
                        continue;
                    }

                    if (!refChain.Contains(rootProperty.Value.@ref) && !refChain.Contains(rootProperty.Value.items?.@ref))
                    {
                        var @ref = root.@ref ?? root.originalRef;
                        var property = this.GetResolvedSchema(rootProperty.Value, Utils.GetReferencedSwaggerFile(@ref, currentSwaggerFilePath), refChain);
                        root.properties[rootProperty.Key] = property;
                        Utils.AddSchemaToRootDefinition(property, definitions);
                    }
                }
            }
            Utils.AddSchemaToRootDefinition(root, definitions);
            return root;
        }
    }
}
