using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace UtilityLibraries;

public class GarbledTextValidation : IValidation
{
    private IBrowser _browser;

    public List<IgnoreItem> ignoreList = IgnoreData.GetIgnoreList("GarbledTextValidation", "contains");

    public GarbledTextValidation(IBrowser browser)
    {
        _browser = browser ?? throw new ArgumentNullException(nameof(browser));
    }

    public async Task<TResult> Validate(string testLink)
    {
        var res = new TResult();
        var errorList = new List<string>();

        //Create a new page from the shared browser instance.
        var page = await _browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        // Get visible text to check for text-based garbled content like ":param:", ":ivar:", etc.
        var htmlText = await page.Locator("html").InnerTextAsync();

        // Usage: This regular expression is used to extract the garbled characters:
        // 1. ":ivar:request_id:" / ":param cert_file:" / ":param str proxy_addr:" format
        // 2. Dictionary with special characters like "Dictionary of <...·...>"
        // Example: Initializer for X509 Certificate :param cert_file: The file path to contents of the certificate (or certificate chain)used to authenticate the device.
        // Link: https://learn.microsoft.com/en-us/python/api/azure-iot-device/azure.iot.device?view=azure-python
        string pattern = @":[\w]+(?:\s+[\w]+){0,2}:|Dictionary of <[^>]*·[^>]*>|Dictionary of <[^>]*\uFFFD[^>]*>|<xref[^>]*>";
        MatchCollection matches = Regex.Matches(htmlText, pattern);

        // Add the results of regular matching to errorList in a loop.
        foreach (Match match in matches)
        {
            //Judge if an element is in the ignoreList.
            bool shouldIgnore = ignoreList.Any(item => string.Equals(item.IgnoreText, match.Value, StringComparison.OrdinalIgnoreCase));

            //If it is not an ignore element, it means that it is garbled text.
            if (!shouldIgnore)
            {
                errorList.Add(match.Value);
            }
        }

        var formattedList = errorList
            .GroupBy(item => item)
            .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times , garbled text :   `{group.Key}`")
            .ToList();

        if (errorList.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.NumberOfOccurrences = errorList.Count;
            res.ErrorInfo = "The test link has garbled text.";
            res.LocationsOfErrors = formattedList;
        }

        await page.CloseAsync();

        return res;
    }
}
