using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AzureSDKDevToolsMCP.Helpers;
using AzureSDKDSpecTools.Models;
using Microsoft.Extensions.Logging;

namespace AzureSDKDSpecTools.Helpers
{
    public interface ISpecPullRequestHelper
    {
        public List<ApiviewData> FindApiReviewLinks(List<string> comments);        
    }
    public class SpecPullRequestHelper(ILogger<SpecPullRequestHelper> logger, IGitHelper gitHelper) : ISpecPullRequestHelper
    {
        private ILogger<SpecPullRequestHelper> _logger = logger;
        private IGitHelper _gitHelper = gitHelper;
        private readonly string apiReviewRegex = "\\|\\s([\\w]+)\\s\\|\\s\\[(.+)\\]\\((.+)\\)";

        public List<ApiviewData> FindApiReviewLinks(List<string> comments)
        {
            try
            {
                var apiviewComments = comments.Where(c => c.Contains("## API Change Check") || c.Contains("APIView"));
                if (apiviewComments == null || !apiviewComments.Any())
                {
                    _logger.LogWarning("No API reviews found in the comments");
                    return [];
                }

                List<ApiviewData> apiviewLinks = [];
                foreach (var comment in apiviewComments)
                {
                    var regex = new Regex(apiReviewRegex);
                    var matches = regex.Matches(comment);
                    if (matches == null)
                    {
                        _logger.LogInformation("No matching found");
                        continue;
                    }

                    foreach (var m in matches)
                    {
                        if (m is Match match)
                        {
                            _logger.LogInformation($"API view match {match.Value}");
                            if(match.Groups.Count == 4)
                            {
                                apiviewLinks.Add(
                                    new ApiviewData()
                                    {
                                        Language = match.Groups[1].Value,
                                        PackageName = match.Groups[2].Value,
                                        ApiviewLink = match.Groups[3].Value
                                    });
                            }                            
                        }
                    }
                }
                return apiviewLinks;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get API review links from comments, Error: {ex.Message}");
                return [];
            }            
        }
    }
}
