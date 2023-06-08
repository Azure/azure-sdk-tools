namespace Azure.Sdk.Tools.Assets.MaintenanceTool.Model;

/// <summary>
/// This class abstracts some common query patterns of a set of scanned results.
/// </summary>
public class AssetsResultSet
{

    public AssetsResultSet(List<AssetsResult> input)
    {
        Results = input;
        CalculateObjects();
    }
    public List<AssetsResult> Results { get; set; } = new List<AssetsResult>();

    public Dictionary<string, List<AssetsResult>> ByLanguageRepo { get; private set; } = new();

    public Dictionary<string, List<AssetsResult>> ByTargetTag { get; private set; } = new();

    public Dictionary<string, List<AssetsResult>> ByOriginSHA { get; private set; } = new();

    private void CalculateObjects()
    {
        ByLanguageRepo = new Dictionary<string, List<AssetsResult>>();
        ByTargetTag = new Dictionary<string, List<AssetsResult>>();
        ByOriginSHA = new Dictionary<string, List<AssetsResult>>();

        // sort to ensure that orderings are always the same
        Results = Results.OrderBy(asset => asset.AssetsRepo).ThenBy(asset => asset.Commit).ThenBy(asset => asset.Tag).ToList();

        foreach (var result in Results)
        {
            if (!ByLanguageRepo.ContainsKey(result.LanguageRepo))
            {
                ByLanguageRepo.Add(result.LanguageRepo, new List<AssetsResult>());
            }
            ByLanguageRepo[result.LanguageRepo].Add(result);

            if (!ByTargetTag.ContainsKey(result.Tag))
            {
                ByTargetTag.Add(result.Tag, new List<AssetsResult>());
            }
            ByTargetTag[result.Tag].Add(result);

            if (!ByOriginSHA.ContainsKey(result.Commit))
            {
                ByOriginSHA.Add(result.Commit, new List<AssetsResult>());
            }
            ByOriginSHA[result.Commit].Add(result);
        }
    }
}
