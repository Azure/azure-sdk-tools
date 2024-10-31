import { ReviewLine, ReviewToken } from "./schemas.js";

export function reviewLineText(line: ReviewLine, indent: number): string {
  const indentString = " ".repeat(indent);
  let tokenText = "";
  for (const token of line.Tokens) {
    tokenText += reviewTokenText(token, tokenText);
  }
  const childrenText = line.Children.map(c => reviewLineText(c, indent + 2)).join("\n");
  if (childrenText !== "") {
    return `${indentString}${tokenText}\n${childrenText}`;
  } else {
    return `${indentString}${tokenText}`;
  }
}
  
function reviewTokenText(token: ReviewToken, preview: string): string {
  const previewEndsInSpace = preview.endsWith(" ");
  const hasSuffixSpace = token.HasSuffixSpace !== undefined ? token.HasSuffixSpace : true;
  const suffixSpace = hasSuffixSpace ? " " : "";
  const prefixSpace = (token.HasPrefixSpace && !previewEndsInSpace) ? " " : "";
  const value = token.Value;
  return `${prefixSpace}${value}${suffixSpace}`;
}

export class NamespaceStack {
  stack = new Array<string>();

  push(val: string) {
    this.stack.push(val);
  }

  pop(): string | undefined {
    return this.stack.pop();
  }

  value(): string {
    return this.stack.join(".");
  }

  reset() {
    this.stack = Array<string>();
  }
}
