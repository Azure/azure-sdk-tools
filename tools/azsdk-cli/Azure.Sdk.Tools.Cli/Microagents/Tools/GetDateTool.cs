using System.ComponentModel;

namespace Azure.Sdk.Tools.Cli.Microagents.Tools;

// Forward declaration to use types from LanguageChecks.cs
public record CurrentDateResult(string CurrentDate);

public class GetDateTool : AgentTool<ChangelogContents, CurrentDateResult>
{
    public override string Name { get; init; } = "GetCurrentDate";
    public override string Description { get; init; } = "Get the current date in yyyy-MM-dd format";

    public override async Task<CurrentDateResult> Invoke(ChangelogContents input, CancellationToken ct)
    {
        var currentDate = DateTime.Now.ToString("yyyy-MM-dd");
        return new CurrentDateResult(currentDate);
    }
}