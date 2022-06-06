using SwaggerApiParser;

namespace SwaggerApiParser
{
    public class SwaggerApiViewGenerator
    {
        public static SwaggerApiViewSpec GenerateSwaggerApiView(SwaggerSpec swaggerSpec, string fileName = "swagger.json", string packageName = "")
        {
            SwaggerApiViewSpec ret = new SwaggerApiViewSpec {SwaggerApiViewGeneral = {info = swaggerSpec.info, swagger = swaggerSpec.swagger, host = swaggerSpec.host}, fileName = fileName, packageName = packageName};

            foreach (var (currentPath, operations) in swaggerSpec.paths)
            {
                foreach (var (key, value) in operations)
                {
                    SwaggerApiViewOperation op = new SwaggerApiViewOperation
                    {
                        operation = value,
                        method = key,
                        path = currentPath,
                        operationId = value.operationId,
                        operationIdPrefix = Utils.GetOperationIdPrefix(value.operationId),
                        operationIdAction = Utils.GetOperationIdAction(value.operationId)
                    };
                    ret.Paths.AddSwaggerApiViewOperation(op);
                }
            }

            ret.Paths.SortByMethod();
            return ret;
        }
    }
}
