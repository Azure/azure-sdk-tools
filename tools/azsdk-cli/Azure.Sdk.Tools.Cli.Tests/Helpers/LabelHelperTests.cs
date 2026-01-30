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

    [Test]
    // Basic cases - returns only service labels (color e99695)
    [TestCase("ServiceA,,e99695", "ServiceA")]
    [TestCase("ServiceA,,e99695\nServiceB,,e99695", "ServiceA,ServiceB")]
    [TestCase("ServiceA,,e99695\nServiceB,,e99695\nServiceC,,e99695", "ServiceA,ServiceB,ServiceC")]
    // Filters out non-service labels (different color codes)
    [TestCase("ServiceA,,e99695\nNonService,,123456\nServiceB,,e99695", "ServiceA,ServiceB")]
    [TestCase("NonService,,123456", "")]
    // Case-insensitive color code matching
    [TestCase("ServiceA,,E99695\nServiceB,,e99695", "ServiceA,ServiceB")]
    // Handles empty/whitespace lines
    [TestCase("ServiceA,,e99695\n\nServiceB,,e99695", "ServiceA,ServiceB")]
    [TestCase("ServiceA,,e99695\n   \nServiceB,,e99695", "ServiceA,ServiceB")]
    // Skips incomplete lines (less than 3 columns)
    [TestCase("ServiceA,,e99695\nIncomplete\nServiceB,,e99695", "ServiceA,ServiceB")]
    [TestCase("ServiceA,,e99695\nTwo,Columns\nServiceB,,e99695", "ServiceA,ServiceB")]
    // Handles Windows line endings
    [TestCase("ServiceA,,e99695\r\nServiceB,,e99695", "ServiceA,ServiceB")]
    // Empty CSV
    [TestCase("", "")]
    public void TestGetAllServiceLabels(string csvContent, string expectedLabelsCsv)
    {
        var result = LabelHelper.GetAllServiceLabels(csvContent);
        var expected = string.IsNullOrEmpty(expectedLabelsCsv) 
            ? new List<string>() 
            : expectedLabelsCsv.Split(',').ToList();

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    // No duplicates - returns false with empty list
    [TestCase("ServiceA,ServiceB,ServiceC", false, "")]
    [TestCase("ServiceA", false, "")]
    [TestCase("", false, "")]
    // With duplicates - returns true with duplicate labels
    [TestCase("ServiceA,ServiceB,ServiceA", true, "ServiceA")]
    [TestCase("ServiceA,ServiceA", true, "ServiceA")]
    // Case-insensitive duplicate detection
    [TestCase("ServiceA,SERVICEA", true, "ServiceA")]
    [TestCase("ServiceA,servicea,SERVICEA", true, "ServiceA")]
    [TestCase("Service-A,SERVICE-A,service-a", true, "Service-A")]
    // Multiple different duplicates
    [TestCase("ServiceA,ServiceB,ServiceA,ServiceB", true, "ServiceA,ServiceB")]
    [TestCase("A,B,C,A,B,C", true, "A,B,C")]
    public void TestTryFindDuplicateLabels(string inputLabelsCsv, bool expectedResult, string expectedDuplicatesCsv)
    {
        var labels = string.IsNullOrEmpty(inputLabelsCsv) 
            ? new List<string>() 
            : inputLabelsCsv.Split(',').ToList();
        var expectedDuplicates = string.IsNullOrEmpty(expectedDuplicatesCsv) 
            ? new List<string>() 
            : expectedDuplicatesCsv.Split(',').ToList();

        var result = LabelHelper.TryFindDuplicateLabels(labels, out var duplicates);

        Assert.That(result, Is.EqualTo(expectedResult));
        Assert.That(duplicates.Count, Is.EqualTo(expectedDuplicates.Count));
        CollectionAssert.AreEqual(expectedDuplicates, duplicates, "Duplicate lists do not match");
    }

    [Test]
    // Test normalization removes # prefix
    [TestCase("#e99695", "e99695")]
    [TestCase("e99695", "e99695")]
    [TestCase("#E99695", "e99695")]
    [TestCase("E99695", "e99695")]
    // Test with other colors
    [TestCase("#ff0000", "ff0000")]
    [TestCase("FF0000", "ff0000")]
    // Test edge cases
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase("#", "")]
    public void TestNormalizeColorForComparison(string input, string expected)
    {
        var result = LabelHelper.NormalizeColorForComparison(input);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    // Test colors are equal regardless of # prefix
    [TestCase("#e99695", "e99695", true)]
    [TestCase("e99695", "#e99695", true)]
    [TestCase("#e99695", "#e99695", true)]
    [TestCase("e99695", "e99695", true)]
    // Test case-insensitive comparison
    [TestCase("#E99695", "e99695", true)]
    [TestCase("E99695", "#e99695", true)]
    [TestCase("#e99695", "E99695", true)]
    // Test different colors are not equal
    [TestCase("e99695", "ff0000", false)]
    [TestCase("#e99695", "#ff0000", false)]
    [TestCase("e99695", "#ff0000", false)]
    // Test edge cases
    [TestCase("", "", true)]
    [TestCase("e99695", "", false)]
    [TestCase("", "e99695", false)]
    public void TestAreColorsEqual(string color1, string color2, bool expected)
    {
        var result = LabelHelper.AreColorsEqual(color1, color2);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    // Test that CheckServiceLabel works with colors that have # prefix
    [TestCase("TestService,,#e99695\nAnotherService,,e99695", "TestService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("TestService,,e99695\nAnotherService,,#e99695", "AnotherService", LabelHelper.ServiceLabelStatus.Exists)]
    [TestCase("TestService,,#E99695\nAnotherService,,e99695", "TestService", LabelHelper.ServiceLabelStatus.Exists)]
    public void TestCheckServiceLabelWithHashPrefix(string csvContent, string serviceLabel, LabelHelper.ServiceLabelStatus expected)
    {
        var actual = LabelHelper.CheckServiceLabel(csvContent, serviceLabel);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    // Test that GetAllServiceLabels works with colors that have # prefix
    [TestCase("ServiceA,,#e99695", "ServiceA")]
    [TestCase("ServiceA,,#e99695\nServiceB,,e99695", "ServiceA,ServiceB")]
    [TestCase("ServiceA,,e99695\nServiceB,,#e99695\nServiceC,,#E99695", "ServiceA,ServiceB,ServiceC")]
    [TestCase("ServiceA,,#e99695\nNonService,,#123456\nServiceB,,e99695", "ServiceA,ServiceB")]
    public void TestGetAllServiceLabelsWithHashPrefix(string csvContent, string expectedLabelsCsv)
    {
        var result = LabelHelper.GetAllServiceLabels(csvContent);
        var expected = string.IsNullOrEmpty(expectedLabelsCsv) 
            ? new List<string>() 
            : expectedLabelsCsv.Split(',').ToList();

        Assert.That(result, Is.EqualTo(expected));
    }
}
