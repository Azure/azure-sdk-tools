import { LanguageConfiguration } from '../languageConfiguration';
import { initSetupPy, createPackage, getPackageName, getInstallationInstructions, generateBreakingChangeReport } from './python';
import { sdkLabels } from '@azure/swagger-validation-common';

/**
 * A language configuration for Python.
 */
export const pythonTrack2: LanguageConfiguration = {
  name: 'Python-Track2',
  generatorPackageName: '@autorest/python',
  packageRootFileName: /^setup.py$|^azure$/,
  packageNameCreator: getPackageName,
  packageCommands: createPackage,
  installationInstructions: getInstallationInstructions,
  packageNameAltPrefix: 'track2_',
  generateBreakingChangeReport: generateBreakingChangeReport,
  runLangAfterScripts : initSetupPy,
  breakingChangeLabel: { name: sdkLabels['azure-sdk-for-python-track2'].deprecatedBreakingChange as string, color: 'dc1432' },
  breakingChangesLabel: { name: sdkLabels['azure-sdk-for-python-track2'].deprecatedBreakingChange as string, color: 'dc1432' }
};
