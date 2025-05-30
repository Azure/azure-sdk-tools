import { parseSemverVersionString } from '../../src/utils/parseSemverVersionString';
import { describe, it, expect } from 'vitest';

describe('parseSemverVersionString', () => {

  it('should return undefined for undefined versionString or language', () => {
    expect(parseSemverVersionString(undefined, 'JavaScript')).toBeUndefined();
    expect(parseSemverVersionString("1.1", undefined)).toBeUndefined();
  });

  it('paser version for .Net', () => {
    const language = '.Net';
    const versionString = '4.6.0';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual('Beta');
    expect(parsedVersion?.isPrerelease).toEqual(false);
  });

  it('Parse a beta version for .Net', () => {
    const language = '.Net';
    const versionArr = ['1.2.0-beta.1', '0.1.0', '1.1.0-preview.5', '0.3.0-beta.6'];
    const parseArr = versionArr.map((versionString) => {
      return parseSemverVersionString(versionString, language)?.versionType;
    });
    expect(parseArr).toHaveLength(4);
    expect(
      parseArr.every((item) => {
        return item == 'Beta';
      }),
    ).toBeTruthy();
  });

  it('paser version for Java', () => {
    const language = 'Java';
    const versionString = '1.19.3';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual('Beta');
  });

  it('Parse a beta version for Java', () => {
    const language = 'Java';
    const versionString = '11.7.0-beta.2';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).toEqual('Beta');
  });

  it('Parse version string for Javascript', () => {
    const versionString = '02.24.25';
    const language = 'JavaScript';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.major).toEqual('2');
    expect(parsedVersion?.minor).toEqual('24');
    expect(parsedVersion?.patch).toEqual('25');
    expect(parsedVersion?.versionType).not.toEqual('Beta');
  });

  it('Parse a beta version for Javascript', () => {
    const versionString = '1.2.3-beta.1';
    const language = 'JavaScript';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).toEqual('Beta');
  });

  it('Parse version string for Python', () => {
    const versionString = '02.24.25';
    const language = 'Python';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual('Beta');
  });

  it('Parse a beta version for Python', () => {
    const language = 'Python';
    const versionString = '11.6.0b3';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).toEqual('Beta');
  });

  it('paser version for Go', () => {
    const language = 'Go';
    const versionString = '1.1.0';
    const parsedVersion = parseSemverVersionString(versionString, language);
    expect(parsedVersion?.versionType).not.toEqual('Beta');
  });

  it('Parse a beta version for Go', () => {
    const language = 'Go';
    const versionArr = ['1.6.0-beta.3', '0.3.0'];
    const parseArr = versionArr.map((versionString) => {
      return parseSemverVersionString(versionString, language)?.versionType;
    });
    expect(parseArr).toHaveLength(2);
    expect(
      parseArr.every((item) => {
        return item == 'Beta';
      }),
    ).toBeTruthy();
  });
});
