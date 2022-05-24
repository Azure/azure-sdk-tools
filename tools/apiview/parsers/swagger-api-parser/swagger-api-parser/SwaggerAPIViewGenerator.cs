namespace swagger_api_parser
{
    public class SwaggerApiViewGenerator
    {
        public static SwaggerApiViewSpec GenerateSwaggerApiView(SwaggerSpec swaggerSpec)
        {
            SwaggerApiViewSpec ret = new SwaggerApiViewSpec {General = {info = swaggerSpec.info, swagger = swaggerSpec.swagger}};

            foreach (var (currentPath, operations) in swaggerSpec.paths)
            {
                foreach (var (key, value) in operations)
                {
                    SwaggerAPIViewOperation op = new SwaggerAPIViewOperation
                    {
                        operation = value,
                        method = key,
                        path = currentPath,
                        operationId = value.operationId,
                        operationIdPrefix = Utils.GetOperationIdPrefix(value.operationId),
                        operationIdAction = Utils.GetOperationIdAction(value.operationId)
                    };
                    ret.AddSwaggerApiViewOperation(op);
                }
            }

            return ret;
        }
    }
}
