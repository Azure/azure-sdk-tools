using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SwaggerApiParser.Specs;
using SwaggerApiParser.SwaggerApiView;

namespace SwaggerApiParser
{
    public class SwaggerApiViewGenerator
    {
        public static async Task<SwaggerApiViewSpec> GenerateSwaggerApiView(Swagger swaggerSpec, string swaggerFilePath, SchemaCache schemaCache, string packageName = "", string swaggerLink = "")
        {
            SwaggerApiViewSpec ret = new SwaggerApiViewSpec
            {
                SwaggerApiViewGeneral =
                {   
                    swaggerLink = swaggerLink,
                    swagger = swaggerSpec.swagger,
                    info = swaggerSpec.info,
                    host = swaggerSpec.host,
                    basePath = swaggerSpec.basePath,
                    schemes = swaggerSpec.schemes,
                    consumes = swaggerSpec.consumes,
                    produces = swaggerSpec.produces,
                    securityDefinitions = swaggerSpec.securityDefinitions,
                    security = swaggerSpec.security,
                    tags = swaggerSpec.tags,
                    externalDocs = swaggerSpec.externalDocs,
                    patternedObjects = swaggerSpec.patternedObjects,
                    schemaCache = schemaCache,
                    swaggerFilePath = swaggerFilePath
                },
                fileName = Path.GetFileName(swaggerFilePath),
                packageName = packageName,
                APIVersion = swaggerSpec.info.version
            };

            AddDefinitionsToCache(swaggerSpec, swaggerFilePath, schemaCache);
            //ret.SwaggerApiViewGeneral.xMsParameterizedHost?.ResolveParameters(schemaCache, swaggerFilePath);

            // If swagger doesn't have any path, it's common definition swagger. 
            if (swaggerSpec.paths.Count == 0)
            {
                return null;
            }

            foreach (var (currentPath, apiPath) in swaggerSpec.paths)
            {
                if (apiPath == null)
                {
                    continue;
                }

                foreach (var (method, operation) in apiPath.operations)
                {
                    SwaggerApiViewOperation op = new SwaggerApiViewOperation
                    {                    
                        tags = operation.tags,
                        summary = operation.summary,
                        description = operation.description,
                        operationId = operation.operationId,
                        consumes = operation.consumes,
                        produces = operation.produces,
                        PathParameters = new SwaggerApiViewOperationParameters("PathParameters"),
                        QueryParameters = new SwaggerApiViewOperationParameters("QueryParameters"),
                        BodyParameters = new SwaggerApiViewOperationParameters("BodyParameters"),
                        HeaderParameters = new SwaggerApiViewOperationParameters("HeaderParameters"),
                        Responses = new List<SwaggerApiViewResponse>(),
                        schemes = operation.schemes,
                        deprecated = operation.deprecated,
                        security = operation.security,
                        patternedObjects = operation.patternedObjects,

                        operationIdPrefix = Utils.GetOperationIdPrefix(operation.operationId),
                        operationIdAction = Utils.GetOperationIdAction(operation.operationId),                        
                        method = method,
                        path = currentPath,
                        operation = operation
                    };

                    if (operation.parameters != null)
                    {
                        foreach (var parameter in operation.parameters)
                        {
                            var param = parameter;
                            var referenceSwaggerFilePath = swaggerFilePath;

                            // Resolve Parameter from multilevel reference 
                            if (parameter.IsRefObject())
                            {
                                param = (Parameter)swaggerSpec.ResolveRefObj(parameter.@ref);
                                if (param == null)
                                {
                                    param = (Parameter)schemaCache.GetParameterFromCache(parameter.@ref, swaggerFilePath);
                                }

                                if (param == null)
                                {
                                    var referencePath = parameter.@ref;
                                    do
                                    {
                                        if (!Path.IsPathFullyQualified(referencePath))
                                        {
                                            referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(referencePath, referenceSwaggerFilePath);
                                            var referenceSwaggerSpec = await SwaggerDeserializer.Deserialize(referenceSwaggerFilePath);
                                            AddDefinitionsToCache(referenceSwaggerSpec, referenceSwaggerFilePath, schemaCache);
                                            param = schemaCache.GetParameterFromCache(referencePath, referenceSwaggerFilePath, false);
                                        }
                                        else
                                        {
                                            var referenceSwaggerSpec = await SwaggerDeserializer.Deserialize(referencePath);
                                            AddDefinitionsToCache(referenceSwaggerSpec, referencePath, schemaCache);
                                            param = schemaCache.GetParameterFromCache(referencePath, referencePath, false);
                                        }

                                        if (param != null && param.IsRefObject())
                                            referencePath = param.@ref;
                                    }
                                    while (param != null && param.IsRefObject());
                                }

                                referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(parameter.@ref, referenceSwaggerFilePath);
                            }

                            var swaggerApiViewOperationParameter = new SwaggerApiViewParameter
                            {
                                name = param.name,
                                @in = param.@in,
                                description = param.description,
                                required = param.required,
                                schema = schemaCache.GetResolvedSchema(param.schema, referenceSwaggerFilePath, null, swaggerSpec.definitions),
                                type = param.type,
                                format = param.format,
                                items = param.items,
                                collectionFormat = param.collectionFormat,
                                @default = param.@default,
                                maximum = param.maximum,
                                exclusiveMaximum = param.exclusiveMaximum,
                                minimum = param.minimum,
                                exclusiveMinimum = param.exclusiveMinimum,
                                maxLength = param.maxLength,
                                minLength = param.minLength,
                                pattern = param.pattern,
                                maxItems = param.maxItems,
                                minItems = param.minItems,
                                uniqueItems = param.uniqueItems,
                                multipleOf = param.multipleOf,
                                patternedObjects = param.patternedObjects,
                                @ref = param.@ref,
                            };

                            switch (param.@in)
                            {
                                case "path":
                                    op.PathParameters.Add(swaggerApiViewOperationParameter);
                                    break;
                                case "query":
                                    op.QueryParameters.Add(swaggerApiViewOperationParameter);
                                    break;
                                case "body":
                                    op.BodyParameters.Add(swaggerApiViewOperationParameter);
                                    break;
                                case "header":
                                    op.HeaderParameters.Add(swaggerApiViewOperationParameter);
                                    break;
                            }
                        }
                    }


                    foreach (var (statusCode, response) in operation.responses)
                    {
                        var resp = response;
                        var referenceSwaggerFilePath = swaggerFilePath;
                        string referencePath = "";

                        if (response.IsRefObject())
                        {
                            resp = (Response)swaggerSpec.ResolveRefObj(response.@ref);
                            if (resp == null)
                            {
                                resp = (Response)schemaCache.GetResponseFromCache(response.@ref, swaggerFilePath);
                                // Update reference path if referenced object is in another swagger file
                                if (response.IsRefObject() && !response.@ref.StartsWith("#"))
                                    referencePath = response.@ref;
                            }

                            if (resp == null)
                            {
                                referencePath = response.@ref;
                                do
                                {
                                    if (!Path.IsPathFullyQualified(referencePath))
                                    {
                                        referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(referencePath, swaggerFilePath);
                                        var referenceSwaggerSpec = await SwaggerDeserializer.Deserialize(referenceSwaggerFilePath);
                                        AddDefinitionsToCache(referenceSwaggerSpec, referenceSwaggerFilePath, schemaCache);
                                        resp = schemaCache.GetResponseFromCache(referencePath, referenceSwaggerFilePath, false);
                                    }
                                    else
                                    {
                                        var referenceSwaggerSpec = await SwaggerDeserializer.Deserialize(referencePath);
                                        AddDefinitionsToCache(referenceSwaggerSpec, referencePath, schemaCache);
                                        resp = schemaCache.GetResponseFromCache(referencePath, referencePath, false);
                                    }

                                    if (resp != null && resp.IsRefObject())
                                        referencePath = resp.@ref;
                                }
                                while (resp != null && resp.IsRefObject());
                            }
                        }

                        var schema = resp.schema;

                        //Resolve ref obj for response schema.

                        if (schema != null)
                        {
                            if (schema.IsRefObject())
                            {
                                // Update swagger file path to correct file if schema reference is local but parent itself is in another swagger file
                                if (schema.@ref.StartsWith("#") && !string.IsNullOrEmpty(referencePath))
                                {
                                    referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(referencePath, referenceSwaggerFilePath);
                                }
                                
                                referencePath = schema.@ref;
                                do
                                {
                                    referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(referencePath, referenceSwaggerFilePath);
                                    var referenceSwaggerSpec = await SwaggerDeserializer.Deserialize(referenceSwaggerFilePath);
                                    AddDefinitionsToCache(referenceSwaggerSpec, referenceSwaggerFilePath, schemaCache);
                                    schema = schemaCache.GetSchemaFromCache(referencePath, referenceSwaggerFilePath, false);
                                    if (schema.originalRef == null)
                                    {
                                        schema.originalRef = referencePath;
                                    }

                                    if (schema != null && schema.IsRefObject())
                                        referencePath = schema.@ref;
                                }
                                while (schema != null && schema.IsRefObject());
                            }

                            LinkedList<string> refChain = new LinkedList<string>();
                            // The initial refChain is the root level schema.
                            // There are some scenarios that the property of the root level schema is a ref to the root level itself (circular reference).
                            // Like "errorDetail" schema in common types.
                            schema = schemaCache.GetResolvedSchema(schema, referenceSwaggerFilePath, refChain, swaggerSpec.definitions);
                        }

                        var headers = resp.headers ?? new Dictionary<string, Header>();

                        op.Responses.Add(new SwaggerApiViewResponse() { description = response.description, statusCode = statusCode, schema = schema, headers = headers });
                    }

                    ret.Paths.AddSwaggerApiViewOperation(op);
                }
            }

            if (swaggerSpec.parameters != null)
            {
                foreach (var (key, value) in swaggerSpec.parameters)
                {
                    var param = value;
                    var swaggerApiViewParameter = new SwaggerApiViewParameter
                    {
                        name = param.name,
                        @in = param.@in,
                        description = param.description,
                        required = param.required,
                        schema = schemaCache.GetResolvedSchema(param.schema, swaggerFilePath, null, swaggerSpec.definitions),
                        type = param.type,
                        format = param.format,
                        items = param.items,
                        collectionFormat = param.collectionFormat,
                        @default = param.@default,
                        maximum = param.maximum,
                        exclusiveMaximum = param.exclusiveMaximum,
                        minimum = param.minimum,
                        exclusiveMinimum = param.exclusiveMinimum,
                        maxLength = param.maxLength,
                        minLength = param.minLength,
                        pattern = param.pattern,
                        maxItems = param.maxItems,
                        minItems = param.minItems,
                        uniqueItems = param.uniqueItems,
                        multipleOf = param.multipleOf,
                        patternedObjects = param.patternedObjects,
                        @ref = param.@ref,
                    };
                    ret.SwaggerApiViewGlobalParameters.Add(key, swaggerApiViewParameter);
                }
            }

            if (swaggerSpec.definitions != null)
            {
                foreach (var definition in swaggerSpec.definitions)
                {
                    ret.SwaggerApiViewDefinitions.Add(definition.Key, definition.Value);
                }
            }

            ret.Paths.SortByMethod();
            return ret;
        }

        public static void AddDefinitionsToCache(Swagger swaggerSpec, string swaggerFilePath, SchemaCache schemaCache)
        {
            var fullPath = Path.GetFullPath(swaggerFilePath);
            if (swaggerSpec.definitions != null)
            {
                foreach (var definition in swaggerSpec.definitions)
                {
                    if (!schemaCache.Cache.ContainsKey(definition.Key))
                    {
                        schemaCache.AddSchema(fullPath, definition.Key, definition.Value);
                    }
                }
            }


            if (swaggerSpec.parameters != null)
            {
                foreach (var parameter in swaggerSpec.parameters)
                {
                    if (!schemaCache.ParametersCache.ContainsKey(parameter.Key))
                    {
                        schemaCache.AddParameter(fullPath, parameter.Key, parameter.Value);
                    }
                }
            }

            if (swaggerSpec.responses != null)
            {
                foreach (var response in swaggerSpec.responses)
                {
                    if (!schemaCache.ResponsesCache.ContainsKey(response.Key))
                    {
                        schemaCache.AddResponse(fullPath, response.Key, response.Value);
                    }
                }
            }
        }
    }
}
