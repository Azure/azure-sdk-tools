import { formatSuppressionLine } from '../../src/utils/reportFormat';
import YAML from 'yaml';
import { describe, it, expect } from 'vitest';

// Run with `npm test -- reportFormat` from tools/spec-gen-sdk.
describe('formatSuppressionLine', () => {
  it('easy case', () => {
    const suppressionLines = ['abc'];
    const formatResult = formatSuppressionLine(suppressionLines);
    expect(formatResult).toEqual(['- "abc"']);
  })

  it('case with /t', () => {
    const suppressionLines = ['+\tFunction `*LinkerClient.BeginCreateOrUpdate` has been removed'];
    const formatResult = formatSuppressionLine(suppressionLines);
    expect(formatResult).toEqual(['- "Function `*LinkerClient.BeginCreateOrUpdate` has been removed"']);
  })

  it('case by using object', () => {
    const suppressionLines = ["+\tType of parameter headers of interface WebHookActivity is changed from {\n        [propertyName: string]: string;\n    } to {\n        [propertyName: string]: any;\n    }"];
    const formatResult = formatSuppressionLine(suppressionLines);
    expect(formatResult).toContain('- "Type of parameter headers of interface WebHookActivity is changed from {\\n        [propertyName: string]: string;\\n    } to {\\n        [propertyName: string]: any;\\n    }"');
  })

  it.each([
    '`KindGen1` from enum `Kind` has been removed',
    '`NameS0`, `NameS1` from enum `Name` has been removed',
    'Property changed: string to number',
    'Message with # a hash',
    'Property "name" has been removed',
    'Path C:\\sdk\\new\\types',
    'Type "name" at C:\\sdk\\types changed\nfrom "string" to "number"',
    'Line one\r\nLine two\rLine three\n',
    '  leading and trailing whitespace  ',
    '\tTabbed message\t',
    '\u0000 control character',
    'true',
    'false',
    'null',
    '~',
    '123',
    '2026-01-01',
    '',
    '# comment',
    '*alias',
    '&anchor',
    '!tag',
    '[item]',
    '{key: value}',
    '- item',
    '> folded',
    '| literal',
    '---',
    'A long message '.repeat(30),
  ])('round-trips the exact suppression value %j', value => {
    const formatted = formatSuppressionLine([value, `+\t${value}`]);
    expect(formatted).toHaveLength(2);
    for (const entry of formatted) {
      expect(entry.startsWith('- "')).toBe(true);
      expect(entry).not.toContain('\n');
      expect(YAML.parse(entry)).toEqual([value]);
    }
    expect(YAML.parse(formatted.join('\n'))).toEqual([value, value]);
  });

  it('strips only a leading diff prefix', () => {
    const values = ['message +\t suffix', ' +\tmessage', '+message', '\tmessage', '+\tmessage'];
    const formatted = formatSuppressionLine([...values, '+\t+\tmessage']);
    expect(YAML.parse(formatted.join('\n'))).toEqual([
      ...values.slice(0, -1), 'message', '+\tmessage'
    ]);
  });

  it('preserves entry order and duplicates', () => {
    const values = ['`KindGen1` from enum `Kind` has been removed', 'abc', 'abc'];
    const formatted = formatSuppressionLine(values);
    expect(formatted).toHaveLength(values.length);
    expect(YAML.parse(formatted.join('\n'))).toEqual(values);
  });

  it('handles no suppression messages', () => {
    expect(formatSuppressionLine([])).toEqual([]);
  });

});
