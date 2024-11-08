import { LanguageConfiguration } from '../languageConfiguration';
import { getPackageName, createNugetPackage, findPackage, getInstallationInstructions } from './dotnet';

/**
 * A language configuration for C#.
 */
export const dotnetTrack2: LanguageConfiguration = {
  name: '.NET Track2',
  generatorPackageName: '@autorest/csharp-v3',
  aliases: ['NET-Track2'],
  packageRootFileName: /.*\.sln|^src$/,
  packageNameCreator: getPackageName,
  afterGenerationCommands: createNugetPackage,
  packageCommands: findPackage,
  packageNameAltPrefix: 'track2_',
  installationInstructions: getInstallationInstructions
};
