using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Configuration;


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
        var csvContent = "Service - TestService,Description with commas and stuff,e99695\nAnotherService,Description2,e99695";
        
        var actual1 = labelHelper.CheckServiceLabel(csvContent, "Service - TestService");
        var actual2 = labelHelper.CheckServiceLabel(csvContent, "AnotherService");
        
        Assert.That(actual1, Is.EqualTo(LabelHelper.ServiceLabelStatus.Exists));
        Assert.That(actual2, Is.EqualTo(LabelHelper.ServiceLabelStatus.Exists));
    }

    [Test]
    public void TestCreateServiceLabelInsertion()
    {
        // Test that a service label is inserted in the correct alphabetical position
        var csvContent = "AAA,,e99695\nCCC,,e99695\nZZZ,,e99695";
        var serviceLabel = "BBB";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "AAA,,e99695\nBBB,,e99695\nCCC,,e99695\nZZZ,,e99695";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestCreateServiceLabelInsertionAtBeginning()
    {
        // Test insertion at the beginning
        var csvContent = "BBB,,e99695\nCCC,,e99695\nZZZ,,e99695";
        var serviceLabel = "AAA";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "AAA,,e99695\nBBB,,e99695\nCCC,,e99695\nZZZ,,e99695";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestCreateServiceLabelInsertionAtEnd()
    {
        // Test insertion at the end
        var csvContent = "AAA,,e99695\nBBB,,e99695\nCCC,,e99695";
        var serviceLabel = "ZZZ";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "AAA,,e99695\nBBB,,e99695\nCCC,,e99695\nZZZ,,e99695";
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void TestCreateServiceLabelEmptyCSV()
    {
        var csvContent = "";
        var serviceLabel = "TestService";
        var actual = labelHelper.CreateServiceLabel(csvContent, serviceLabel);
        var expected = "\nTestService,,e99695";
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
