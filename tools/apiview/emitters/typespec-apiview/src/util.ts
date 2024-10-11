import { ReviewLine, ReviewToken } from "./schemas.js";

export function reviewLineText(line: ReviewLine, indent: number): string {
  const indentString = " ".repeat(indent);
  const tokenText = line.Tokens.map(t => reviewTokenText(t)).join("");
  const childrenText = line.Children.map(c => reviewLineText(c, indent + 2)).join("\n");
  return `${indentString}${tokenText}\n${childrenText}`;
}
  
function reviewTokenText(token: ReviewToken): string {
  const suffixSpace = token.HasSuffixSpace ? " " : "";
  const value = token.Value;
  return `${value}${suffixSpace}`;
}
