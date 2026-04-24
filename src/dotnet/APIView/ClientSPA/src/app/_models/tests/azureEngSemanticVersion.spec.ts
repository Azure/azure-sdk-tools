import { AzureEngSemanticVersion } from "../azureEngSemanticVersion";

describe('AzureEngSemanticVersion', () => {
  it('should sort versions correctly', () => {
    const versions = [
      { version: "1.0.1", language: "C#" },
      { version: "2.0.0", language: "C#" },
      { version: "2.0.0-alpha.20200920", language: "C#" },
      { version: "2.0.0-beta.2", language: "C#" },
      { version: "2.0.0-beta.1", language: "C#" },
      { version: "1.0.0", language: "C#" },
      { version: "1.0.0b2", language: "Python" },
    ];
    const expectedSort = [
      { version: "2.0.0", language: "C#" },
      { version: "2.0.0-beta.2", language: "C#" },
      { version: "2.0.0-beta.1", language: "C#" },
      { version: "2.0.0-alpha.20200920", language: "C#" },
      { version: "1.0.1", language: "C#" },
      { version: "1.0.0", language: "C#" },
      { version: "1.0.0b2", language: "Python" },
    ];

    versions.sort((a, b) => {
      const aVer = new AzureEngSemanticVersion(a.version, a.language);
      const bVer = new AzureEngSemanticVersion(b.version, b.language);
      return bVer.compareTo(aVer);
    });

    expect(versions).toEqual(expectedSort);
  });
});

describe('AzureEngSemanticVersion - Python Post-release', () => {
  const postReleaseTestCases = [
    // GA post-release
    { input: "1.0.0.post1", isPostRelease: true, postNum: 1, isPrerelease: false, versionType: "GA" },
    // Prerelease post-release
    { input: "1.0.0b2.post1", isPostRelease: true, postNum: 1, isPrerelease: true, versionType: "Beta", prereleaseLabel: "b", prereleaseNumber: 2 },
    // Implicit post0
    { input: "1.0.0.post", isPostRelease: true, postNum: 0, isPrerelease: false, versionType: "GA" },
    // Alternate separators (hyphen, underscore, none)
    { input: "1.0.0-post1", isPostRelease: true, postNum: 1 },
    { input: "1.0.0_post1", isPostRelease: true, postNum: 1 },
    { input: "1.0.0post1", isPostRelease: true, postNum: 1 },
    // Case insensitive
    { input: "1.0.0.POST1", isPostRelease: true, postNum: 1 },
  ];

  postReleaseTestCases.forEach(({ input, isPostRelease, postNum, isPrerelease, versionType, prereleaseLabel, prereleaseNumber }) => {
    it(`should parse "${input}" correctly`, () => {
      const ver = new AzureEngSemanticVersion(input, "Python");
      expect(ver.isSemVerFormat).toBe(true);
      expect(ver.isPostRelease).toBe(isPostRelease);
      expect(ver.postReleaseNumber).toBe(postNum);
      if (isPrerelease !== undefined) expect(ver.isPrerelease).toBe(isPrerelease);
      if (versionType !== undefined) expect(ver.versionType).toBe(versionType);
      if (prereleaseLabel !== undefined) expect(ver.prereleaseLabel).toBe(prereleaseLabel);
      if (prereleaseNumber !== undefined) expect(ver.prereleaseNumber).toBe(prereleaseNumber);
    });
  });

  it('should not detect post-release for non-Python languages', () => {
    const ver = new AzureEngSemanticVersion("1.0.0-post.1", "C#");
    expect(ver.isSemVerFormat).toBe(true);
    expect(ver.isPostRelease).toBe(false);
    expect(ver.prereleaseLabel).toBe("post");
  });

  it('should sort post-releases correctly', () => {
    const versions = [
      { version: "2.0.0", language: "Python" },
      { version: "1.0.0.post1", language: "Python" },
      { version: "2.0.0b1", language: "Python" },
      { version: "1.0.0", language: "Python" },
      { version: "2.0.0b1.post1", language: "Python" },
      { version: "2.0.0.post1", language: "Python" },
    ];
    const expectedSort = [
      { version: "2.0.0.post1", language: "Python" },
      { version: "2.0.0", language: "Python" },
      { version: "2.0.0b1.post1", language: "Python" },
      { version: "2.0.0b1", language: "Python" },
      { version: "1.0.0.post1", language: "Python" },
      { version: "1.0.0", language: "Python" },
    ];

    versions.sort((a, b) => {
      const aVer = new AzureEngSemanticVersion(a.version, a.language);
      const bVer = new AzureEngSemanticVersion(b.version, b.language);
      return bVer.compareTo(aVer);
    });

    expect(versions).toEqual(expectedSort);
  });
});