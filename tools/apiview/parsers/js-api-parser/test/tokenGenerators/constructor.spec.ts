import { describe, expect, it } from "vitest";
import { constructorTokenGenerator } from "../../src/tokenGenerators/constructor";
import {
  ApiConstructor,
  ApiItem,
  ApiItemKind,
  ExcerptTokenKind,
  Parameter,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

function createMockConstructor(
  options: {
    isProtected?: boolean;
    parameters?: Parameter[];
    excerptText?: string;
    overloadIndex?: number;
  } = {},
): ApiConstructor {
  const {
    isProtected = false,
    parameters = [],
    excerptText = "constructor()",
    overloadIndex = 1,
  } = options;

  const mock: any = {
    kind: ApiItemKind.Constructor,
    displayName: "constructor",
    isProtected,
    parameters,
    canonicalReference: {
      toString: () => `@test!TestClass#constructor(${overloadIndex}):member`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    fileUrlPath: undefined,
    excerpt: {
      text: excerptText,
      tokenRange: { startIndex: 0, endIndex: 0 },
      tokens: [],
    },
    excerptTokens: [],
    releaseTag: undefined,
    tsdocComment: undefined,
    overloadIndex,
  };

  return mock as ApiConstructor;
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

describe("constructorTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for constructor items", () => {
      const mockConstructor = { kind: ApiItemKind.Constructor } as ApiConstructor;
      expect(constructorTokenGenerator.isValid(mockConstructor)).toBe(true);
    });

    it("returns false for non-constructor items", () => {
      const mockMethod = { kind: ApiItemKind.Method } as ApiItem;
      expect(constructorTokenGenerator.isValid(mockMethod)).toBe(false);
    });
  });

  describe("generate", () => {
    it("throws for non-constructor items", () => {
      const mockMethod = {
        kind: ApiItemKind.Method,
        displayName: "method",
      } as unknown as ApiConstructor;

      expect(() => constructorTokenGenerator.generate(mockMethod)).toThrow(
        "Invalid item method of kind Method for Constructor token generator.",
      );
    });

    it("generates tokens for a simple constructor", () => {
      const mockConstructor = createMockConstructor();
      const { tokens } = constructorTokenGenerator.generate(mockConstructor, false);
      const values = tokens.map((t) => t.Value);

      expect(values).toEqual(["constructor", "(", ")", ";"]);
    });

    it("generates tokens for a protected constructor", () => {
      const mockConstructor = createMockConstructor({ isProtected: true });
      const { tokens } = constructorTokenGenerator.generate(mockConstructor, false);

      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "protected",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[1].Value).toBe("constructor");
    });

    it("generates constructor parameters", () => {
      const mockConstructor = createMockConstructor({
        parameters: [
          createMockParameter("name", "string"),
          createMockParameter("options", "ClientOptions", true),
        ],
      });

      const { tokens } = constructorTokenGenerator.generate(mockConstructor, false);
      const values = tokens.map((t) => t.Value).join(" ");

      expect(values).toContain("constructor");
      expect(values).toContain("name");
      expect(values).toContain("string");
      expect(values).toContain("options");
      expect(values).toContain("?");
      expect(values).toContain("ClientOptions");
      expect(tokens[tokens.length - 1].Value).toBe(";");
    });

    it("preserves parameter property modifiers", () => {
      const mockConstructor = createMockConstructor({
        excerptText:
          "constructor(public readonly endpoint: string, private credential: TokenCredential)",
        parameters: [
          createMockParameter("endpoint", "string"),
          createMockParameter("credential", "TokenCredential"),
        ],
      });

      const { tokens } = constructorTokenGenerator.generate(mockConstructor, false);
      const values = tokens.map((t) => t.Value).join(" ");

      expect(values).toContain("public readonly endpoint : string");
      expect(values).toContain("private credential : TokenCredential");
    });

    it("renders constructor overloads as independent signatures", () => {
      const overload1 = createMockConstructor({
        overloadIndex: 1,
        excerptText: "constructor(connectionString: string)",
        parameters: [createMockParameter("connectionString", "string")],
      });
      const overload2 = createMockConstructor({
        overloadIndex: 2,
        excerptText: "constructor(url: string, credential: TokenCredential)",
        parameters: [
          createMockParameter("url", "string"),
          createMockParameter("credential", "TokenCredential"),
        ],
      });

      const tokens1 = constructorTokenGenerator.generate(overload1, false).tokens;
      const tokens2 = constructorTokenGenerator.generate(overload2, false).tokens;

      expect(tokens1.map((t) => t.Value).join(" ")).toContain("connectionString : string");
      expect(tokens2.map((t) => t.Value).join(" ")).toContain(
        "url : string , credential : TokenCredential",
      );
      expect(tokens1[tokens1.length - 1].Value).toBe(";");
      expect(tokens2[tokens2.length - 1].Value).toBe(";");
    });

    it("marks tokens as deprecated", () => {
      const mockConstructor = createMockConstructor();
      const { tokens } = constructorTokenGenerator.generate(mockConstructor, true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });
  });
});