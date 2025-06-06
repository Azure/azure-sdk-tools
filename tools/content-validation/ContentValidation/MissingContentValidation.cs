using Microsoft.Playwright;

namespace UtilityLibraries;

public class MissingContentValidation : IValidation
{
    private IPlaywright _playwright;

    public MissingContentValidation(IPlaywright playwright)
    {
        _playwright = playwright ?? throw new ArgumentNullException(nameof(playwright));
    }

    public async Task<TResult> Validate(string testLink)
    {
        var res = new TResult();
        var errorList = new List<string>();

        // Create a browser instance.
        var browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await PlaywrightHelper.GotoageWithRetriesAsync(page, testLink);

        // Fetch all th and td tags in the test page.
        var cellElements = await page.Locator("td,th").AllAsync();

        // Flag for ignore method clear, copy, items, keys, values
        bool skipFlag = false;
        List<string> ignoreList = new List<string>(){"clear", "copy", "items", "keys", "values"};

        // Check if the cell is empty. If it is, retrieve the href attribute of the anchor tag above it for positioning.
        foreach (var cell in cellElements)
        {
            if(skipFlag == true){
                skipFlag = false;
                continue;
            }
            
            var cellText = (await cell.InnerTextAsync()).Trim();
            if(ignoreList.Contains(cellText)){
                skipFlag = true;
            }
            // Console.WriteLine($"Cell Text: {cellText}");

            // Usage: Check if it is an empty cell and get the href attribute of the nearest <a> tag with a specific class name before it. Finally, group and format these errors by position and number of occurrences.
            // Example: The Description column of the Parameter table is Empty.
            // Link: https://learn.microsoft.com/en-us/python/api/azure-ai-textanalytics/azure.ai.textanalytics.aio.asyncanalyzeactionslropoller?view=azure-python
            if (string.IsNullOrEmpty(cellText))
            {
                // Console.WriteLine($"Cell Text: {cellText}");
                string anchorLink = "No anchor link found, need to manually search for empty cells on the page.";
                var nearestHTagText = await cell.EvaluateAsync<string?>(@"element => {
                    function findNearestHeading(startNode) {
                        let currentNode = startNode;
                        
                        while (currentNode && currentNode.tagName !== 'BODY' && currentNode.tagName !== 'HTML') {
                            // Check the sibling nodes and child nodes of the current node
                            let sibling = currentNode.previousElementSibling;
                            while (sibling) {
                                // Check if the sibling node itself is an <h2> or <h3>
                                if (['H2', 'H3'].includes(sibling.tagName)) {
                                    return sibling.textContent || '';
                                }
                                
                                // Recursively check the children of sibling nodes
                                let childHeading = findNearestHeadingInChildren(sibling);
                                if (childHeading) {
                                    return childHeading;
                                }
                                
                                sibling = sibling.previousElementSibling;
                            }
                            
                            // If not found in the sibling node, continue traversing upwards
                            currentNode = currentNode.parentElement;
                        }
                        
                        return null; // If no matching <h> tags are found
                    }
                    
                    function findNearestHeadingInChildren(node) {
                        // Traverse the child nodes and recursively search for <h2> or <h3>
                        for (let child of node.children) {
                            if (['H2', 'H3'].includes(child.tagName)) {
                                return child.textContent || '';
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

                if (nearestHTagText != null) {
                    nearestHTagText = nearestHTagText.Replace("\n", "").Replace("\t", "");
                    var aLocators = page.Locator("#side-doc-outline a");
                    var aElements = await aLocators.ElementHandlesAsync();

                    foreach (var elementHandle in aElements)
                    {
                        if(await elementHandle.InnerTextAsync() == nearestHTagText)
                        {
                            var href = await elementHandle.GetAttributeAsync("href");
                            if (href != null){
                                anchorLink = testLink + href;
                            }
                        }
                    }
                }
                if(!anchorLink.Contains("#packages") && !anchorLink.Contains("#modules"))
                {
                    errorList.Add(anchorLink);
                }
            }
        }

        var formattedList = errorList
            .GroupBy(item => item)
            .Select((group, Index) => $"{Index + 1}. Appears {group.Count()} times , location : {group.Key}")
            .ToList();

        if (errorList.Count > 0)
        {
            res.Result = false;
            res.ErrorLink = testLink;
            res.ErrorInfo = "Some cells in the table are missing content";
            res.NumberOfOccurrences = errorList.Count;
            res.LocationsOfErrors = formattedList;
        }

        await browser.CloseAsync();

        return res;
    }
}