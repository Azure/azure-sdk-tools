using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;

namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class LabelHelperTests
{
    private ILabelHelper labelHelper;
    private TestLogger<LabelHelper> testLogger;

    [SetUp]
    public void Setup()
    {
        testLogger = new TestLogger<LabelHelper>();
        labelHelper = new LabelHelper(testLogger);
    }

    [Test]
    public async Task TestCheckServiceLabel_FindsServiceLabel()
    {
        var csvContent = "TestService,Description,e99695\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "TestService");
        Assert.That(actual, Is.EqualTo("TestService"));
    }

    [Test]
    public async Task TestCheckServiceLabel_DoesNotFindServiceLabel()
    {
        var csvContent = "TestService,Description,e99695\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "NonExistentService");
        Assert.That(actual, Is.Null);
    }

    [Test]
    public async Task TestCheckServiceLabel_ColorCodeDoesNotMatch()
    {
        var csvContent = "TestService,Description,123456\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "TestService");
        Assert.That(actual, Is.Null);
    }

    [Test]
    public async Task TestPraseCsvLine_Stuff()
    {
        // Actual value:
        // "Test,Service",Description\",e99695
        var line = "\"Test,Service\",Description\\\",e99695";

        var columns = LabelHelper.ParseCsvLine(line);
        Assert.That(columns.Count, Is.EqualTo(3));
        Assert.That(columns[0], Is.EqualTo("Test,Service"));
        Assert.That(columns[1], Is.EqualTo("Description\""));
        Assert.That(columns[2], Is.EqualTo("e99695"));
    }

}
