using System;
using System.Collections.Generic;
using APIViewWeb.Models;

namespace APIViewWeb.Managers;

public interface ILanguageExpansionRule
{
    /// <summary>
    /// Expands one metadata record into one or more (languageKey, PackageInfo) pairs
    /// to store in ExpectedPackages. The languageKey is a PackageKey.ToString() string,
    /// </summary>
    IEnumerable<(string languageKey, PackageInfo pkg)> Expand(
        string normalizedLanguage, LanguageConfig config);

    /// <summary>
    /// Returns the composite key under which this review should be stored in
    /// project.Reviews and looked up in project.ExpectedPackages.
    /// </summary>
    string GetLanguageKey(string normalizedLanguage, string packageName);
}

/// <summary>
/// Handles all Java packages. When metadata carries Flavor="azurev2", a single record expands
/// into two ExpectedPackages entries:
///   "Java:v2" → the v2 package as-is  (e.g. com.azure.v2:azure-core-test / com.azure.v2.security.keys)
///   "Java"    → base package with v2 markers stripped (e.g. com.azure:azure-core-test / com.azure.security.keys)
/// For regular Java (no azurev2 flavor) a single "Java" entry is produced.
/// For review linking: a Java review whose package name contains ".v2:" maps to "Java:v2";
/// all other Java reviews map to "Java".
/// </summary>
public class JavaExpansionRule : ILanguageExpansionRule
{
    public static JavaExpansionRule Instance { get; } = new();

    public IEnumerable<(string, PackageInfo)> Expand(string normalizedLanguage, LanguageConfig config)
    {
        bool isV2 = string.Equals(config.Flavor, "azurev2", StringComparison.OrdinalIgnoreCase);
        if (isV2)
        {
            // Two entries: the v2 package as-is, plus the derived base package.
            yield return ("Java:v2", new PackageInfo
            {
                PackageName = config.PackageName,
                Namespace = config.Namespace
            });
            yield return ("Java", new PackageInfo
            {
                PackageName = StripV2PackageSuffix(config.PackageName),
                Namespace = StripV2NamespaceSegment(config.Namespace)
            });
        }
        else
        {
            yield return ("Java", new PackageInfo
            {
                PackageName = config.PackageName,
                Namespace = config.Namespace
            });
        }
    }

    public string GetLanguageKey(string normalizedLanguage, string packageName) =>
        packageName?.Contains(".v2:", StringComparison.OrdinalIgnoreCase) == true
            ? "Java:v2"
            : "Java";

    /// "com.azure.v2:azure-core-test" → "com.azure:azure-core-test"
    private static string StripV2PackageSuffix(string name)
        => string.IsNullOrEmpty(name)
            ? name
            : name.Replace(".v2:", ":", StringComparison.OrdinalIgnoreCase);

    /// "com.azure.v2.security.keyvault.keys" → "com.azure.security.keyvault.keys"
    private static string StripV2NamespaceSegment(string ns)
        => string.IsNullOrEmpty(ns)
            ? ns
            : ns.Replace(".v2.", ".", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Used for all languages that do not have a dedicated expansion rule.
/// One metadata record → one ExpectedPackages entry keyed by the plain language name.
/// Flavor is ignored — language-specific rules handle any flavor distinctions.
/// </summary>
public class DefaultExpansionRule : ILanguageExpansionRule
{
    public static DefaultExpansionRule Instance { get; } = new();

    public IEnumerable<(string, PackageInfo)> Expand(string normalizedLanguage, LanguageConfig config)
    {
        yield return (normalizedLanguage, new PackageInfo
        {
            PackageName = config.PackageName,
            Namespace = config.Namespace
        });
    }

    public string GetLanguageKey(string normalizedLanguage, string packageName) => normalizedLanguage;
}

