using AdaptiveCards;
using AdaptiveCards.Templating;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace AzureSdkQaBot
{
    public class CardBuilder
    {
        private static readonly string _qaCardPath = Path.Combine(".", "Resources", "QACard.json");
        private static readonly string _prCardPath = Path.Combine(".", "Resources", "PRCard.json");
        private static readonly string _prAndQaCardPath = Path.Combine(".", "Resources", "PRAndQACard.json");

        public static async Task<Attachment> NewQAAttachment(string query, string answer, IList<string> _relevancies, CancellationToken cancellationToken)
        {
            bool hasRelevancies = false;
            List<Dictionary<string, string>>? relevancies = _relevancies.Select((relevance) =>
            {
                return new Dictionary<string, string>
                {
                    { "relevance", relevance}
                };
            }).ToList();

            if (relevancies != null && relevancies.Count > 0)
            {
                hasRelevancies = true;
            }
            string cardTemplate = await File.ReadAllTextAsync(_qaCardPath, cancellationToken);
            string cardContent = new AdaptiveCardTemplate(cardTemplate).Expand(
                new
                {
                    query,
                    answer,
                    relevancies,
                    hasRelevancies
                });
            return new Attachment
            {
                Content = JsonConvert.DeserializeObject(cardContent),
                ContentType = AdaptiveCard.ContentType
            };
        }

        public static async Task<Attachment> NewPRAttachment(string query, string answer, string action, IList<string> _references, CancellationToken cancellationToken)
        {
            var references = _references.Select((reference) =>
            {
                return new Dictionary<string, string>
                {
                    { "reference", reference }
                };
            }).ToList();
            string cardTemplate = await File.ReadAllTextAsync(_prCardPath, cancellationToken);
            string cardContent = new AdaptiveCardTemplate(cardTemplate).Expand(
                new
                {
                    query,
                    answer,
                    action,
                    references
                });
            return new Attachment
            {
                Content = JsonConvert.DeserializeObject(cardContent),
                ContentType = AdaptiveCard.ContentType
            };
        }

        public static async Task<Attachment> NewPRAndQAAttachment(string query, string answer, string action, string additionalAnswer, IList<string>? _references, IList<string>? _relevancies, CancellationToken cancellationToken)
        {
            List<Dictionary<string, string>>? references = null;
            List<Dictionary<string, string>>? relevancies = null;
            bool hasRelevancies = false;

            if (_references != null)
            {
                references = _references.Select((reference) =>
                {
                    return new Dictionary<string, string>
                {
                    { "reference", reference }
                };
                }).ToList();
            }

            if (_relevancies != null)
            {
                relevancies = _relevancies.Select((relevance) =>
                {
                    return new Dictionary<string, string>
                {
                    { "relevance", relevance}
                };
                }).ToList();
            }

            if (relevancies != null && relevancies.Count > 0)
            {
                hasRelevancies = true;
            }

            string cardTemplate = await File.ReadAllTextAsync(_prAndQaCardPath, cancellationToken);
            string cardContent = new AdaptiveCardTemplate(cardTemplate).Expand(
                new
                {
                    query,
                    answer,
                    action,
                    references,
                    additionalAnswer,
                    relevancies,
                    hasRelevancies
                });
            return new Attachment
            {
                Content = JsonConvert.DeserializeObject(cardContent),
                ContentType = AdaptiveCard.ContentType
            };
        }

        // Add pull request link inline to make the text clickable in markdown format
        public static string AddPrlinkInMarkdownFormat(string input, string prLink)
        {
            if (!string.IsNullOrEmpty(input) && !string.IsNullOrEmpty(prLink))
            {
                if (input.Contains(prLink))
                {
                    input = AddInlineUrlToText(input);
                }
                else
                {
                    string prText = "Pull Request #";
                    int startIndex = input.IndexOf(prText);
                    if (startIndex > 0)
                    {
                        int endIndex = input.IndexOf(" ", startIndex + prText.Length);
                        prText = input.Substring(startIndex, endIndex - startIndex);
                        input = input.Replace(prText, $"[{prText}]({prLink})");
                    }
                }
            }
            return input;
        }

        // Add the inline url to the text that makes the text clickable
        public static string AddInlineUrlToText(string input)
        {
            if (!string.IsNullOrEmpty(input) && input.Contains("https://"))
            {
                string pattern = @"(https?://\S+[^\s\p{P}])";
                Regex regex = new(pattern);
                Match match = regex.Match(input);
                if (match.Success)
                {
                    string url = match.Groups[1].Value;
                    input = input.Replace(url, $"[{url}]({url})");
                }
            }
            return input;
        }
    }
}
