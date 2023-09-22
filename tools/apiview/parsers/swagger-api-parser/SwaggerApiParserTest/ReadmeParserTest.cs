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
        var inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, "default");
        Assert.Equal(43, inputFile.ToList().Count);
    }

    [Fact]
    public void TestGetSwaggerFileFromReadmeForAppConfiguration()
    {
        const string readmeFilePath = "./fixtures/appconfigurationreadme.md";
        var inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, "default");
        var enumerable = inputFile as string[] ?? inputFile.ToArray();
        Assert.Equal("Microsoft.AppConfiguration/stable/2022-05-01/appconfiguration.json", enumerable.ToArray()[0]);
        Assert.Single(enumerable.ToList());


        inputFile = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, "package-2020-06-01");
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
        var inputFiles = ReadmeParser.GetSwaggerFilesFromReadme(readmeFilePath, "package-2023-02");
        Assert.Collection(inputFiles, x => Assert.Equal("a.json", x), x => Assert.Equal("z.json", x));
    }
}
