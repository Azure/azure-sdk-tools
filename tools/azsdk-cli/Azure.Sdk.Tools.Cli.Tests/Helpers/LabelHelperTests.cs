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

    [Test]
    public void TestCreateServiceLabelInsertion()
    {
        var csvContent = "SMR,,e99695\nRRC,,e99695\nPVLG,,e99695\nSBEC,,e99695";
        var serviceLabel = "TestService";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "PVLG,,e99695\nRRC,,e99695\nSBEC,,e99695\nSMR,,e99695\nTestService,,e99695";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestCreateServiceLabelEmptyCSV()
    {
        var csvContent = "";
        var serviceLabel = "TestService";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "TestService,,e99695";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestNormalizeLabel_WithSpacesAndDashes()
    {
        var actual = labelHelper.NormalizeLabel("Test - Service");
        Assert.That(actual, Is.EqualTo("test-service"));
    }

    [Test]
    public void TestNormalizeLabel_WithSpaces()
    {
        var actual = labelHelper.NormalizeLabel("New Test Service");
        Assert.That(actual, Is.EqualTo("new-test-service"));
    }

    [Test]
    public void TestNormalizeLabel_WithSlash()
    {
        var actual = labelHelper.NormalizeLabel("Test/Service");
        Assert.That(actual, Is.EqualTo("test-service"));
    }
}
