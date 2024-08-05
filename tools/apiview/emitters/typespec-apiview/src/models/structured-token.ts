import { ApiViewSerializable } from "../interface.js";

/** 
 * Helper enum to declare where a token should be placed.
 */
export const enum TokenLocation {
  /** Apithis.TopTokens. Most tokens will go here. */
  top,
  /** Apithis.BottomTokens. Useful for closing braces. */
  bottom,
}

/**
 * Describes the type of a structured token. Most tokens will be of type
 * content.
 */
export const enum StructuredTokenKind {
  content = 0,
  lineBreak = 1,
  nonBreakingSpace = 2,
  tabSpace = 3,
  parameterSeparator = 4,
}

/**
 * Options when creating a new StructuredToken.
 */
export interface TokenOptions {
  renderClasses?: (StructuredTokenRenderClass | string)[];
  value?: string;
  tags?: StructuredTokenTag[];
  properties?: Map<string, string>;
  location?: TokenLocation;
  lineId?: string;
  groupId?: "doc" | undefined;
  navigateToId?: string;
}

/**
 * New-style structured APIView token.
 */
export class StructuredToken implements ApiViewSerializable {
  id?: string;
  kind: StructuredTokenKind;
  value?: string;
  properties?: StructuredTokenProperties;
  tags?: StructuredTokenTag[];
  renderClasses?: (StructuredTokenRenderClass | string)[];

  constructor(kind: StructuredTokenKind, options?: TokenOptions) {
    this.id = options?.lineId;
    this.kind = kind;
    this.value = options?.value;
    this.properties = new StructuredTokenProperties(options?.groupId, options?.navigateToId);
    this.renderClasses = options?.renderClasses;
  }

  serialize(): object {
    return {
      Id: this.id,
      Kind: this.kind,
      Value: this.value,
      Properties: this.properties?.serialize(),
      Tags: this.tags,
      RenderClasses: this.renderClasses,
   }
  }
}

/**
 * Properties which can be set on a structured token.
 */
class StructuredTokenProperties implements ApiViewSerializable {
  groupId?: "doc" | undefined;
  navigateToId?: string;
  
  constructor(groupId?: "doc" | undefined, navigateToId?: string) {
    this.groupId = groupId;
    this.navigateToId = navigateToId;
  }
  
  serialize(): object {
    return {
      GroupId: this.groupId,
      NavigateToId: this.navigateToId,
    }
  }
}

/**
 * Tags which can be set on a structured token.
 */
export enum StructuredTokenTag {
  deprecated = "Deprecated",
  skipDiff = "SkipDiff"
}

/**
 * Render classes applicable to a structured token.
 */
export enum StructuredTokenRenderClass {
  comment = "comment",
  keyword = "keyword",
  literal = "literal",
  stringLiteral = "sliteral",
  memberName = "mname",
  typeName = "tname",
  punctuation = "punc",
  text = "text",
} 
