using System.Collections.Generic;
using swagger_api_parser;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class UtilsTest
{
    private readonly ITestOutputHelper output;

    public UtilsTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TestGetCommonPath()
    {
        var paths = new List<string>() {"/api/v1/users", "/api/v1/users/{id}", "/api/v1/users/{id}/friends"};
        var commonPath = Utils.GetCommonPath(paths);
        Assert.Equal("/api/v1/users", commonPath);
    }
}
