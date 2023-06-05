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
                packageName = packageName
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
                        operation = operation,
                        method = method,
                        path = currentPath,
                        operationId = operation.operationId,
                        tags = operation.tags,
                        summary = operation.summary,
                        description = operation.description,
                        produces = operation.produces,
                        consumes = operation.consumes,
                        operationIdPrefix = Utils.GetOperationIdPrefix(operation.operationId),
                        operationIdAction = Utils.GetOperationIdAction(operation.operationId),
                        PathParameters = new SwaggerApiViewOperationParameters("PathParameters"),
                        QueryParameters = new SwaggerApiViewOperationParameters("QueryParameters"),
                        BodyParameters = new SwaggerApiViewOperationParameters("BodyParameters"),
                        HeaderParameters = new SwaggerApiViewOperationParameters("HeaderParameters"),
                        Responses = new List<SwaggerApiViewResponse>(),
                    };

                    if (operation.parameters != null)
                    {
                        foreach (var parameter in operation.parameters)
                        {
                            var param = parameter;
                            if (parameter.IsRefObject())
                            {
                                param = (Parameter)swaggerSpec.ResolveRefObj(parameter.@ref) ?? schemaCache.GetParameterFromCache(parameter.@ref, swaggerFilePath);
                            }

                            var currentSwaggerFilePath = swaggerFilePath;

                            if (param == null)
                            {
                                // Need to handle cases where ref is many levels deep
                                if (!Path.IsPathFullyQualified(parameter.@ref))
                                {
                                    var referenceSwaggerFilePath = Utils.GetReferencedSwaggerFile(parameter.@ref, currentSwaggerFilePath);
                                    var referenceSwaggerSpec = await SwaggerDeserializer.Deserialize(referenceSwaggerFilePath);
                                    referenceSwaggerSpec.swaggerFilePath = Path.GetFullPath(referenceSwaggerFilePath);
                                    AddDefinitionsToCache(referenceSwaggerSpec, referenceSwaggerFilePath, schemaCache);
                                    param = schemaCache.GetParameterFromCache(parameter.@ref, currentSwaggerFilePath);
                                }
                                else
                                {
                                    continue;
                                }
                            }

                            var swaggerApiViewOperationParameter = new SwaggerApiViewParameter
                            {
                                name = param.name,
                                @in = param.@in,
                                description = param.description,
                                required = param.required,
                                schema = schemaCache.GetResolvedSchema(param.schema, currentSwaggerFilePath),
                                format = param.format,
                                @ref = param.@ref,
                                type = param.type
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
                        var schema = response.schema;
                        var currentSwaggerFilePath = swaggerFilePath;

                        //Resolve ref obj for response schema.

                        if (response.schema != null)
                        {
                            // The initial refChain is the root level schema.
                            // There are some scenarios that the property of the root level schema is a ref to the root level itself (circular reference).
                            // Like "errorDetail" schema in common types.
                            LinkedList<string> refChain = new LinkedList<string>();
                            schema = schemaCache.GetResolvedSchema(schema, currentSwaggerFilePath, refChain);
                        }

                        var headers = response.headers ?? new Dictionary<string, Header>();

                        op.Responses.Add(new SwaggerApiViewResponse() { description = response.description, statusCode = statusCode, schema = schema, headers = headers });
                    }

                    ret.Paths.AddSwaggerApiViewOperation(op);
                }
            }

            if (swaggerSpec.definitions != null)
            {
                foreach (var definition in swaggerSpec.definitions)
                {
                    ret.SwaggerApiViewDefinitions.Add(definition.Key, definition.Value);
                }
            }

            if (swaggerSpec.parameters != null)
            {
                foreach (var (key, value) in swaggerSpec.parameters)
                {
                    var param = value;
                    var swaggerApiViewParameter = new SwaggerApiViewParameter
                    {
                        description = param.description,
                        name = param.name,
                        required = param.required,
                        format = param.format,
                        @in = param.@in,
                        schema = schemaCache.GetResolvedSchema(param.schema, swaggerFilePath),
                        @ref = param.@ref,
                        type = param.type
                    };
                    ret.SwaggerApiViewGlobalParameters.Add(key, swaggerApiViewParameter);
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
        }
    }
}
