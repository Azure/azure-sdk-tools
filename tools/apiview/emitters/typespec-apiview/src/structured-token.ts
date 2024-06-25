import { ApiViewSerializable } from "./apiview.js";

/** Supported render classes for APIView v2.
 *  You can add custom ones but need to provide CSS to EngSys.
 */
export const enum RenderClass {
  text = "text",
  keyword = "keyword",
  punctuation = "punctuation",
  literal = "literal",
  comment = "comment",
  typeName = "type-name",
  memberName = "member-name",
  stringLiteral = "string-literal",
}

/** Tags supported by APIView v2 */
export enum TokenTag {
  /** Show item as deprecated. */
  deprecated = "deprecated",
  /** Hide item from APIView. */
  hidden = "hidden",
  /** Hide item from APIView Navigation. */
  hideFromNav = "hideFromNav",
  /** Ignore differences in this item when calculating diffs. */
  skipDiff = "skipDiff",
}

export const enum TokenLocation {
  /** Apithis.TopTokens. Most tokens will go here. */
  top,
  /** Apithis.BottomTokens. Useful for closing braces. */
  bottom,
}

/**
 * Describes the type of structured token.
 */
export const enum TokenKind {
  content = 0,
  lineBreak = 1,
  nonBreakingSpace = 2,
  tabSpace = 3,
  parameterSeparator = 4,
  url = 5,
}

/**
 * Options when creating a new StructuredToken.
 */
export interface TokenOptions {
  renderClasses?: RenderClass[];
  value?: string;
  tags?: TokenTag[];
  properties?: Map<string, string>;
  location?: TokenLocation;
  lineId?: string;
}

/**
 * New-style structured APIView token.
 */
export class StructuredToken implements ApiViewSerializable {
  value?: string;
  id?: string;
  kind: TokenKind;
  tags?: TokenTag[];
  properties?: Map<string, string>;
  renderClasses?: RenderClass[];

  constructor(kind: TokenKind, options?: TokenOptions) {
    this.id = options?.lineId;
    this.kind = kind;
    this.value = options?.value;
    this.tags = options?.tags;
    this.properties = options?.properties;
    this.renderClasses = options?.renderClasses;
  }

  static fromJSON(json: any): StructuredToken {
    const token = new StructuredToken(json.Kind);
    token.value = json.Value;
    token.id = json.Id;
    token.tags = json.Tags;
    token.properties = json.Properties;
    token.renderClasses = json.RenderClasses;
    return token;
  }

  toJSON(abbreviate: boolean): object {
    const value = this.value;
    const id = this.id;
    const kind = this.kind;
    const tags = this.tags ? this.tags.map((x) => x.toString()) : undefined;
    const properties = this.properties;
    const renderClasses = this.renderClasses ? this.renderClasses.map((x) => x.toString()) : undefined;
    let result = {};
    if (abbreviate) {
      result = {
        v: value,
        i: id,
        k: kind,
        t: tags,
        p: properties,
        rc: renderClasses,
      };
    } else {
      result = {
        Value: value,
        Id: id,
        Kind: kind,
        Tags: tags,
        Properties: properties,
        RenderClasses: renderClasses,
      };
    }
    return result;
  }

  toText(): string {
    switch (this.kind) {
      case TokenKind.lineBreak:
        return "\n";
      case TokenKind.nonBreakingSpace:
        return " ";
      case TokenKind.tabSpace:
        return "\t";
      case TokenKind.parameterSeparator:
        return ",";
      default:
        return this.value ?? "";
    }
  }
}
