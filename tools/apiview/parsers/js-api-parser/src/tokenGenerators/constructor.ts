import { ApiConstructor, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import * as ts from "typescript";
import { ReviewToken, TokenKind } from "../models";
import { TokenGenerator, GeneratorResult } from "./index";
import { createToken, processExcerptTokens } from "./helpers";

const PARAMETER_PROPERTY_MODIFIERS = new Set([
  "public",
  "private",
  "protected",
  "readonly",
  "override",
]);

function isValid(item: ApiItem): item is ApiConstructor {
  return item.kind === ApiItemKind.Constructor;
}

function getConstructorNode(item: ApiConstructor): ts.ConstructorDeclaration | undefined {
  const signatureText = item.excerpt?.text?.trim();
  if (!signatureText) {
    return undefined;
  }

  const sourceText = `declare class __ApiViewConstructorContainer { ${signatureText} }`;
  const sourceFile = ts.createSourceFile(
    "constructor.ts",
    sourceText,
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TS,
  );

  for (const statement of sourceFile.statements) {
    if (!ts.isClassDeclaration(statement)) {
      continue;
    }

    for (const member of statement.members) {
      if (ts.isConstructorDeclaration(member)) {
        return member;
      }
    }
  }

  return undefined;
}

function getParameterPropertyModifiers(item: ApiConstructor): string[][] {
  const constructorNode = getConstructorNode(item);
  if (!constructorNode) {
    return [];
  }

  return constructorNode.parameters.map((parameter) => {
    const modifiers = parameter.modifiers ?? [];
    return modifiers
      .map((modifier) => modifier.getText())
      .filter((modifier) => PARAMETER_PROPERTY_MODIFIERS.has(modifier));
  });
}

function generate(item: ApiConstructor, deprecated?: boolean): GeneratorResult {
  const tokens: ReviewToken[] = [];

  if (item.kind !== ApiItemKind.Constructor) {
    throw new Error(
      `Invalid item ${item.displayName} of kind ${item.kind} for Constructor token generator.`,
    );
  }

  const parameters = item.parameters;
  const parameterModifiersByIndex = getParameterPropertyModifiers(item);

  if (item.isProtected) {
    tokens.push(
      createToken(TokenKind.Keyword, "protected", {
        hasSuffixSpace: true,
        deprecated,
      }),
    );
  }

  tokens.push(createToken(TokenKind.Keyword, "constructor", { deprecated }));
  tokens.push(createToken(TokenKind.Text, "(", { deprecated }));

  if (parameters?.length > 0) {
    parameters.forEach((param, index) => {
      const parameterModifiers = parameterModifiersByIndex[index] ?? [];

      parameterModifiers.forEach((modifier, modifierIndex) => {
        tokens.push(
          createToken(TokenKind.Keyword, modifier, {
            hasPrefixSpace: index > 0 && modifierIndex === 0,
            hasSuffixSpace: true,
            deprecated,
          }),
        );
      });

      tokens.push(
        createToken(TokenKind.Text, param.name, {
          hasPrefixSpace: index > 0 && parameterModifiers.length === 0,
          deprecated,
        }),
      );

      if (param.isOptional) {
        tokens.push(createToken(TokenKind.Text, "?", { deprecated }));
      }

      tokens.push(createToken(TokenKind.Text, ":", { hasSuffixSpace: true, deprecated }));
      processExcerptTokens(param.parameterTypeExcerpt.spannedTokens, tokens, deprecated);

      if (index < parameters.length - 1) {
        tokens.push(createToken(TokenKind.Text, ",", { hasSuffixSpace: true, deprecated }));
      }
    });
  }

  tokens.push(createToken(TokenKind.Text, ")", { deprecated }));
  tokens.push(createToken(TokenKind.Punctuation, ";", { deprecated }));

  return { tokens };
}

export const constructorTokenGenerator: TokenGenerator<ApiConstructor> = {
  isValid,
  generate,
};
