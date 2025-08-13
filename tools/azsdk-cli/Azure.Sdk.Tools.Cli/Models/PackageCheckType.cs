namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Defines the types of validation checks that can be run on SDK packages.
/// </summary>
public enum PackageCheckName
{
    /// <summary>
    /// Run all available validation checks.
    /// </summary>
    All,
    
    /// <summary>
    /// Run changelog validation check.
    /// </summary>
    Changelog,
    
    /// <summary>
    /// Run dependency analysis check.
    /// </summary>
    Dependency
}
