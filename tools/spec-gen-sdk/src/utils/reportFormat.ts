
import YAML from 'yaml';

export function formatSuppressionLine(suppressionLines: string[]): string[] {
  return suppressionLines
    .map(lineItem => lineItem.startsWith('+\t') ? lineItem.slice(2) : lineItem)
    .map(lineItem => YAML.stringify([lineItem], {
      defaultStringType: 'QUOTE_DOUBLE',
      doubleQuotedAsJSON: true,
      blockQuote: false,
      lineWidth: 0
    }).trimEnd());
}
