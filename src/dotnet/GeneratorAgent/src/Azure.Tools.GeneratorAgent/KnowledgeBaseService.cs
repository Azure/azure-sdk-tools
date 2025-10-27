using Microsoft.Extensions.Logging;

namespace Azure.Tools.GeneratorAgent;

/// <summary>
/// Service for loading and managing the TypeSpec knowledge base content
/// </summary>
internal class KnowledgeBaseService
{
    private readonly ILogger<KnowledgeBaseService> Logger;
    private readonly string KnowledgeContent;

    public KnowledgeBaseService(ILogger<KnowledgeBaseService> logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        KnowledgeContent = LoadKnowledgeBase();
    }

    /// <summary>
    /// Gets the loaded knowledge base content
    /// </summary>
    /// <returns>The knowledge base content as a string, or empty string if not available</returns>
    public string GetKnowledgeBase() => KnowledgeContent;

    /// <summary>
    /// Indicates whether the knowledge base was successfully loaded
    /// </summary>
    public bool IsKnowledgeBaseAvailable => !string.IsNullOrEmpty(KnowledgeContent);

    /// <summary>
    /// Loads the knowledge base from the knowledge.md file
    /// </summary>
    private string LoadKnowledgeBase()
    {
        try
        {
            var knowledgePath = Path.Combine(AppContext.BaseDirectory, "knowledge.md");

            if (File.Exists(knowledgePath))
            {
                var content = File.ReadAllText(knowledgePath);
                Logger.LogDebug("Successfully loaded knowledge base from: {Path} ({Size} characters)",
                    knowledgePath, content.Length);
                return content;
            }

            Logger.LogWarning("Knowledge base file not found at expected location: {Path}", knowledgePath);
            return string.Empty;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load knowledge base from disk");
            return string.Empty;
        }
    }
}
