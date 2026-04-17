import { ApiNamespace, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken } from "./helpers";

function isValid(item: ApiItem): item is ApiNamespace {
  return item.kind === ApiItemKind.Namespace;
}

function generate(item: ApiNamespace, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Namespace) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Namespace token generator.`,
    );
  }

  // Emit "declare namespace <Name>" to match the legacy behavior
  tokens.push(createToken(TokenKind.Keyword, "declare", { hasSuffixSpace: true, deprecated }));
  tokens.push(createToken(TokenKind.Keyword, "namespace", { hasSuffixSpace: true, deprecated }));

  const nameToken = createToken(TokenKind.TypeName, item.displayName, { deprecated });
  nameToken.NavigateToId = item.canonicalReference.toString();
  nameToken.NavigationDisplayName = item.displayName;
  nameToken.RenderClasses = ["namespace"];
  tokens.push(nameToken);

  return { tokens };
}

export const namespaceTokenGenerator: TokenGenerator<ApiNamespace> = {
  isValid,
  generate,
};
