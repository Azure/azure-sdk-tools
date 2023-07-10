using System.Collections.Generic;
using System.Linq;
using SwaggerApiParser.SwaggerApiView;
using Xunit;

namespace SwaggerApiParserTest 
{
    public class SwaggerApiViewOperationTest
    {
        [Fact]
        public void TestSortSwaggerApiViewOperation()
        {
            List<SwaggerApiViewOperation> operations = new List<SwaggerApiViewOperation>() { };

            SwaggerApiViewOperation getA = new SwaggerApiViewOperation() { method = "get", operationId = "getA" };
            SwaggerApiViewOperation getB = new SwaggerApiViewOperation() { method = "get", operationId = "getB" };
            SwaggerApiViewOperation putA = new SwaggerApiViewOperation() { method = "put", operationId = "putA" };
            SwaggerApiViewOperation postA = new SwaggerApiViewOperation() { method = "post", operationId = "postA", path = "/resource/{resourceName}" };
            SwaggerApiViewOperation postActionA = new SwaggerApiViewOperation() { method = "post", operationId = "postAction", path = "/resource/{resourceName}/restart" };
            SwaggerApiViewOperation postActionB = new SwaggerApiViewOperation() { method = "post", operationId = "postActionB", path = "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/installPatches" };

            operations.Add(postActionB);
            operations.Add(getA);
            operations.Add(getB);
            operations.Add(putA);
            operations.Add(postA);
            operations.Add(postActionA);



            var comp = new SwaggerApiViewOperationComp();
            operations.Sort(comp);

            string[] expect = new[] { "post", "put", "get", "get", "post", "post" };
            List<string> actual = operations.Select(operation => operation.method).ToList();
            Assert.Equal(expect, actual);
        }
    }
}
