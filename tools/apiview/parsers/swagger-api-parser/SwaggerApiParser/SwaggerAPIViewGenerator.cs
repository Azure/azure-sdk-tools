using System.Collections.Generic;
using System.IO;

namespace SwaggerApiParser
{
    public class SwaggerApiViewGenerator
    {
        public static SwaggerApiViewSpec GenerateSwaggerApiView(SwaggerSpec swaggerSpec, string swaggerFilePath, SchemaCache schemaCache, string packageName = "")
        {
            SwaggerApiViewSpec ret = new SwaggerApiViewSpec
            {
                SwaggerApiViewGeneral =
                {
                    info = swaggerSpec.info,
                    swagger = swaggerSpec.swagger,
                    host = swaggerSpec.host,
                    schemes = swaggerSpec.schemes,
                    consumes = swaggerSpec.consumes,
                    produces = swaggerSpec.produces,
                    security = swaggerSpec.security,
                    securityDefinitions = swaggerSpec.securityDefinitions,
                    xMsParameterizedHost = swaggerSpec.xMsParameterizedHost
                },
                fileName = Path.GetFileName(swaggerFilePath),
                packageName = packageName
            };

            foreach (var definition in swaggerSpec.definitions)
            {
                if (!schemaCache.Cache.ContainsKey(definition.Key))
                {
                    schemaCache.AddSchema(swaggerFilePath, definition.Key, definition.Value);
                }
            }

            foreach (var (currentPath, operations) in swaggerSpec.paths)
            {
                if (operations == null)
                {
                    continue;
                }

                foreach (var (key, value) in operations)
                {
                    SwaggerApiViewOperation op = new SwaggerApiViewOperation
                    {
                        operation = value,
                        method = key,
                        path = currentPath,
                        operationId = value.operationId,
                        description = value.description,
                        summary = value.summary,
                        tags = value.tags,
                        procudes = value.produces,
                        consumes = value.consumes,
                        xMSPageable = value.xMsPageable,
                        operationIdPrefix = Utils.GetOperationIdPrefix(value.operationId),
                        operationIdAction = Utils.GetOperationIdAction(value.operationId),
                        PathParameters = new SwaggerApiViewOperationParameters("PathParameters"),
                        QueryParameters = new SwaggerApiViewOperationParameters("QueryParameters"),
                        BodyParameters = new SwaggerApiViewOperationParameters("BodyParameters"),
                        Responses = new List<SwaggerApiViewResponse>(),
                        xMsLongRunningOperation = value.xMsLongRunningOperaion
                    };
                    if (value.parameters != null)
                        foreach (var parameter in value.parameters)
                        {
                            var param = parameter;
                            if (parameter.IsRefObject())
                            {
                                param = (Parameter)swaggerSpec.ResolveRefObj(parameter.Ref);
                            }

                            var currentSwaggerFilePath = swaggerFilePath;
                            var swaggerApiViewOperationParameter = new SwaggerApiViewParameter
                            {
                                description = param.description,
                                name = param.name,
                                required = param.required,
                                format = param.format,
                                In = param.In,
                                schema = schemaCache.GetResolvedSchema(param.schema, currentSwaggerFilePath),
                                Ref = param.Ref,
                                type = param.type
                            };


                            switch (param.In)
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
                            }
                        }

                    {
                    }


                    foreach (var (statusCode, response) in value.responses)
                    {
                        var schema = response.schema;
                        var currentSwaggerFilePath = swaggerFilePath;

                        //Resolve ref obj for response schema.
                        if (response.schema != null)
                        {
                            schema = schemaCache.GetResolvedSchema(schema, currentSwaggerFilePath);
                        }

                        op.Responses.Add(new SwaggerApiViewResponse() {description = response.description, statusCode = statusCode, schema = schema});
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
                foreach (var kv in swaggerSpec.parameters)
                {
                    var param = kv.Value;
                    var swaggerApiViewParameter = new SwaggerApiViewParameter
                    {
                        description = param.description,
                        name = param.name,
                        required = param.required,
                        format = param.format,
                        In = param.In,
                        schema = schemaCache.GetResolvedSchema(param.schema, swaggerFilePath),
                        Ref = param.Ref,
                        type = param.type
                    };
                    ret.SwaggerApiViewGlobalParameters.Add(kv.Key, swaggerApiViewParameter);
                }
            }

            ret.Paths.SortByMethod();
            return ret;
        }
    }
}
