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
    public void TestCheckServiceLabel_FindsServiceLabel()
    {
        var csvContent = "TestService,Description,e99695\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "TestService");
        Assert.That(actual, Is.True);
    }

    [Test]
    public void TestCheckServiceLabel_DoesNotFindServiceLabel()
    {
        var csvContent = "TestService,Description,e99695\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "NonExistentService");
        Assert.That(actual, Is.False);
    }

    [Test]
    public void TestCheckServiceLabel_ColorCodeDoesNotMatch()
    {
        var csvContent = "TestService,Description,123456\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "TestService");
        Assert.That(actual, Is.False);
    }

    [Test]
    public void TestCheckServiceLabel_WithComplexCsvFormat()
    {

        var csvContent = "\"Service - TestService\",\"Description with commas, and stuff\\\",e99695\nAnotherService,Description2,e99695";

        var column = LabelHelper.ParseCsvLine(csvContent.Split('\n')[0]);
        Assert.That(column.Count, Is.EqualTo(3));
        Assert.That(column[0], Is.EqualTo("Service - TestService"));
        Assert.That(column[1], Is.EqualTo("Description with commas, and stuff\\"));
        Assert.That(column[2], Is.EqualTo("e99695"));

        var actual = labelHelper.CheckServiceLabel(csvContent, "Service - TestService");
        Assert.That(actual, Is.True);
    }

}
