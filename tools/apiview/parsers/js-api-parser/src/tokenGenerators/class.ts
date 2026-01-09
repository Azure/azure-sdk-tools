import {
  ApiClass,
  ApiItem,
  ApiItemKind,
  ApiDeclaredItem,
  ExcerptTokenKind,
} from "@microsoft/api-extractor-model";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator } from "./index";
import { buildToken, splitAndBuild } from "../jstokens";

function isValid(item: ApiItem): item is ApiClass {
  return item.kind === ApiItemKind.Class;
}

function generate(item: ApiClass, deprecated?: boolean): ReviewToken[] {
  const tokens: ReviewToken[] = [];
  if (item.kind !== ApiItemKind.Class) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Class token generator.`,
    );
  }

  if (item instanceof ApiDeclaredItem) {
    for (const excerpt of item.excerptTokens) {
      if (excerpt.kind === ExcerptTokenKind.Reference && excerpt.canonicalReference) {
        tokens.push(
          buildToken({
            Kind: TokenKind.TypeName,
            NavigateToId: excerpt.canonicalReference.toString(),
            Value: excerpt.text,
            IsDeprecated: deprecated,
          }),
        );
      } else {
        splitAndBuild(tokens, excerpt.text, item, deprecated);
      }
    }
  }

  return tokens;
}

export const classTokenGenerator: TokenGenerator<ApiClass> = {
  isValid,
  generate,
};
