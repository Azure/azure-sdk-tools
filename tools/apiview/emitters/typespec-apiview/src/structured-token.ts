/** Supported render classes for APIView v2.
 *  You can add custom ones but need to provide CSS to EngSys.
 */
export const enum RenderClass {
  text,
  keyword,
  punctuation,
  literal,
  comment,
  typeName = "type-name",
  memberName = "member-name",
  stringLiteral = "string-literal",
}

/** Tags supported by APIView v2 */
export enum TokenTag {
  /** Show item as deprecated. */
  deprecated,
  /** Hide item from APIView. */
  hidden,
  /** Hide item from APIView Navigation. */
  hideFromNav,
  /** Ignore differences in this item when calculating diffs. */
  skipDiff,
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
export class StructuredToken {
  value?: string;
  id?: string;
  kind: TokenKind;
  tags?: Set<string>;
  properties: Map<string, string>;
  renderClasses: Set<string>;

  constructor(kind: TokenKind, options?: TokenOptions) {
    this.id = options?.lineId;
    this.kind = kind;
    this.value = options?.value;
    this.properties = options?.properties ?? new Map<string, string>();
    this.renderClasses = new Set([...(options?.renderClasses ?? []).toString()]);
  }
}
