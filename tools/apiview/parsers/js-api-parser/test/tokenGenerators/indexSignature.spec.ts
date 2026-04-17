import { describe, expect, it } from "vitest";
import { indexSignatureTokenGenerator } from "../../src/tokenGenerators/indexSignature";
import {
  ApiIndexSignature,
  ApiItem,
  ApiItemKind,
  ExcerptTokenKind,
  Parameter,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

function createMockParameter(
  name: string,
  typeName: string,
  isOptional: boolean = false,
): Parameter {
  return {
    name,
    isOptional,
    parameterTypeExcerpt: {
      text: typeName,
      spannedTokens: [{ kind: ExcerptTokenKind.Content, text: typeName }],
    },
  } as unknown as Parameter;
}

function createMockIndexSignature(
  options: {
    parameters?: Parameter[];
    isReadonly?: boolean;
    returnTypeText?: string;
    returnTypeTokens?: any[];
  } = {},
): ApiIndexSignature {
  const {
    parameters = [createMockParameter("key", "string")],
    isReadonly = false,
    returnTypeText = "any",
    returnTypeTokens,
  } = options;

  const mock: any = {
    kind: ApiItemKind.IndexSignature,
    displayName: "",
    parameters,
    isReadonly,
    canonicalReference: {
      toString: () => `@test!TestInterface#(:index)`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    fileUrlPath: undefined,
    excerpt: {
      text: "",
      tokenRange: { startIndex: 0, endIndex: 0 },
      tokens: [],
    },
    excerptTokens: [],
    returnTypeExcerpt: {
      text: returnTypeText,
      spannedTokens: returnTypeTokens || [
        { kind: ExcerptTokenKind.Content, text: returnTypeText },
      ],
    },
    releaseTag: undefined,
    tsdocComment: undefined,
  };

  return mock as ApiIndexSignature;
}

describe("indexSignatureTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for index signature items", () => {
      const mock = { kind: ApiItemKind.IndexSignature } as ApiIndexSignature;
      expect(indexSignatureTokenGenerator.isValid(mock)).toBe(true);
    });

    it("returns false for non-index-signature items", () => {
      const mockMethod = { kind: ApiItemKind.Method } as ApiItem;
      expect(indexSignatureTokenGenerator.isValid(mockMethod)).toBe(false);
    });

    it("returns false for call signature items", () => {
      const mock = { kind: ApiItemKind.CallSignature } as ApiItem;
      expect(indexSignatureTokenGenerator.isValid(mock)).toBe(false);
    });
  });

  describe("generate", () => {
    it("throws for invalid items", () => {
      const mock = {
        kind: ApiItemKind.Method,
        displayName: "method",
      } as unknown as ApiIndexSignature;

      expect(() => indexSignatureTokenGenerator.generate(mock)).toThrow(
        "Invalid item method of kind Method for IndexSignature token generator.",
      );
    });

    it("generates tokens for a basic index signature [key: string]: any", () => {
      const mock = createMockIndexSignature();
      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual(["[", "key", ":", "string", "]", ":", "any", ";"]);
    });

    it("generates tokens for a readonly index signature", () => {
      const mock = createMockIndexSignature({ isReadonly: true });
      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual(["readonly", "[", "key", ":", "string", "]", ":", "any", ";"]);
      expect(tokens[0].Kind).toBe(TokenKind.Keyword);
    });

    it("does not include readonly keyword when not readonly", () => {
      const mock = createMockIndexSignature({ isReadonly: false });
      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const values = tokens.map((t) => t.Value);

      expect(values).not.toContain("readonly");
    });

    it("generates tokens with a number parameter type", () => {
      const mock = createMockIndexSignature({
        parameters: [createMockParameter("index", "number")],
        returnTypeText: "string",
      });
      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual(["[", "index", ":", "number", "]", ":", "string", ";"]);
    });

    it("generates tokens with multiple parameters", () => {
      const mock = createMockIndexSignature({
        parameters: [
          createMockParameter("row", "number"),
          createMockParameter("col", "number"),
        ],
        returnTypeText: "Cell",
      });
      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual([
        "[",
        "row",
        ":",
        "number",
        ",",
        "col",
        ":",
        "number",
        "]",
        ":",
        "Cell",
        ";",
      ]);
    });

    it("marks tokens as deprecated", () => {
      const mock = createMockIndexSignature();
      const { tokens } = indexSignatureTokenGenerator.generate(mock, true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("does not mark tokens as deprecated when deprecated is false", () => {
      const mock = createMockIndexSignature();
      const { tokens } = indexSignatureTokenGenerator.generate(mock, false);
      expect(tokens.every((t) => !t.IsDeprecated)).toBe(true);
    });

    it("generates tokens with type reference navigation in return type", () => {
      const mock = createMockIndexSignature({
        returnTypeTokens: [
          {
            kind: ExcerptTokenKind.Reference,
            text: "MyModel",
            canonicalReference: {
              toString: () => "@azure/test!MyModel:interface",
            },
          },
        ],
        returnTypeText: "MyModel",
      });

      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const typeToken = tokens.find((t) => t.Value === "MyModel");
      expect(typeToken).toBeDefined();
      expect(typeToken!.Kind).toBe(TokenKind.TypeName);
      expect(typeToken!.NavigateToId).toBe("@azure/test!MyModel:interface");
    });

    it("generates tokens with type reference navigation in parameter type", () => {
      const mock = createMockIndexSignature({
        parameters: [
          {
            name: "key",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "MyKey",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "MyKey",
                  canonicalReference: {
                    toString: () => "@azure/test!MyKey:type",
                  },
                },
              ],
            },
          } as unknown as Parameter,
        ],
        returnTypeText: "string",
      });

      const { tokens } = indexSignatureTokenGenerator.generate(mock);
      const typeToken = tokens.find((t) => t.Value === "MyKey");
      expect(typeToken).toBeDefined();
      expect(typeToken!.Kind).toBe(TokenKind.TypeName);
      expect(typeToken!.NavigateToId).toBe("@azure/test!MyKey:type");
    });

    it("uses correct token kinds for punctuation and text", () => {
      const mock = createMockIndexSignature();
      const { tokens } = indexSignatureTokenGenerator.generate(mock);

      const bracket = tokens.find((t) => t.Value === "[");
      expect(bracket!.Kind).toBe(TokenKind.Punctuation);

      const closeBracket = tokens.find((t) => t.Value === "]");
      expect(closeBracket!.Kind).toBe(TokenKind.Punctuation);

      const paramName = tokens.find((t) => t.Value === "key");
      expect(paramName!.Kind).toBe(TokenKind.Text);

      const semicolon = tokens.find((t) => t.Value === ";");
      expect(semicolon!.Kind).toBe(TokenKind.Punctuation);
    });
  });
});
