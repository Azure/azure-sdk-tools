// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using Xunit;

namespace APIViewUnitTests;

public class VersionNormalizationTests
{
    #region Stable releases

    [Theory]
    [InlineData("1.0.0",   "1.0.0",   VersionKind.Stable)]
    [InlineData("v1.0.0",  "1.0.0",   VersionKind.Stable)]  // strip leading 'v'
    [InlineData("V2.3.4",  "2.3.4",   VersionKind.Stable)]  // strip leading 'V'
    [InlineData("1.2.3",   "1.2.3",   VersionKind.Stable)]
    [InlineData("10.0.0",  "10.0.0",  VersionKind.Stable)]
    public void NormalizeVersion_Stable(string input, string expectedId, VersionKind expectedKind)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedKind, kind);
    }

    #endregion

    #region Sub-1.0.0 versions (Preview)

    // Versions with Major == 0 are treated as pre-stable regardless of whether
    // they carry an explicit prerelease suffix (e.g. 0.5.0) or not (0.5.0-beta.1).
    [Theory]
    [InlineData("0.1.0",        "0.1.0",        VersionKind.Preview)]  // no suffix
    [InlineData("0.0.1",        "0.0.1",        VersionKind.Preview)]  // no suffix
    [InlineData("0.5.0-beta.1", "0.5.0-beta.1", VersionKind.Preview)]  // explicit prerelease label
    [InlineData("0.9.1",        "0.9.1",        VersionKind.Preview)]  // no suffix, non-trivial
    public void NormalizeVersion_SubOneZeroZero_IsPreview(string input, string expectedId, VersionKind expectedKind)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedKind, kind);
    }

    #endregion

    #region Milestone prereleases (Preview)

    [Theory]
    [InlineData("1.0.0-beta.1",  "1.0.0-beta.1",  VersionKind.Preview)]
    [InlineData("1.0.0-beta.2",  "1.0.0-beta.2",  VersionKind.Preview)]
    [InlineData("2.0.0-rc.2",    "2.0.0-rc.2",    VersionKind.Preview)]
    public void NormalizeVersion_MilestonePrerelease_IsPreview(string input, string expectedId, VersionKind expectedKind)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedKind, kind);
    }

    // Label-only prereleases (no numeric milestone suffix): the number must NOT be appended.
    [Theory]
    [InlineData("1.2.0-alpha", "1.2.0-alpha", VersionKind.Preview)]
    [InlineData("1.0.0-beta",  "1.0.0-beta",  VersionKind.Preview)]
    [InlineData("2.0.0-rc",    "2.0.0-rc",    VersionKind.Preview)]
    public void NormalizeVersion_LabelOnlyPrerelease_NoNumericSuffix(string input, string expectedId, VersionKind expectedKind)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedKind, kind);
    }

    // Python PEP 440 milestone: prelabel="b"/"a", PrereleaseNumber is a small sequential integer.
    [Theory]
    [InlineData("1.0.0b1", "1.0.0b1", VersionKind.Preview)]
    [InlineData("1.0.0b2", "1.0.0b2", VersionKind.Preview)]
    public void NormalizeVersion_PythonMilestonePrerelease_IsPreview(string input, string expectedId, VersionKind expectedKind)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input, language: "Python");
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedKind, kind);
    }

    [Fact]
    public void NormalizeVersion_UppercaseLabel_IsLowercased()
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion("1.0.0-BETA.1");
        Assert.Equal("1.0.0-beta.1", id);
        Assert.Equal(VersionKind.Preview, kind);
    }

    #endregion

    #region Rolling prereleases (RollingPrerelease)

    // C# / Java / JS: dot-separated YYYYMMDD date stamp in the prerelease number.
    [Theory]
    [InlineData("1.2.0-alpha.20260323.1", "1.2.0-alpha", VersionKind.RollingPrerelease)]
    [InlineData("1.2.0-alpha.20260324.1", "1.2.0-alpha", VersionKind.RollingPrerelease)]  // same channel, next day
    [InlineData("2.0.0-alpha.20201221.5", "2.0.0-alpha", VersionKind.RollingPrerelease)]
    [InlineData("2.0.0-alpha.20201221.6", "2.0.0-alpha", VersionKind.RollingPrerelease)]  // same channel, next build
    [InlineData("2.0.0-alpha.20200920",   "2.0.0-alpha", VersionKind.RollingPrerelease)]  // no build counter
    [InlineData("1.0.0-beta.20230101.1",  "1.0.0-beta",  VersionKind.RollingPrerelease)]
    [InlineData("1.2.0-dev.20260323",     "1.2.0-dev",   VersionKind.RollingPrerelease)]
    public void NormalizeVersion_DateStampedPrerelease_NormalizesToChannel(
        string input, string expectedId, VersionKind expectedKind)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(expectedKind, kind);
    }

    [Fact]
    public void NormalizeVersion_DateStamped_UppercaseLabelIsLowercased()
    {
        // Upper-case label is lowercased; date stamp is stripped → channel identifier only.
        var (id, _) = VersionNormalizationHelper.NormalizeVersion("2.0.0-Alpha.20201221.5");
        Assert.Equal("2.0.0-alpha", id);
    }

    // Python PEP 440: date stamp encoded as the prerelease number without dot separator
    // (e.g. 1.2.0a20260323001 → prelabel="a", prenumber=20260323, buildnumber="001").
    [Fact]
    public void NormalizeVersion_PythonDailyBuild_NormalizesToChannel()
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion("1.2.0a20260323001", language: "Python");
        Assert.Equal("1.2.0a", id);
        Assert.Equal(VersionKind.RollingPrerelease, kind);
    }

    [Fact]
    public void NormalizeVersion_NonPythonDateStampWithoutDotSeparator_IsRolling()
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion("1.0.0b20230101001");
        Assert.Equal("1.0.0b", id);
        Assert.Equal(VersionKind.RollingPrerelease, kind);
    }

    #endregion

    #region Non-SemVer / unparseable fallback (Preview)

    // Non-semver strings that are not bare positive integers fall back to Preview.
    // PEP 440 .dev/.post qualifiers, four-part dotted versions, plain text all land here.
    [Theory]
    [InlineData("1.0.0.dev0",    "1.0.0.dev0")]    // PEP 440 dev release (Python-specific)
    [InlineData("1.0.0.post1",   "1.0.0.post1")]   // PEP 440 post release (Python-specific)
    [InlineData("1.0.0.0",       "1.0.0.0")]       // four-part version
    [InlineData("some-snapshot", "some-snapshot")]  // arbitrary string
    public void NormalizeVersion_NonSemVer_FallsBackToPreview(string input, string expectedId)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(VersionKind.Preview, kind);
    }

    #endregion

    #region Bare-integer versions (Stable — e.g. Azure Key Vault "6", "7")

    // Some services version their API surface with a plain integer (e.g. Key Vault secrets: 6, 7).
    // These are GA stable releases and must NOT be classified as Preview.
    [Theory]
    [InlineData("6",  "6")]
    [InlineData("7",  "7")]
    [InlineData("10", "10")]
    public void NormalizeVersion_BarePositiveInteger_IsStable(string input, string expectedId)
    {
        var (id, kind) = VersionNormalizationHelper.NormalizeVersion(input);
        Assert.Equal(expectedId, id);
        Assert.Equal(VersionKind.Stable, kind);
    }

    #endregion

    #region Edge cases

    [Fact]
    public void NormalizeVersion_NullOrEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => VersionNormalizationHelper.NormalizeVersion(null));
        Assert.Throws<ArgumentException>(() => VersionNormalizationHelper.NormalizeVersion(""));
        Assert.Throws<ArgumentException>(() => VersionNormalizationHelper.NormalizeVersion("   "));
    }

    [Fact]
    public void NormalizeVersion_VPrefixStripped_ProducesSameIdentifier()
    {
        var (id1, _) = VersionNormalizationHelper.NormalizeVersion("1.2.3");
        var (id2, _) = VersionNormalizationHelper.NormalizeVersion("v1.2.3");
        Assert.Equal(id1, id2);
    }

    #endregion
}
