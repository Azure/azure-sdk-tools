import { AzureEngSemanticVersion } from "../azureEngSemanticVersion";

describe('AzureEngSemanticVersion', () => {
  const version = [
    { version: "1.0.1", langugae: "C#" },
    { version: "2.0.0", langugae: "C#" },
    { version: "2.0.0-alpha.20200920", langugae: "C#" },
    { version: "2.0.0-alpha.20200920.1", langugae: "C#" },
    { version: "2.0.0-beta.2", langugae: "C#" },
    { version: "1.0.10", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.03", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.1", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.5", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.2", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.10", langugae: "C#" },
    { version: "2.0.0-beta.1", langugae: "C#" },
    { version: "2.0.0-beta.10", langugae: "C#" },
    { version: "1.0.0", langugae: "C#" },
    { version: "1.0.0b2", langugae: "Python" },
    { version: "1.0.2", langugae: "C#" }, 
  ];

  const expectedSort = [
    { version: "2.0.0", langugae: "C#" },
    { version: "2.0.0-beta.10", langugae: "C#" },
    { version: "2.0.0-beta.2", langugae: "C#" },
    { version: "2.0.0-beta.1", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.10", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.5", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.03", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.2", langugae: "C#" },
    { version: "2.0.0-alpha.20201221.1", langugae: "C#" },
    { version: "2.0.0-alpha.20200920.1", langugae: "C#" },
    { version: "2.0.0-alpha.20200920", langugae: "C#" },
    { version: "1.0.10", langugae: "C#" },
    { version: "1.0.2", langugae: "C#" },
    { version: "1.0.1", langugae: "C#" },
    { version: "1.0.0", langugae: "C#" },
    { version: "1.0.0b2", langugae: "Python" },
  ];

  it('should sort the versions correctly', () => {
    version.sort((a: any, b: any) => {
      const aVersion = new AzureEngSemanticVersion(a.version, a.langugae);
      const bVersion = new AzureEngSemanticVersion(b.version, b.langugae);
      return bVersion.compareTo(aVersion);
    });

    expect(version).toEqual(expectedSort);
  });
});