using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ApiView;
using APIView;
using swagger_api_parser;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class TokenSerializerTest
{
    private readonly ITestOutputHelper output;

    public TokenSerializerTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task TestTokenSerializerGeneral()
    {
        var general = new General {swagger = "2.0", info = {description = "sample", title = "sample swagger"}};
        var jsonDoc = JsonSerializer.SerializeToDocument(general);


        var ret = Visitor.GenerateCodeFileTokens(jsonDoc);

        this.output.WriteLine(ret.ToString());

        CodeFile codeFile = new CodeFile()
        {
            Tokens = ret,
            Language = "Swagger",
            VersionString = "0",
            Name = "tmp",
            PackageName = "tmp",
            Navigation = new NavigationItem[
            ] { }
        };

        var outputFilePath = Path.GetFullPath("./part_output.json");

        this.output.WriteLine($"Write result to: {outputFilePath}");
        await using FileStream writer = File.Open(outputFilePath, FileMode.Create);
        await codeFile.SerializeAsync(writer);
    }

    [Fact]
    public void TestSortSwaggerApiViewOperation()
    {
        List<SwaggerApiViewOperation> operations = new List<SwaggerApiViewOperation>() { };

        SwaggerApiViewOperation getA = new SwaggerApiViewOperation() {method = "get", operationId = "getA"};
        SwaggerApiViewOperation getB = new SwaggerApiViewOperation() {method = "get", operationId = "getB"};
        SwaggerApiViewOperation putA = new SwaggerApiViewOperation() {method = "put", operationId = "putA"};
        SwaggerApiViewOperation postA = new SwaggerApiViewOperation() {method = "post", operationId = "postA", path = "/resource/{resourceName}"};
        SwaggerApiViewOperation postActionA = new SwaggerApiViewOperation() {method = "post", operationId = "postAction", path = "/resource/{resourceName}/restart"};
        SwaggerApiViewOperation postActionB = new SwaggerApiViewOperation() {method = "post", operationId = "postActionB", path = "/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachines/{vmName}/installPatches"};

        operations.Add(postActionB);
        operations.Add(getA);
        operations.Add(getB);
        operations.Add(putA);
        operations.Add(postA);
        operations.Add(postActionA);



        var comp = new SwaggerApiViewOperationComp();
        operations.Sort(comp);

        string[] expect = new[] {"post", "put", "get", "get", "post", "post"};
        List<string> actual = operations.Select(operation => operation.method).ToList();
        Assert.Equal(expect, actual);
    }
}
