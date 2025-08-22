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

  // Regex inspired but simplified from https://semver.org/#is-there-a-suggested-regular-expression-regex-to-check-a-semver-string
  // Validation: https://regex101.com/r/vkijKf/426
  const SEMVER_REGEX =
   /(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:(?<presep>-?)(?<prelabel>[a-zA-Z]+)(?:(?<prenumsep>\.?)(?<prenumber>[0-9]{1,8})(?:(?<buildnumsep>\.?)(?<buildnumber>\d{1,3}))?)?)?/im;
  
  let major: string | undefined;
  let minor: string | undefined;
  let patch: string | undefined;

  let prereleaseLabelSeparator: string | undefined;
  let prereleaseLabel: string | undefined;
  let prereleaseNumberSeparator: string | undefined;
  let buildNumberSeparator: string | undefined;

  let buildNumber: string | undefined;
  let prereleaseNumber: string | undefined;
  let isPrerelease: boolean | undefined;
  let versionType: string | undefined;
  let rawVersion: string | undefined;
  let isSemVerFormat: boolean | undefined;

  let defaultPrereleaseLabel: string | undefined;
  let defaultAlphaReleaseLabel: string | undefined;

  const matches = versionString.match(SEMVER_REGEX);
  if (matches) {
    isSemVerFormat = true;
    rawVersion = versionString;
    major = matches && matches.groups && matches.groups.major;
    minor = matches && matches.groups && matches.groups.minor;
    patch = matches && matches.groups && matches.groups.patch;
    
    // If Language exists and is set to python setup the python conventions.
    const conventions = language.toLowerCase() === 'python'
      ? SetupPythonConventions()
      : SetupDefaultConventions();
    prereleaseLabelSeparator = conventions.prereleaseLabelSeparator;
    prereleaseNumberSeparator = conventions.prereleaseNumberSeparator;
    buildNumberSeparator = conventions.buildNumberSeparator;
    defaultPrereleaseLabel = conventions.defaultPrereleaseLabel;
    defaultAlphaReleaseLabel = conventions.defaultAlphaReleaseLabel;

    if (!(matches && matches.groups && matches.groups.prelabel)) {
      isPrerelease = false;
      versionType = 'GA';
      if (major?.toString() === '0') {
        // Treat initial 0 versions as a prerelease beta's
        versionType = 'Beta';
        isPrerelease = true;
      } else if (patch?.toString() !== '0') {
        versionType = 'Patch';
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

// Python uses no separators and "b" for beta so this sets up the the object to work with those conventions
function SetupPythonConventions() {
  return {
    prereleaseLabelSeparator: '',
    prereleaseNumberSeparator: '',
    buildNumberSeparator: '',
    defaultPrereleaseLabel: 'b',
    defaultAlphaReleaseLabel: 'a'
  };
}

// Use the default common conventions
function SetupDefaultConventions() {
  return {
    prereleaseLabelSeparator: '-',
    prereleaseNumberSeparator: '.',
    buildNumberSeparator: '.',
    defaultPrereleaseLabel: 'beta',
    defaultAlphaReleaseLabel: 'alpha'
  };
}

type ParseVersion = {
  major: string | undefined;
  minor: string | undefined;
  patch: string | undefined;
  prereleaseLabelSeparator: string | undefined;
  prereleaseLabel: string | undefined;
  prereleaseNumberSeparator: string | undefined;
  buildNumberSeparator: string | undefined;
  buildNumber: string | undefined;
  prereleaseNumber: string | undefined;
  isPrerelease: boolean | undefined;
  versionType: string | undefined;
  rawVersion: string | undefined;
  isSemVerFormat: boolean | undefined;
  defaultPrereleaseLabel: string | undefined;
  defaultAlphaReleaseLabel: string | undefined;
};
