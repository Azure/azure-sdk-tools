// tslint:disable:max-line-length
/**
 * @description
 *   This function has been copied over and adapted to TypeScript from:
 *   https://github.com/Azure/azure-sdk-tools/blob/efa8a15c81e4614f2071b82dd8ca4f6ce6076f7b/eng/common/scripts/SemVer.ps1#L1
 *   Specifically, the function body was adapted from 'AzureEngSemanticVersion([string] $versionString)'.
 *
 *   This function parses a semver version string into its components and supports operations around it that we use for
 *   versioning our packages.
 *
 * @param versionString
 *   The semver version string to parse.
 *
 * @param language
 *   The language of the package to which the versionString pertains.
 *   This is used to determine the conventions of the versionString.
 *
 * @returns
 *   A ParseVersion object that contains the parsed components of the versionString.
 *   If the versionString or language is undefined, the function returns undefined.
 */
export function parseSemverVersionString(
  versionString: string | undefined,
  language: string | undefined
): ParseVersion | undefined {
  if (!versionString || !language) {
    return undefined;
  }
  // Copied from https://github.com/Azure/azure-sdk-tools/blob/efa8a15c81e4614f2071b82dd8ca4f6ce6076f7b/eng/common/scripts/SemVer.ps1#L36
  const SEMVER_REGEX =
   /(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?/im;
  let prereleaseLabelSeparator: string | undefined;
  let prereleaseNumberSeparator: string | undefined;
  let buildNumberSeparator: string | undefined;
  let defaultPrereleaseLabel: string | undefined;
  let defaultAlphaReleaseLabel: string | undefined;
  let major: string | undefined;
  let minor: string | undefined;
  let patch: string | undefined;
  let prereleaseLabel: string | undefined;
  let prereleaseNumber: string | undefined;
  let isPrerelease: boolean | undefined;
  let versionType: string | undefined;
  let buildNumber: string | undefined;
  let prelabel: string | undefined;
  let isSemVerFormat: boolean | undefined;
  let rawVersion: string | undefined;
  const matches = versionString.match(SEMVER_REGEX);
  if (matches) {
    isSemVerFormat = true;
    rawVersion = versionString;
    major = matches && matches.groups && matches.groups.major;
    minor = matches && matches.groups && matches.groups.minor;
    patch = matches && matches.groups && matches.groups.patch;
    // If Language exists and is set to python setup the python conventions.
    const parseLanguage = 'python';
    if (parseLanguage === language.toLowerCase()) {
      // Python uses no separators and 'b' for beta so this sets up the the object to work with those conventions
      prereleaseLabelSeparator = prereleaseNumberSeparator = buildNumberSeparator = '';
      defaultPrereleaseLabel = 'b';
      defaultAlphaReleaseLabel = 'a';
    } else {
      // Use the default common conventions
      prereleaseLabelSeparator = '-';
      prereleaseNumberSeparator = '.';
      buildNumberSeparator = '.';
      defaultPrereleaseLabel = 'beta';
      defaultAlphaReleaseLabel = 'alpha';
    }

    prelabel = matches && matches.groups && matches.groups.prelabel;
    if (!prelabel) {
      prereleaseLabel = 'zzz';
      prereleaseNumber = '99999999';
      isPrerelease = false;
      versionType = 'GA';
      if (major?.toString() === '0') {
        // Treat initial 0 versions as a prerelease beta's
        versionType = 'Beta';
        isPrerelease = true;
      } else if (patch?.toString() !== '0') {
        versionType = 'patch';
      }
    } else {
      prereleaseLabel = matches && matches.groups && matches.groups.prelabel;
      prereleaseLabelSeparator = matches && matches.groups && matches.groups.presep;
      prereleaseNumber = matches && matches.groups && matches.groups.prenumber;
      prereleaseNumberSeparator = matches && matches.groups && matches.groups.prenumsep;
      isPrerelease = true;
      versionType = 'Beta';
      buildNumberSeparator = matches && matches.groups && matches.groups.buildnumsep;
      buildNumber = matches && matches.groups && matches.groups.buildnumber;
    }
  } else {
    rawVersion = versionString;
    isSemVerFormat = false;
  }
  return {
    prelabel,
    major,
    minor,
    patch,
    prereleaseLabelSeparator,
    prereleaseLabel,
    prereleaseNumberSeparator,
    buildNumberSeparator,
    buildNumber,
    prereleaseNumber,
    isPrerelease,
    versionType,
    rawVersion,
    isSemVerFormat,
    defaultPrereleaseLabel,
    defaultAlphaReleaseLabel
  };
}

type ParseVersion = {
  prereleaseLabelSeparator: string | undefined;
  prereleaseNumberSeparator: string | undefined;
  buildNumberSeparator: string | undefined;
  defaultPrereleaseLabel: string | undefined;
  defaultAlphaReleaseLabel: string | undefined;
  major: string | undefined;
  minor: string | undefined;
  patch: string | undefined;
  prereleaseLabel: string | undefined;
  prereleaseNumber: string | undefined;
  isPrerelease: boolean | undefined;
  versionType: string | undefined;
  buildNumber: string | undefined;
  prelabel: string | undefined;
  isSemVerFormat: boolean | undefined;
  rawVersion: string | undefined;
};
