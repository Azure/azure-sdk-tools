
export function formatSuppressionLine(suppressionLines: string[]): string[] {
  return suppressionLines
    .map(lineItem => lineItem.startsWith('+\t') ? lineItem.replace('+\t', '') : lineItem)
    .map(newlineItem => newlineItem.includes('\n') ? `\"${newlineItem.replace(/\n/g, '\\n')}\"` : newlineItem)
    .map(_newlineItem => `- ${_newlineItem}`)
}
