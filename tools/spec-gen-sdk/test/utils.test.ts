import { parseSemverVersionString } from '../src/utils/parseSemverVersionString';
import { removeAnsiEscapeCodes, removeDuplicatesFromRelatedFiles } from '../src/utils/utils';
import { WorkflowContext } from '../src/automation/workflow';
import * as typespecUtils from '../src/utils/typespecUtils';

// To invoke these tests, run `npm run test-utils` from the "private/openapi-sdk-automation" directory.
describe('parseSemverVersionString', () => {

  it('paser version for .Net', () => {
    const language = '.Net';
    const versionString = '4.6.0';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual("Beta");
    expect(parsedVersion?.isPrerelease).toEqual(false);
  });

  it('Parse a beta version for .Net', () => {
    const language = '.Net';
    const versionArr = ['1.2.0-beta.1', '0.1.0', '1.1.0-preview.5', '0.3.0-beta.6'];
    const parseArr = versionArr.map(versionString => {
      return parseSemverVersionString(versionString, language)?.versionType;
    })
    expect(parseArr).toHaveLength(4);
    expect(parseArr.every(item => { return item == 'Beta' })).toBeTruthy();
  });

  it('paser version for Java', () => {
    const language = 'Java';
    const versionString = '1.19.3';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual("Beta");
  });

  it('Parse a beta version for Java', () => {
    const language = 'Java';
    const versionString = '11.7.0-beta.2';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).toEqual("Beta");
  });

  it('Parse version string for Javascript', () => {
    const versionString = '02.24.25';
    const language = 'JavaScript';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.major).toEqual("2");
    expect(parsedVersion?.minor).toEqual("24");
    expect(parsedVersion?.patch).toEqual("25");
    expect(parsedVersion?.versionType).not.toEqual("Beta");
  });

  it('Parse a beta version for Javascript', () => {
    const versionString = '1.2.3-beta.1';
    const language = 'JavaScript';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).toEqual("Beta");
  });

  it('Parse version string for Python', () => {
    const versionString = '02.24.25';
    const language = 'Python';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual("Beta");
  });

  it('Parse a beta version for Python', () => {
    const language = 'Python';
    const versionString = '11.6.0b3';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).toEqual("Beta");
  });

  it('paser version for Go', () => {
    const language = 'Go';
    const versionString = '1.1.0';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual("Beta");
  });

  it('Parse a beta version for Go', () => {
    const language = 'Go';
    const versionArr = ['1.6.0-beta.3', '0.3.0'];
    const parseArr = versionArr.map(versionString => {
      return parseSemverVersionString(versionString, language)?.versionType;
    })
    expect(parseArr).toHaveLength(2);
    expect(parseArr.every(item => { return item == 'Beta' })).toBeTruthy();
  });
});

describe('Remove AnsiEscape Codes', () => {
  it('test ansi code array', () => {
    const ansiArr = [
      "command\tpwsh ./eng/scripts/Automation-Sdk-Init.ps1 ../azure-sdk-for-net_tmp/initInput.json ../azure-sdk-for-net_tmp/initOutput.json",
      "command\tpwsh ./eng/scripts/Invoke-GenerateAndBuildV2.ps1 ../azure-sdk-for-net_tmp/generateInput.json ../azure-sdk-for-net_tmp/generateOutput.json",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1mGeneratePackage: \u001b[0m/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:714\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1mLine |\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m 714 | \u001b[0m         \u001b[36;1mGeneratePackage -projectFolder $projectFolder -sdkRootPath $s\u001b[0m …\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m     | \u001b[31;1m         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mFailed to generate sdk. exit code: False\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1mGet-ChildItem: \u001b[0m/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:807\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1mLine |\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m 807 | \u001b[0m … rtifacts += \u001b[36;1mGet-ChildItem $artifactsPath -Filter *.nupkg -exclude *.s\u001b[0m …\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m     | \u001b[31;1m               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mCannot find path\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1m'/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/artifacts/packages/Debug/'\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m\u001b[31;1m\u001b[36;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mbecause it does not exist.\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1mGeneratePackage: \u001b[0m/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:714\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1mLine |\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m 714 | \u001b[0m         \u001b[36;1mGeneratePackage -projectFolder $projectFolder -sdkRootPath $s\u001b[0m …\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m     | \u001b[31;1m         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\u001b[0m",
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mFailed to generate sdk artifact\u001b[0m",
      "Pushing to https://github.com/azure-sdk/azure-sdk-for-net\nremote: Permission to azure-sdk/azure-sdk-for-net.git denied to JackTn.\nfatal: unable to access 'https://github.com/azure-sdk/azure-sdk-for-net/': The requested URL returned error: 403\n",
    ]

    const resArr = [
      'command\tpwsh ./eng/scripts/Automation-Sdk-Init.ps1 ../azure-sdk-for-net_tmp/initInput.json ../azure-sdk-for-net_tmp/initOutput.json',
      'command\tpwsh ./eng/scripts/Invoke-GenerateAndBuildV2.ps1 ../azure-sdk-for-net_tmp/generateInput.json ../azure-sdk-for-net_tmp/generateOutput.json',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] GeneratePackage: /home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:714',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] Line |',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]  714 |          GeneratePackage -projectFolder $projectFolder -sdkRootPath $s …',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      |          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      | Failed to generate sdk. exit code: False',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] Get-ChildItem: /home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:807',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] Line |',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]  807 |  … rtifacts += Get-ChildItem $artifactsPath -Filter *.nupkg -exclude *.s …',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      |                ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      | Cannot find path',
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1]      | '/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/artifacts/packages/Debug/'",
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      | because it does not exist.',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] GeneratePackage: /home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:714',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] Line |',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]  714 |          GeneratePackage -projectFolder $projectFolder -sdkRootPath $s …',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      |          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1]      | Failed to generate sdk artifact',
      'Pushing to https://github.com/azure-sdk/azure-sdk-for-net\n' +
      'remote: Permission to azure-sdk/azure-sdk-for-net.git denied to JackTn.\n' +
      "fatal: unable to access 'https://github.com/azure-sdk/azure-sdk-for-net/': The requested URL returned error: 403\n"
    ]

    expect(removeAnsiEscapeCodes(ansiArr)).toEqual(expect.arrayContaining(resArr));
  })
})

describe('getTypeSpecProjectServiceName', () => {
  it('test getTypeSpecProjectServiceName', () => {
    const outputFolder = '{azure-resource-provider-folder}/Language/{version-status}/{version}/analyzetext.json';
    const res = typespecUtils.getTypeSpecProjectServiceName(outputFolder);
    expect(res).toEqual('Language');
  })

  it('test getTypeSpecProjectServiceName when output service-name ', () => {
    const outputFolder = '{azure-resource-provider-folder}/{service-name}/{version-status}/{version}/analyzetext.json';
    const res = typespecUtils.getTypeSpecProjectServiceName(outputFolder);
    expect(res).toEqual('{service-name}');
  })
})

describe('getTypeSpecProjectResourceProvider', () => {
  it('test getTypeSpecProjectResourceProvider 1', () => {
    const typespecProject = 'specification/cognitiveservices/OpenAI.Inference';
    const res = typespecUtils.getTypeSpecProjectResourceProvider(typespecProject);
    expect(res).toEqual('OpenAI.Inference');
  })
  it('test getTypeSpecProjectResourceProvider 2', () => {
    const typespecProject = 'specification/cognitiveservices/ContentSafety/a/b/c';
    const res = typespecUtils.getTypeSpecProjectResourceProvider(typespecProject);
    expect(res).toEqual('ContentSafety/a/b/c');
  })
})

describe('remove relatedTypeSpecProjectFolder & relatedReadmeMdFiles duplicates', () => {
  const context = {
    specFolder: 'path/to/specs',
    logger: {
      info: jest.fn() as unknown,
    }
  } as WorkflowContext;

  it('test dup only resource-manage', () => {
    const relatedTypeSpecProjectFolder = [
      "specification/azurefleet/AzureFleet.Management",
    ];
    const relatedReadmeMdFiles = [
      "specification/appcomplianceautomation/resource-manager/readme.md",
      "specification/azurefleet/resource-manager/readme.md",
    ]
    const res = removeDuplicatesFromRelatedFiles(relatedTypeSpecProjectFolder, relatedReadmeMdFiles, context);
    expect(res).toEqual([
      'specification/appcomplianceautomation/resource-manager/readme.md'
    ])
  })

  it('test dup only data-plane', () => {
    const relatedTypeSpecProjectFolder = [
      "specification/cognitiveservices/ContentSafety",
    ];
    const relatedReadmeMdFiles = [
      "specification/cognitiveservices/data-plane/ContentSafety/readme.md",
    ];
    jest.spyOn(typespecUtils, 'getTypeSpecOutputFolder').mockReturnValue('data-plane/ContentSafety');
    const res = removeDuplicatesFromRelatedFiles(relatedTypeSpecProjectFolder, relatedReadmeMdFiles, context);
    expect(res).toEqual([]);
  })

  it('test dup only data-plane', () => {
    const relatedTypeSpecProjectFolder = [
      "specification/cognitiveservices/ContentSafety",
    ];
    const relatedReadmeMdFiles = [
      "specification/cognitiveservices/data-plane/ContentSafety/readme.md",
    ];
    jest.spyOn(typespecUtils, 'getTypeSpecOutputFolder').mockReturnValue('data-plane/ContentSafety1');
    const res = removeDuplicatesFromRelatedFiles(relatedTypeSpecProjectFolder, relatedReadmeMdFiles, context);
    expect(res).toEqual(["specification/cognitiveservices/data-plane/ContentSafety/readme.md"]);
  })
})


