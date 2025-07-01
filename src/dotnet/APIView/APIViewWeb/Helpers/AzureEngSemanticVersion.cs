using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

// Ported from https://github.com/Azure/azure-sdk-tools/blob/main/eng/common/scripts/SemVer.ps1
public class AzureEngSemanticVersion : IComparable<AzureEngSemanticVersion>
{
    private static readonly Regex SemVerRegex = new Regex(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?$",
        RegexOptions.Compiled);

    public int Major { get; private set; }
    public int Minor { get; private set; }
    public int Patch { get; private set; }
    public string PrereleaseLabelSeparator { get; private set; } = string.Empty;
    public string PrereleaseLabel { get; private set; } = string.Empty;
    public string PrereleaseNumberSeparator { get; private set; } = string.Empty;
    public string BuildNumberSeparator { get; private set; } = string.Empty;
    public string BuildNumber { get; private set; } = string.Empty;
    public int PrereleaseNumber { get; private set; }
    public bool IsPrerelease { get; private set; }
    public string VersionType { get; private set; } = string.Empty;
    public string RawVersion { get; private set; }
    public bool IsSemVerFormat { get; private set; }
    public string DefaultPrereleaseLabel { get; private set; } = string.Empty;
    public string DefaultAlphaReleaseLabel { get; private set; } = string.Empty;

    public AzureEngSemanticVersion(string version, string language = null)
    {
        RawVersion = version;
        var match = SemVerRegex.Match(version);

        if (match.Success)
        {
            IsSemVerFormat = true;

            Major = int.Parse(match.Groups["major"].Value);
            Minor = int.Parse(match.Groups["minor"].Value);
            Patch = int.Parse(match.Groups["patch"].Value);

            if (language == "Python")
            {
                SetupPythonConventions();
            }
            else
            {
                SetupDefaultConventions();
            }

            if (match.Groups["prelabel"].Success)
            {
                PrereleaseLabel = match.Groups["prelabel"].Value;
                PrereleaseLabelSeparator = match.Groups["presep"].Value;
                PrereleaseNumber = match.Groups["prenumber"].Success ? int.Parse(match.Groups["prenumber"].Value) : 0;
                PrereleaseNumberSeparator = match.Groups["prenumsep"].Value;
                IsPrerelease = true;
                VersionType = "Beta";
                BuildNumberSeparator = match.Groups["buildnumsep"].Value;
                BuildNumber = match.Groups["buildnumber"].Success ? match.Groups["buildnumber"].Value : string.Empty;
            }
            else
            {
                // Artificially provide these values for non-prereleases to enable easy sorting of them later than prereleases.
                PrereleaseLabel = "zzz";
                PrereleaseNumber = 99999999;
                IsPrerelease = false;
                VersionType = "GA";

                if (Major == 0)
                {
                    // Treat initial 0 versions as prerelease beta's
                    VersionType = "Beta";
                    IsPrerelease = true;
                }
                else if (Patch != 0)
                {
                    VersionType = "Patch";
                }
            }
        }
        else
        {
            IsSemVerFormat = false;
        }
    }

    private void SetupPythonConventions()
    {
        PrereleaseLabelSeparator = string.Empty;
        PrereleaseNumberSeparator = string.Empty;
        BuildNumberSeparator = string.Empty;
        DefaultPrereleaseLabel = "b";
        DefaultAlphaReleaseLabel = "a";
    }

    private void SetupDefaultConventions()
    {
        PrereleaseLabelSeparator = "-";
        PrereleaseNumberSeparator = ".";
        BuildNumberSeparator = ".";
        DefaultPrereleaseLabel = "beta";
        DefaultAlphaReleaseLabel = "alpha";
    }

    public override string ToString()
    {
        string versionString = $"{Major}.{Minor}.{Patch}";

        if (!string.IsNullOrEmpty(PrereleaseLabel))
        {
            versionString += $"{PrereleaseLabelSeparator}{PrereleaseLabel}{PrereleaseNumberSeparator}{PrereleaseNumber}";
            if (!string.IsNullOrEmpty(BuildNumber))
            {
                versionString += $"{BuildNumberSeparator}{BuildNumber}";
            }
        }

        return versionString;
    }

    /// <summary>
    /// Compares this version with another version. Implemented for descending order sorting.
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public int CompareTo(AzureEngSemanticVersion other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        int ret = other.Major.CompareTo(this.Major);
        if (ret != 0) return ret;

        ret = other.Minor.CompareTo(this.Minor); 
        if (ret != 0) return ret;

        ret = other.Patch.CompareTo(this.Patch);
        if (ret != 0) return ret;

        string thisPrereleaseLabel = this.PrereleaseLabel ?? "zzz";
        string otherPrereleaseLabel = other.PrereleaseLabel ?? "zzz";
        ret = string.Compare(otherPrereleaseLabel, thisPrereleaseLabel, StringComparison.OrdinalIgnoreCase);
        if (ret != 0) return ret;

        ret = other.PrereleaseNumber.CompareTo(this.PrereleaseNumber);
        if (ret != 0) return ret;

        int thisBuildNumber = string.IsNullOrEmpty(this.BuildNumber) ? 0 : int.Parse(this.BuildNumber);
        int otherBuildNumber = string.IsNullOrEmpty(other.BuildNumber) ? 0 : int.Parse(other.BuildNumber);
        return otherBuildNumber.CompareTo(thisBuildNumber);
    }

    public static List<string> SortVersionStrings(IEnumerable<string> versionStrings)
    {
        var versions = versionStrings
            .Select(v => new AzureEngSemanticVersion(v))
            .Where(v => v != null)
            .ToList();
        versions.Sort();
        return versions.Select(v => v.RawVersion).ToList();
    }
}
