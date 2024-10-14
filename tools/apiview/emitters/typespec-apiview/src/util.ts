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

/** A simple structure that holds the last n ReviewLines in reverse order, without nesting */
export class ReviewLineLookback {
  lines: ReviewLine[];
  maxSize: number;

  constructor(maxSize: number = 10) {
    this.maxSize = maxSize;
    this.lines = [];
  }

  push(line: ReviewLine) {
    if (this.lines.length >= this.maxSize) {
      this.lines.pop();
    }
    this.lines.unshift(line);
  }
}
