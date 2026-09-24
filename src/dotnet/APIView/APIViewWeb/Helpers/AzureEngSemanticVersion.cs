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

    private static readonly Regex PythonSemVerRegex = new Regex(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?(?:(?<postsep>[.\-_]?)(?<postword>[Pp][Oo][Ss][Tt])\.?(?<postnum>\d{1,8})?)?$",
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
    public bool IsDailyDevBuild { get; private set; }
    public bool HasPrereleaseLabel { get; private set; }
    public bool HasPrereleaseNumber { get; private set; }
    public string VersionType { get; private set; } = string.Empty;
    public string RawVersion { get; private set; }
    public bool IsSemVerFormat { get; private set; }
    public string DefaultPrereleaseLabel { get; private set; } = string.Empty;
    public string DefaultAlphaReleaseLabel { get; private set; } = string.Empty;
    // For Python PEP440 post-release support only
    public bool IsPostRelease { get; private set; }
    public int PostReleaseNumber { get; private set; }
    public string PostReleaseSeparator { get; private set; } = string.Empty;

    public AzureEngSemanticVersion(string version, string language = null)
    {
        RawVersion = version;
        bool isPython = string.Equals(language, "Python", StringComparison.OrdinalIgnoreCase);
        var regex = isPython ? SetupPythonConventions() : SetupDefaultConventions();
        var match = regex.Match(version);

        if (match.Success)
        {
            IsSemVerFormat = true;

            Major = int.Parse(match.Groups["major"].Value);
            Minor = int.Parse(match.Groups["minor"].Value);
            Patch = int.Parse(match.Groups["patch"].Value);

            bool skipPrelabel = false;
            if (isPython)
            {
                if (match.Groups["postword"].Success)
                {
                    IsPostRelease = true;
                    PostReleaseNumber = match.Groups["postnum"].Success ? int.Parse(match.Groups["postnum"].Value) : 0;
                    PostReleaseSeparator = ".post";
                }
                else if (match.Groups["prelabel"].Success && 
                         string.Equals(match.Groups["prelabel"].Value, "post", StringComparison.OrdinalIgnoreCase))
                {
                    // Alternate PEP 440 forms like "1.0.0-post1" or "1.0.0post1" where the regex
                    // matched "post" as a prerelease label — reinterpret as post-release.
                    IsPostRelease = true;
                    PostReleaseNumber = match.Groups["prenumber"].Success ? int.Parse(match.Groups["prenumber"].Value) : 0;
                    PostReleaseSeparator = ".post";
                    skipPrelabel = true;
                }
            }

            if (!skipPrelabel && match.Groups["prelabel"].Success)
            {
                PrereleaseLabel = match.Groups["prelabel"].Value;
                PrereleaseLabelSeparator = match.Groups["presep"].Value;
                PrereleaseNumber = match.Groups["prenumber"].Success ? int.Parse(match.Groups["prenumber"].Value) : 0;
                PrereleaseNumberSeparator = match.Groups["prenumsep"].Value;
                HasPrereleaseNumber = match.Groups["prenumber"].Success;
                IsPrerelease = true;
                HasPrereleaseLabel = true;
                VersionType = "Beta";
                BuildNumberSeparator = match.Groups["buildnumsep"].Value;
                BuildNumber = match.Groups["buildnumber"].Success ? match.Groups["buildnumber"].Value : string.Empty;
                // CI daily builds encode a YYYYMMDD date as the prerelease number;
                IsDailyDevBuild = PrereleaseNumber >= 20000101;
            }
            else if (skipPrelabel || !match.Groups["prelabel"].Success)
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
            else
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
        }
        else
        {
            IsSemVerFormat = false;
        }
    }

    private Regex SetupPythonConventions()
    {
        PrereleaseLabelSeparator = string.Empty;
        PrereleaseNumberSeparator = string.Empty;
        BuildNumberSeparator = string.Empty;
        DefaultPrereleaseLabel = "b";
        DefaultAlphaReleaseLabel = "a";
        return PythonSemVerRegex;
    }

    private Regex SetupDefaultConventions()
    {
        PrereleaseLabelSeparator = "-";
        PrereleaseNumberSeparator = ".";
        BuildNumberSeparator = ".";
        DefaultPrereleaseLabel = "beta";
        DefaultAlphaReleaseLabel = "alpha";
        return SemVerRegex;
    }

    public override string ToString()
    {
        string versionString = $"{Major}.{Minor}.{Patch}";

        // Only output prerelease label if it's a real one (not the artificial "zzz" used for sorting)
        if (!string.IsNullOrEmpty(PrereleaseLabel) && PrereleaseLabel != "zzz")
        {
            versionString += $"{PrereleaseLabelSeparator}{PrereleaseLabel}{PrereleaseNumberSeparator}{PrereleaseNumber}";
            if (!string.IsNullOrEmpty(BuildNumber))
            {
                versionString += $"{BuildNumberSeparator}{BuildNumber}";
            }
        }

        if (IsPostRelease)
        {
            versionString += $"{PostReleaseSeparator}{PostReleaseNumber}";
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
        ret = otherBuildNumber.CompareTo(thisBuildNumber);
        if (ret != 0) return ret;

        // Post-release versions sort after their base version
        int thisPost = this.IsPostRelease ? 1 : 0;
        int otherPost = other.IsPostRelease ? 1 : 0;
        ret = otherPost.CompareTo(thisPost);
        if (ret != 0) return ret;

        return other.PostReleaseNumber.CompareTo(this.PostReleaseNumber);
    }

    public static List<string> SortVersionStrings(IEnumerable<string> versionStrings)
    {
        var versions = versionStrings
            .Select(v => new AzureEngSemanticVersion(v))
            .Where(v => v.IsSemVerFormat)
            .ToList();
        versions.Sort();
        return versions.Select(v => v.RawVersion).ToList();
    }

    public static List<string> SortVersionStrings(IEnumerable<string> versionStrings, string language)
    {
        var versions = versionStrings
            .Select(v => new AzureEngSemanticVersion(v, language))
            .Where(v => v.IsSemVerFormat)
            .ToList();
        versions.Sort();
        return versions.Select(v => v.RawVersion).ToList();
    }
}
