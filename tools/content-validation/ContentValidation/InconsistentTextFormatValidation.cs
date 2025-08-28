using Microsoft.Playwright;

namespace UtilityLibraries;

public class InconsistentTextFormatValidation : IValidation
{
    private IPlaywright _playwright;

    public InconsistentTextFormatValidation(IPlaywright playwright)
    {
        _playwright = playwright;
    }

    public async Task<TResult> Validate(string testLink)
    {
        //Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        var res = new TResult();
        var errorList = new HashSet<string>();
        var errorLocation = new List<string>();

        var hTagsInTd = await page.QuerySelectorAllAsync("td h1, td h2, td h3, td h4, td h5, td h6");

        if (hTagsInTd.Count > 0)
        {
            foreach (var element in hTagsInTd)
            {
                var rawText = await element.EvaluateAsync<string>("el => el.textContent");
                var text = rawText?.Trim() ?? "";

                if (string.IsNullOrEmpty(text))
                {
                    continue; // Skip empty headings
                }

                errorList.Add(text);
                var anchorLink = await GetAnchorLinkForCellAsync(element, page, testLink);
                errorLocation.Add(anchorLink);
            }

            // Format the error list
            var formattedList = errorLocation
                .GroupBy(item => item)
                .Select((group, index) => $"{index + 1}. Appears {group.Count()} times , location : {group.Key}")
                .ToList();

            if (errorList.Count > 0)
            {
                res.Result = false;
                res.ErrorLink = testLink;
                res.NumberOfOccurrences = hTagsInTd.Count;
                res.ErrorInfo = $"Inconsistent Text Format: `{string.Join("`, `", errorList)}`";
                res.LocationsOfErrors = formattedList;
            }
        }

        await browser.CloseAsync();

        return res;
    }

    private async Task<string> GetAnchorLinkForCellAsync(IElementHandle cell, IPage page, string testLink)
    {
        string anchorLink = "No anchor link found, need to manually search for empty cells on the page.";

        var nearestHTagText = await cell.EvaluateAsync<string?>(@"element => {
            function findNearestHeading(startNode) {
                let currentNode = startNode;
                while (currentNode && currentNode.tagName !== 'BODY' && currentNode.tagName !== 'HTML') {
                    let sibling = currentNode.previousElementSibling;
                    while (sibling) {
                        if (['H2'].includes(sibling.tagName)) {
                            return sibling.textContent?.trim() || '';
                        }
                        let childHeading = findNearestHeadingInChildren(sibling);
                        if (childHeading) {
                            return childHeading;
                        }
                        sibling = sibling.previousElementSibling;
                    }
                    currentNode = currentNode.parentElement;
                }
                return null;
            }

            function findNearestHeadingInChildren(node) {
                for (let child of node.children) {
                    if (['H2'].includes(child.tagName)) {
                        return child.textContent?.trim() || '';
                    }
                    let grandChildHeading = findNearestHeadingInChildren(child);
                    if (grandChildHeading) {
                        return grandChildHeading;
                    }
                }
                return null;
            }
            
            return findNearestHeading(element);
        }");

        if (!string.IsNullOrEmpty(nearestHTagText))
        {
            nearestHTagText = nearestHTagText.Replace("\n", "").Replace("\t", "");            

            var aLocators = page.Locator("#side-doc-outline a");
            var aElements = await aLocators.ElementHandlesAsync();

            foreach (var elementHandle in aElements)
            {
                var linkText = await elementHandle.EvaluateAsync<string>("el => el.textContent?.trim()");
                if (linkText == nearestHTagText)
                {
                    var href = await elementHandle.GetAttributeAsync("href");
                    if (!string.IsNullOrEmpty(href))
                    {
                        anchorLink = testLink + href;
                        break;
                    }
                }
            }
        }

        return anchorLink;
    }
}