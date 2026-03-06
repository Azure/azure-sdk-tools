import { ApiVariable, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, parseTypeText } from "./helpers";

function isValid(item: ApiItem): item is ApiVariable {
  return item.kind === ApiItemKind.Variable;
}

function generate(item: ApiVariable, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.Variable) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Variable token generator.`,
    );
  }

  // Add export keyword
  tokens.push(createToken(TokenKind.Keyword, "export", { hasSuffixSpace: true, deprecated }));

  // Check for default export
  const isDefaultExport = item.excerptTokens.some((t) => t.text.includes("export default"));
  if (isDefaultExport) {
    tokens.push(createToken(TokenKind.Keyword, "default", { hasSuffixSpace: true, deprecated }));
  }

  // Add const keyword
  tokens.push(createToken(TokenKind.Keyword, "const", { hasSuffixSpace: true, deprecated }));

  // Add variable name with navigation metadata
  const nameToken = createToken(TokenKind.MemberName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["variable"];
  tokens.push(nameToken);

  // Add colon and type (only if type annotation exists)
  const typeText = item.variableTypeExcerpt?.text?.trim();
  let children;

  if (typeText) {
    tokens.push(createToken(TokenKind.Punctuation, ":", { hasSuffixSpace: true, deprecated }));
    children = parseTypeText(typeText, tokens, deprecated);
  }

  // Add initializer value if present (e.g., = 1000000)
  const initializerText = item.initializerExcerpt?.text?.trim();
  if (initializerText) {
    tokens.push(
      createToken(TokenKind.Punctuation, "=", {
        hasPrefixSpace: true,
        hasSuffixSpace: true,
        deprecated,
      }),
    );
    tokens.push(createToken(TokenKind.StringLiteral, initializerText, { deprecated }));
  }

  if (!children) {
    tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));
  }

  return { tokens, children };
}

export const variableTokenGenerator: TokenGenerator<ApiVariable> = {
  isValid,
  generate,
};
