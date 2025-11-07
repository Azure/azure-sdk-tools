using Microsoft.Playwright;
public static class PlaywrightHelper
{
    public static async Task<IPage> GotoageWithRetriesAsync(IPage page, string url, int retryCount = 3)
    {
        // Increase timeout for CI/CD environments
        var timeout = 60000;
        var waitBetweenRetries = 2000; // 2 seconds between retries
        
        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                await page.GotoAsync(url, new PageGotoOptions 
                { 
                    WaitUntil = WaitUntilState.NetworkIdle, // Wait for network to be idle instead of just load
                    Timeout = timeout 
                });

                return page;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to navigate to {url} on attempt {i + 1}/{retryCount}. Error: {ex.Message}");
                
                if (i == retryCount - 1)
                {
                    Console.WriteLine($"All {retryCount} attempts failed for {url}");
                    throw;
                }
                
                // Wait before retry
                Console.WriteLine($"Waiting {waitBetweenRetries}ms before next attempt...");
                await Task.Delay(waitBetweenRetries);
            }
        }
        throw new InvalidOperationException("This code should not be executed.");
    }
}
