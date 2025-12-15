namespace Azure.Sdk.Tools.Cli.Models;

/// <summary>
/// Defines the types of validation checks that can be run on SDK packages.
/// </summary>
public enum PackageCheckType
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
    Dependency,

    /// <summary>
    /// Run README validation check.
    /// </summary>
    Readme,

    /// <summary>
    /// Run cspell.
    /// </summary>
    Cspell,

    /// <summary>
    /// Run snippet update check.
    /// </summary>
    Snippets,

    /// <summary>
    /// Run linting on code.
    /// </summary>
    Linting,

    /// <summary>
    /// Format code
    /// </summary>
    Format,

    /// <summary>
    /// .NET validation for AOT compatibility.
    /// </summary>
    CheckAotCompat,

    /// <summary>
    /// .NET validation for generated code.
    /// </summary>
    GeneratedCodeChecks,

    /// <summary>
    /// Validate samples
    /// </summary>
    Samples
}
