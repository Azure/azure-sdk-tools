import { describe, expect, it } from "vitest";
import { callableSignatureTokenGenerator } from "../../src/tokenGenerators/callableSignature";
import {
  ApiCallSignature,
  ApiConstructSignature,
  ApiItem,
  ApiItemKind,
  ExcerptTokenKind,
  Parameter,
  TypeParameter,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

function createMockCallSignature(
  options: {
    parameters?: Parameter[];
    typeParameters?: TypeParameter[];
    returnTypeText?: string;
    returnTypeTokens?: any[];
  } = {},
): ApiCallSignature {
  const {
    parameters = [],
    typeParameters = [],
    returnTypeText = "void",
    returnTypeTokens,
  } = options;

  const mock: any = {
    kind: ApiItemKind.CallSignature,
    displayName: "",
    parameters,
    typeParameters,
    canonicalReference: {
      toString: () => `@test!TestInterface#(:call)`,
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

  return mock as ApiCallSignature;
}

function createMockConstructSignature(
  options: {
    parameters?: Parameter[];
    typeParameters?: TypeParameter[];
    returnTypeText?: string;
    returnTypeTokens?: any[];
  } = {},
): ApiConstructSignature {
  const {
    parameters = [],
    typeParameters = [],
    returnTypeText = "MyClass",
    returnTypeTokens,
  } = options;

  const mock: any = {
    kind: ApiItemKind.ConstructSignature,
    displayName: "",
    parameters,
    typeParameters,
    canonicalReference: {
      toString: () => `@test!TestInterface#(:new)`,
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

  return mock as ApiConstructSignature;
}

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

describe("callableSignatureTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for call signature items", () => {
      const mock = { kind: ApiItemKind.CallSignature } as ApiCallSignature;
      expect(callableSignatureTokenGenerator.isValid(mock)).toBe(true);
    });

    it("returns true for construct signature items", () => {
      const mock = { kind: ApiItemKind.ConstructSignature } as ApiConstructSignature;
      expect(callableSignatureTokenGenerator.isValid(mock)).toBe(true);
    });

    it("returns false for non-signature items", () => {
      const mockMethod = { kind: ApiItemKind.Method } as ApiItem;
      expect(callableSignatureTokenGenerator.isValid(mockMethod)).toBe(false);
    });

    it("returns false for constructor items", () => {
      const mock = { kind: ApiItemKind.Constructor } as ApiItem;
      expect(callableSignatureTokenGenerator.isValid(mock)).toBe(false);
    });
  });

  describe("call signature", () => {
    it("throws for invalid items", () => {
      const mock = {
        kind: ApiItemKind.Method,
        displayName: "method",
      } as unknown as ApiCallSignature;

      expect(() => callableSignatureTokenGenerator.generate(mock)).toThrow(
        "Invalid item method of kind Method for Signature token generator.",
      );
    });

    it("generates tokens for a simple call signature with no parameters", () => {
      const mock = createMockCallSignature();
      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual(["(", ")", ":", "void", ";"]);
    });

    it("generates tokens for a call signature with parameters", () => {
      const mock = createMockCallSignature({
        parameters: [
          createMockParameter("a", "number"),
          createMockParameter("b", "string"),
        ],
        returnTypeText: "boolean",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toContain("a");
      expect(values).toContain("number");
      expect(values).toContain(",");
      expect(values).toContain("b");
      expect(values).toContain("string");
      expect(values).toContain("boolean");
      expect(values[values.length - 1]).toBe(";");
    });

    it("generates tokens for a call signature with optional parameter", () => {
      const mock = createMockCallSignature({
        parameters: [createMockParameter("options", "RequestOptions", true)],
        returnTypeText: "void",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toContain("options");
      expect(values).toContain("?");
      expect(values).toContain("RequestOptions");
    });

    it("generates tokens for a call signature with type parameters", () => {
      const mock = createMockCallSignature({
        typeParameters: [
          {
            name: "T",
            constraintExcerpt: { text: "", spannedTokens: [] },
            defaultTypeExcerpt: { text: "", spannedTokens: [] },
          } as unknown as TypeParameter,
        ],
        parameters: [createMockParameter("value", "T")],
        returnTypeText: "T",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toContain("<");
      expect(values).toContain("T");
      expect(values).toContain(">");
    });

    it("does not include new keyword", () => {
      const mock = createMockCallSignature();
      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).not.toContain("new");
    });

    it("marks tokens as deprecated", () => {
      const mock = createMockCallSignature();
      const { tokens } = callableSignatureTokenGenerator.generate(mock, true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("generates tokens with type reference navigation", () => {
      const mock = createMockCallSignature({
        parameters: [
          {
            name: "input",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "MyType",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "MyType",
                  canonicalReference: {
                    toString: () => "@azure/test!MyType:interface",
                  },
                },
              ],
            },
          } as unknown as Parameter,
        ],
        returnTypeText: "void",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const typeToken = tokens.find((t) => t.Value === "MyType");
      expect(typeToken).toBeDefined();
      expect(typeToken!.Kind).toBe(TokenKind.TypeName);
      expect(typeToken!.NavigateToId).toBe("@azure/test!MyType:interface");
    });
  });

  describe("construct signature", () => {
    it("generates tokens for a simple construct signature with no parameters", () => {
      const mock = createMockConstructSignature();
      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual(["new", "(", ")", ":", "MyClass", ";"]);
      expect(tokens[0].Kind).toBe(TokenKind.Keyword);
    });

    it("generates tokens for a construct signature with parameters", () => {
      const mock = createMockConstructSignature({
        parameters: [
          createMockParameter("endpoint", "string"),
          createMockParameter("options", "ClientOptions"),
        ],
        returnTypeText: "MyClient",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values[0]).toBe("new");
      expect(values).toContain("endpoint");
      expect(values).toContain("string");
      expect(values).toContain(",");
      expect(values).toContain("options");
      expect(values).toContain("ClientOptions");
      expect(values).toContain("MyClient");
      expect(values[values.length - 1]).toBe(";");
    });

    it("generates tokens for a construct signature with optional parameter", () => {
      const mock = createMockConstructSignature({
        parameters: [createMockParameter("options", "ClientOptions", true)],
        returnTypeText: "MyClient",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toContain("new");
      expect(values).toContain("options");
      expect(values).toContain("?");
      expect(values).toContain("ClientOptions");
    });

    it("generates tokens for a construct signature with type parameters", () => {
      const mock = createMockConstructSignature({
        typeParameters: [
          {
            name: "T",
            constraintExcerpt: { text: "", spannedTokens: [] },
            defaultTypeExcerpt: { text: "", spannedTokens: [] },
          } as unknown as TypeParameter,
        ],
        parameters: [createMockParameter("value", "T")],
        returnTypeText: "Container<T>",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const values = tokens.map((t) => t.Value);

      expect(values[0]).toBe("new");
      expect(values).toContain("<");
      expect(values).toContain("T");
      expect(values).toContain(">");
    });

    it("marks tokens as deprecated", () => {
      const mock = createMockConstructSignature();
      const { tokens } = callableSignatureTokenGenerator.generate(mock, true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("generates tokens with type reference navigation", () => {
      const mock = createMockConstructSignature({
        returnTypeTokens: [
          {
            kind: ExcerptTokenKind.Reference,
            text: "MyClient",
            canonicalReference: {
              toString: () => "@azure/test!MyClient:class",
            },
          },
        ],
        returnTypeText: "MyClient",
      });

      const { tokens } = callableSignatureTokenGenerator.generate(mock, false);
      const typeToken = tokens.find((t) => t.Value === "MyClient");
      expect(typeToken).toBeDefined();
      expect(typeToken!.Kind).toBe(TokenKind.TypeName);
      expect(typeToken!.NavigateToId).toBe("@azure/test!MyClient:class");
    });
  });
});
