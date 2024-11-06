using System.Linq;
using SwaggerApiParser;
using Xunit;
using Xunit.Abstractions;

namespace SwaggerApiParserTest;

public class ReadmeParserTest
{
    private readonly ITestOutputHelper output;

    public ReadmeParserTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TestParseReadme()
    {
        const string readmeFilePath = "./fixtures/apimanagementReadme.md";
        var tag = "default";
        var inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, ref tag);
        Assert.Equal(43, inputFile.ToList().Count);
    }

    [Fact]
    public void TestGetSwaggerFileFromReadmeForAppConfiguration()
    {
        const string readmeFilePath = "./fixtures/appconfigurationreadme.md";
        var tag = "default";
        var inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, ref tag);
        var enumerable = inputFile as string[] ?? inputFile.ToArray();
        Assert.Equal("Microsoft.AppConfiguration/stable/2022-05-01/appconfiguration.json", enumerable.ToArray()[0]);
        Assert.Single(enumerable.ToList());

        tag = "package-2020-06-01";
        inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, ref tag);
        enumerable = inputFile as string[] ?? inputFile.ToArray();
        Assert.Equal("Microsoft.AppConfiguration/stable/2020-06-01/appconfiguration.json", enumerable.ToArray()[0]);
        Assert.Single(enumerable.ToList());
    }

    [Fact]
    public void TestGetTagFromYamlArguments()
    {
        string input = "$(tag) == 'package-preview-2021-04'";
        string result = ReadmeParser.GetTagFromYamlArguments(input);
        this.output.WriteLine(result);
        Assert.Equal("package-preview-2021-04", result);

        input = "$(tag) == 'package-2018-06-preview'";
        result = ReadmeParser.GetTagFromYamlArguments(input);
        this.output.WriteLine(result);
        Assert.Equal("package-2018-06-preview", result);

        input = "$(tag) == 'package-2019-01'";
        result = ReadmeParser.GetTagFromYamlArguments(input);
        this.output.WriteLine(result);
        Assert.Equal("package-2019-01", result);
        
    }

    [Fact]
    public void TestGetTagFromYamlArgumentsInvalidCase()
    {
        var input = "$(tag) ==package-2019-01";
        var result = ReadmeParser.GetTagFromYamlArguments(input);
        this.output.WriteLine(result);
        Assert.Equal("", result);
        
        input = "$tag =package-2019-01";
        result = ReadmeParser.GetTagFromYamlArguments(input);
        this.output.WriteLine(result);
        Assert.Equal("", result);
        
    }

    [Fact]
    public void TestOrderedInputFiles()
    {
        const string readmeFilePath = "./fixtures/unordered.md";
        var tag = "package-2023-02";
        var inputFiles = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, ref tag);
        Assert.Collection(inputFiles, x => Assert.Equal("a.json", x), x => Assert.Equal("z.json", x));
    }

    [Fact]
    public void TestTagRetrievalUsingGetSwaggerFilesFromReadme()
    {
        string readmeFilePath = "./fixtures/appconfigurationreadme.md";
        var tag = "default";
        var inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, ref tag);
        Assert.Equal("package-2022-05-01", tag);

        readmeFilePath = "./fixtures/unordered.md";
        tag = "package-2023-02";
        inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, ref tag);
        Assert.Equal("package-2023-02", tag);
    }
}
