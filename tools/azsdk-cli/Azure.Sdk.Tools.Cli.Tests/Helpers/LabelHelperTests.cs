using Azure.Sdk.Tools.Cli.Helpers;
using Azure.Sdk.Tools.Cli.Tests.TestHelpers;
using Azure.Sdk.Tools.Cli.Configuration;


namespace Azure.Sdk.Tools.Cli.Tests.Helpers;

internal class LabelHelperTests
{
    [Test]
    [TestCase("TestService,Description,e99695\nAnotherService,Description2,e99695", "TestService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("TestService,Description,e99695\r\nAnotherService,Description2,e99695\nThirdService,Description3,e99695", "AnotherService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("TestService,Description,e99695\nAnotherService,Description2,e99695\r\n", "TestService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("TestService,Description,e99695\nAnotherService,Description2,e99695", "NonExistentService", LabelHelper.ServiceLabelStatus.DoesNotExist)]
    [TestCase("TestService,Description,123456\nAnotherService,Description2,e99695", "TestService", LabelHelper.ServiceLabelStatus.NotAServiceLabel)]
    [TestCase("Service - TestService,Description with commas and stuff,e99695\nAnotherService,Description2,e99695", "Service - TestService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("   \nAnotherService,Description2,e99695", "AnotherService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("TestService,Description\nAnotherService,Description2,e99695", "AnotherService", LabelHelper.ServiceLabelStatus.Exists)]
    public void TestCheckServiceLabel(string csvContent, string serviceLabel, LabelHelper.ServiceLabelStatus expected)
    {
        var actual = LabelHelper.CheckServiceLabel(csvContent, serviceLabel);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("AAA,,e99695\nCCC,,e99695\nZZZ,,e99695", "BBB", "AAA,,e99695\nBBB,,e99695\nCCC,,e99695\nZZZ,,e99695\n")]
    [TestCase("BBB,,e99695\nCCC,,e99695\nZZZ,,e99695", "AAA", "AAA,,e99695\nBBB,,e99695\nCCC,,e99695\nZZZ,,e99695\n")]
    [TestCase("AAA,,e99695\nBBB,,e99695\nCCC,,e99695", "ZZZ", "AAA,,e99695\nBBB,,e99695\nCCC,,e99695\nZZZ,,e99695\n")]
    [TestCase("", "TestService", "TestService,,e99695\n")]
    [TestCase("AAA,,e99695", "BBB", "AAA,,e99695\nBBB,,e99695\n")]
    [TestCase("AAA,,e99695\r\nCCC,,e99695", "BBB", "AAA,,e99695\r\nBBB,,e99695\nCCC,,e99695\n")]
    [TestCase("AAA,,e99695\nCCC,,e99695\nZZZ,,e99695", "CCC", "AAA,,e99695\nCCC,,e99695\nCCC,,e99695\nZZZ,,e99695\n")]
    [TestCase("AAA,,e99695\n\nZZZ,,e99695", "BBB", "AAA,,e99695\nBBB,,e99695\nZZZ,,e99695\n")] // Test for empty line handling
    [TestCase("AAA,,e99695\n   \nZZZ,,e99695", "BBB", "AAA,,e99695\nBBB,,e99695\nZZZ,,e99695\n")] // Test for whitespace line handling
    public void TestCreateServiceLabel(string csvContent, string serviceLabel, string expected)
    {
        var actual = LabelHelper.CreateServiceLabel(csvContent, serviceLabel);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("Test - Service", "test-service")]
    [TestCase("New Test Service", "new-test-service")]
    [TestCase("Test/Service", "test-service")]
    [TestCase("  Test Service  ", "test-service")]
    [TestCase("-Test Service-", "test-service")]
    [TestCase("  -Test Service-  ", "test-service")]
    public void TestNormalizeLabel(string input, string expected)
    {
        var actual = LabelHelper.NormalizeLabel(input);
        Assert.That(actual, Is.EqualTo(expected));
    }
}
