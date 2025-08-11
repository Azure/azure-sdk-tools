import { removeAnsiEscapeCodes, diffStringArrays, extractPathFromSpecConfig, getValueByKey, mapToObject, parseYamlContent } from '../../src/utils/utils';

import { describe, it, expect } from 'vitest';

describe('Remove AnsiEscape Codes', () => {
  it('test ansi code array', () => {
    const ansiArr = [
      'command\tpwsh ./eng/scripts/Automation-Sdk-Init.ps1 ../azure-sdk-for-net_tmp/initInput.json ../azure-sdk-for-net_tmp/initOutput.json',
      'command\tpwsh ./eng/scripts/Invoke-GenerateAndBuildV2.ps1 ../azure-sdk-for-net_tmp/generateInput.json ../azure-sdk-for-net_tmp/generateOutput.json',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1mGeneratePackage: \u001b[0m/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:714\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1mLine |\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m 714 | \u001b[0m         \u001b[36;1mGeneratePackage -projectFolder $projectFolder -sdkRootPath $s\u001b[0m …\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m     | \u001b[31;1m         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mFailed to generate sdk. exit code: False\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1mGet-ChildItem: \u001b[0m/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:807\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1mLine |\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m 807 | \u001b[0m … rtifacts += \u001b[36;1mGet-ChildItem $artifactsPath -Filter *.nupkg -exclude *.s\u001b[0m …\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m     | \u001b[31;1m               ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mCannot find path\u001b[0m',
      "cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1m'/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/artifacts/packages/Debug/'\u001b[0m",
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m\u001b[31;1m\u001b[36;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mbecause it does not exist.\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1mGeneratePackage: \u001b[0m/home/tianenx/tmp1/sdkauto/azure-sdk-for-net/eng/scripts/automation/GenerateAndBuildLib.ps1:714\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1mLine |\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m 714 | \u001b[0m         \u001b[36;1mGeneratePackage -projectFolder $projectFolder -sdkRootPath $s\u001b[0m …\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m     | \u001b[31;1m         ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~\u001b[0m',
      'cmderr\t[Invoke-GenerateAndBuildV2.ps1] \u001b[31;1m\u001b[0m\u001b[36;1m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[0m\u001b[36;1m\u001b[31;1m\u001b[31;1m\u001b[36;1m     | \u001b[31;1mFailed to generate sdk artifact\u001b[0m',
      "Pushing to https://github.com/azure-sdk/azure-sdk-for-net\nremote: Permission to azure-sdk/azure-sdk-for-net.git denied to JackTn.\nfatal: unable to access 'https://github.com/azure-sdk/azure-sdk-for-net/': The requested URL returned error: 403\n",
    ];

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
        "fatal: unable to access 'https://github.com/azure-sdk/azure-sdk-for-net/': The requested URL returned error: 403\n",
    ];

    expect(removeAnsiEscapeCodes(ansiArr)).toEqual(expect.arrayContaining(resArr));
  });

  it('test ansi code error in net generate script', () => {
    const ansiError =
      '\x1b[31;1mWrite-Error: \x1b[31;1m[ERROR] The service service is not onboarded yet. We will not support onboard a new service from swagger. Please contact the DotNet language support channel at https://aka.ms/azsdk/donet-teams-channel and include this spec pull request.\x1b[0m';
    expect(removeAnsiEscapeCodes(ansiError)).toEqual(
      'Write-Error: [ERROR] The service service is not onboarded yet. We will not support onboard a new service from swagger. Please contact the DotNet language support channel at https://aka.ms/azsdk/donet-teams-channel and include this spec pull request.',
    );
  });
});

describe('test diffStringArrays between breakingchanges from generate script and present suppressions', () => {
  it('test diffStringArrays if both same', () => {
    const left = ['Function `*LinkerClient.NewListPager` has been removed'];
    const right = ['Function `*LinkerClient.NewListPager` has been removed'];
    const res = diffStringArrays(left, right);
    expect(res.hasDiff).toEqual(false);
    expect(res.diffResult).toEqual(['\tFunction `*LinkerClient.NewListPager` has been removed']);
  });

  it('test diffStringArrays only added', () => {
    const left = ['Function `*LinkerClient.NewListPager` has been removed'];
    const right = [
      'Function `*LinkerClient.NewListPager` has been select',
      "'Type of `OperationStatus.Properties` has been changed from `map[string]interface{}` to `interface{}`'",
    ];
    const res = diffStringArrays(left, right);
    expect(res.hasDiff).toEqual(true);
    expect(res.diffResult).toEqual([
      '+\tFunction `*LinkerClient.NewListPager` has been select',
      "+\t'Type of `OperationStatus.Properties` has been changed from `map[string]interface{}` to `interface{}`'",
    ]);
  });

  it('test diffStringArrays only removed', () => {
    const left = ['Function `*LinkerClient.NewListPager` has been removed'];
    const right = [];
    const res = diffStringArrays(left, right);
    expect(res.hasDiff).toEqual(false);
    expect(res.diffResult).toEqual([]);
  });

  it('test diffStringArrays edit both', () => {
    const left = ['Function `*LinkerClient.NewListPager` has been select'];
    const right = ["'Type of `OperationStatus.Properties` has been changed from `map[string]interface{}` to `interface{}`'"];
    const res = diffStringArrays(left, right);
    expect(res.hasDiff).toEqual(true);
    expect(res.diffResult).toEqual(["+\t'Type of `OperationStatus.Properties` has been changed from `map[string]interface{}` to `interface{}`'"]);
  });
});

describe('extract and format the prefix from spec config path', () => {
  it('should extract and format the prefix from tspConfigPath', () => {
    const tspConfigPath = 'specification/myService.management/tspconfig.yaml';
    const readmePath = undefined;
    const result = extractPathFromSpecConfig(tspConfigPath, readmePath);
    expect(result).toEqual('myservice-management');
  });

  it('should extract and format the prefix from readmePath', () => {
    const tspConfigPath = undefined;
    const readmePath = 'specification/myService/resource-manager/readme.md';
    const result = extractPathFromSpecConfig(tspConfigPath, readmePath);
    expect(result).toEqual('myservice-resource-manager');
  });

  it('should extract and format the prefix from readmePath', () => {
    const tspConfigPath = undefined;
    const readmePath = 'specification/myService/subservice/data-plane/readme.md';
    const result = extractPathFromSpecConfig(tspConfigPath, readmePath);
    expect(result).toEqual('myservice-subservice-data-plane');
  });

  it('should return an empty string if paths are not provided', () => {
    const tspConfigPath = undefined;
    const readmePath = undefined;
    const result = extractPathFromSpecConfig(tspConfigPath, readmePath);
    expect(result).toMatch(/no-readme-tspconfig-\d+/);
  });
});

describe('getValueByKey', () => {
  it('should return the value for the given key if present', () => {
    const arr = [{ foo: 1, bar: 2 }, { baz: 3 }] as { [key: string]: any }[];
    expect(getValueByKey(arr, 'foo')).toBe(1);
    expect(getValueByKey(arr, 'baz')).toBe(3);
  });

  it('should return undefined if the key is not present', () => {
    const arr = [{ foo: 1 }, { bar: 2 }] as { [key: string]: any }[];
    expect(getValueByKey(arr, 'baz')).toBeUndefined();
  });

  it('should return undefined for empty array', () => {
    expect(getValueByKey([], 'foo')).toBeUndefined();
  });
});

describe('parseYamlContent', () => {
  it('should parse valid YAML string to object', () => {
    const yaml = `
        foo: bar
        baz:
          - 1
          - 2
      `;
    const result = parseYamlContent(yaml);
    expect(result).toEqual({ foo: 'bar', baz: [1, 2] });
  });

  it('should return null for empty YAML', () => {
    expect(parseYamlContent('')).toBeNull();
    expect(parseYamlContent('\n')).toBeNull();
  });

  it('should throw error for invalid YAML', () => {
    expect(() => parseYamlContent('foo: [bar')).toThrow();
  });
});

describe('mapToObject', () => {
  it('should convert a Map to an object', () => {
    const map = new Map<string, number>([
      ['a', 1],
      ['b', 2],
    ]);
    expect(mapToObject(map)).toEqual({ a: 1, b: 2 });
  });

  it('should handle non-string keys by stringifying them', () => {
    const map = new Map<any, string>([
      [1, 'one'],
      [{ foo: 'bar' }, 'obj'],
    ]);
    const obj = mapToObject(map);
    expect(obj['1']).toBe('one');
    expect(obj['[object Object]']).toBe('obj');
  });

  it('should return an empty object for an empty map', () => {
    expect(mapToObject(new Map())).toEqual({});
  });
});
