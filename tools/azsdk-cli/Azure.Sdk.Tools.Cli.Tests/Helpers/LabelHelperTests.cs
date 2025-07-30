using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;


namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class LabelHelperTests
{
    private ILabelHelper labelHelper;

    [SetUp]
    public void Setup()
    {
        labelHelper = new LabelHelper(new TestLogger<LabelHelper>());
    }

    [Test]
    public void TestCheckServiceLabel_FindsServiceLabel()
    {
        var csvContent = "TestService,Description,e99695\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "TestService");
        Assert.That(actual, Is.EqualTo(LabelHelper.ServiceLabelStatus.Exists));
    }

    [Test]
    public void TestCheckServiceLabel_DoesNotFindServiceLabel()
    {
        var csvContent = "TestService,Description,e99695\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "NonExistentService");
        Assert.That(actual, Is.EqualTo(LabelHelper.ServiceLabelStatus.DoesNotExist));
    }

    [Test]
    public void TestCheckServiceLabel_ColorCodeDoesNotMatch()
    {
        var csvContent = "TestService,Description,123456\nAnotherService,Description2,e99695";
        var actual = labelHelper.CheckServiceLabel(csvContent, "TestService");
        Assert.That(actual, Is.EqualTo(LabelHelper.ServiceLabelStatus.NotAServiceLabel));
    }

    [Test]
    public void TestCheckServiceLabel_WithComplexCsvFormat()
    {
        var csvContent = "\"Service - TestService\",\"Description with commas, and stuff\\\",e99695\nAnotherService,Description2,e99695";

        var records = LabelHelper.GetLabelsFromCsv(csvContent);
        Assert.That(records.Count, Is.EqualTo(2));
        Assert.That(records[0].Name, Is.EqualTo("Service - TestService"));
        Assert.That(records[0].Description, Is.EqualTo("Description with commas, and stuff\\"));
        Assert.That(records[0].Color, Is.EqualTo("e99695"));
        Assert.That(records[1].Name, Is.EqualTo("AnotherService"));
        Assert.That(records[1].Description, Is.EqualTo("Description2"));
        Assert.That(records[1].Color, Is.EqualTo("e99695"));
    }

    [Test]
    public void TestCreateServiceLabelInsertion()
    {
        var csvContent = "SMR,,e99695\nRRC,,e99695\nPVLG,,e99695\nSBEC,,e99695";
        var serviceLabel = "TestService";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "PVLG,,e99695\nRRC,,e99695\nSBEC,,e99695\nSMR,,e99695\nTestService,,e99695\n";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestCreateServiceLabelEmptyCSV()
    {
        var csvContent = "";
        var serviceLabel = "TestService";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "TestService,,e99695\n";
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
