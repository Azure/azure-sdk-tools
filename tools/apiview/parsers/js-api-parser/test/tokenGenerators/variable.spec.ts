import { describe, expect, it } from "vitest";
import { variableTokenGenerator } from "../../src/tokenGenerators/variable";
import {
  ApiVariable,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiVariable with all required properties
function createMockVariable(
  displayName: string,
  typeExcerptText: string,
  excerptTokens: ExcerptToken[],
  initializerText?: string,
): ApiVariable {
  const mock: any = {
    kind: ApiItemKind.Variable,
    displayName,
    excerptTokens,
    canonicalReference: {
      toString: () => `@test!${displayName}:var`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    fileUrlPath: undefined,
    excerpt: {
      text: excerptTokens.map((t) => t.text).join(""),
      tokenRange: { startIndex: 0, endIndex: excerptTokens.length },
      tokens: excerptTokens,
    },
    variableTypeExcerpt: {
      text: typeExcerptText,
      spannedTokens: [{ kind: ExcerptTokenKind.Content, text: typeExcerptText }],
    },
    initializerExcerpt: initializerText ? { text: initializerText } : undefined,
    releaseTag: undefined,
    tsdocComment: undefined,
  };
  return mock as ApiVariable;
}

function createBasicExcerptTokens(varName: string, typeValue: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: "export " },
    { kind: ExcerptTokenKind.Content, text: "const " },
    { kind: ExcerptTokenKind.Content, text: varName },
    { kind: ExcerptTokenKind.Content, text: ": " },
    { kind: ExcerptTokenKind.Content, text: typeValue },
  ] as ExcerptToken[];
}

function createDefaultExportExcerptTokens(varName: string, typeValue: string): ExcerptToken[] {
  return [
    { kind: ExcerptTokenKind.Content, text: "export default " },
    { kind: ExcerptTokenKind.Content, text: "const " },
    { kind: ExcerptTokenKind.Content, text: varName },
    { kind: ExcerptTokenKind.Content, text: ": " },
    { kind: ExcerptTokenKind.Content, text: typeValue },
  ] as ExcerptToken[];
}

describe("variableTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for variable items", () => {
      const mockVariable = {
        kind: ApiItemKind.Variable,
        displayName: "testVariable",
      } as ApiVariable;
      expect(variableTokenGenerator.isValid(mockVariable)).toBe(true);
    });

    it.each([
      [ApiItemKind.Enum, "TestEnum"],
      [ApiItemKind.Function, "testFunction"],
      [ApiItemKind.TypeAlias, "TestType"],
      [ApiItemKind.Class, "TestClass"],
    ])("returns false for %s items", (kind, name) => {
      const mockItem = { kind, displayName: name } as ApiItem;
      expect(variableTokenGenerator.isValid(mockItem)).toBe(false);
    });
  });

  describe("generate", () => {
    it("generates correct tokens for a simple variable", () => {
      const mockVariable = createMockVariable(
        "myString",
        "string",
        createBasicExcerptTokens("myString", "string"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      expect(tokens[0]).toMatchObject({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
      });
      expect(tokens[1]).toMatchObject({
        Kind: TokenKind.Keyword,
        Value: "const",
        HasSuffixSpace: true,
      });
      expect(tokens[2]).toMatchObject({
        Kind: TokenKind.MemberName,
        Value: "myString",
        NavigateToId: "@test!myString:var",
        NavigationDisplayName: "myString",
        RenderClasses: ["variable"],
      });
      expect(tokens[3]).toMatchObject({
        Kind: TokenKind.Punctuation,
        Value: ":",
        HasSuffixSpace: true,
      });
    });

    it("marks all tokens as deprecated when deprecated flag is true", () => {
      const mockVariable = createMockVariable(
        "deprecatedVar",
        "number",
        createBasicExcerptTokens("deprecatedVar", "number"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, true);

      tokens.slice(0, 4).forEach((token) => {
        expect(token.IsDeprecated).toBe(true);
      });
    });

    it("generates correct tokens for default export", () => {
      const mockVariable = createMockVariable(
        "defaultVar",
        "string",
        createDefaultExportExcerptTokens("defaultVar", "string"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      expect(tokens[0]).toMatchObject({ Kind: TokenKind.Keyword, Value: "export" });
      expect(tokens[1]).toMatchObject({ Kind: TokenKind.Keyword, Value: "default" });
      expect(tokens[2]).toMatchObject({ Kind: TokenKind.Keyword, Value: "const" });
      expect(tokens[3]).toMatchObject({ Kind: TokenKind.MemberName, Value: "defaultVar" });
    });

    it("handles deprecated default export", () => {
      const mockVariable = createMockVariable(
        "deprecatedDefaultVar",
        "string",
        createDefaultExportExcerptTokens("deprecatedDefaultVar", "string"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, true);

      expect(tokens.slice(0, 4).every((t) => t.IsDeprecated)).toBe(true);
    });

    it.each([
      ["string", "string"],
      ["number", "number"],
      ["boolean", "boolean"],
      ["string[]", "string[]"],
      ["Promise<string>", "Promise<string>"],
      ["Record<string, number>", "Record<string, number>"],
      ["readonly string[]", "readonly string[]"],
      ["MyClient", "MyClient"],
      ["Map<string, Promise<Array<number>>>", "Map<string, Promise<Array<number>>>"],
      ["[string, number, boolean]", "[string, number, boolean]"],
      ["typeof SomeClass", "typeof SomeClass"],
    ])("handles %s type", (typeText) => {
      const mockVariable = createMockVariable(
        "testVar",
        typeText,
        createBasicExcerptTokens("testVar", typeText),
      );
      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      expect(tokens.length).toBeGreaterThan(0);
      expect(tokens[2]).toMatchObject({ Kind: TokenKind.MemberName, Value: "testVar" });
    });

    it("handles union types with pipe operators", () => {
      const mockVariable = createMockVariable(
        "value",
        "string | number | null",
        createBasicExcerptTokens("value", "string | number | null"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);
      const pipeTokens = tokens.filter((t) => t.Value === "|");
      expect(pipeTokens.length).toBe(2);
    });

    it("handles intersection types with ampersand operators", () => {
      const mockVariable = createMockVariable(
        "intersected",
        "TypeA & TypeB & TypeC",
        createBasicExcerptTokens("intersected", "TypeA & TypeB & TypeC"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);
      const ampTokens = tokens.filter((t) => t.Value === "&");
      expect(ampTokens.length).toBe(2);
    });

    it("handles object type with children", () => {
      const mockVariable = createMockVariable(
        "config",
        "{ name: string; value: number; }",
        createBasicExcerptTokens("config", "{ name: string; value: number; }"),
      );

      const result = variableTokenGenerator.generate(mockVariable, false);
      const openBrace = result.tokens.find((t) => t.Value === "{");
      expect(openBrace).toBeDefined();
    });

    it("throws an error for invalid item kind", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
        excerptTokens: [],
      } as any;
      expect(() => variableTokenGenerator.generate(mockClass, false)).toThrow(
        "Invalid item TestClass of kind Class for Variable token generator.",
      );
    });

    it("includes initializer value when present", () => {
      const mockVariable = createMockVariable(
        "MAX_SIZE",
        "number",
        createBasicExcerptTokens("MAX_SIZE", "number"),
        "1000000",
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      const equalsToken = tokens.find((t) => t.Value === "=");
      expect(equalsToken).toBeDefined();
      expect(equalsToken).toMatchObject({
        Kind: TokenKind.Punctuation,
        Value: "=",
        HasPrefixSpace: true,
        HasSuffixSpace: true,
      });

      const valueToken = tokens.find((t) => t.Value === "1000000");
      expect(valueToken).toBeDefined();
      expect(valueToken).toMatchObject({
        Kind: TokenKind.StringLiteral,
        Value: "1000000",
      });
    });

    it("includes string initializer value", () => {
      const mockVariable = createMockVariable(
        "DEFAULT_NAME",
        "string",
        createBasicExcerptTokens("DEFAULT_NAME", "string"),
        '"hello"',
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      const valueToken = tokens.find((t) => t.Value === '"hello"');
      expect(valueToken).toBeDefined();
    });

    it("does not include initializer tokens when no initializer", () => {
      const mockVariable = createMockVariable(
        "myVar",
        "string",
        createBasicExcerptTokens("myVar", "string"),
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      const equalsToken = tokens.find((t) => t.Value === "=");
      expect(equalsToken).toBeUndefined();
    });

    it("does not emit colon when variable has no type annotation", () => {
      const mockVariable = createMockVariable(
        "AI_OPERATION_NAME",
        "",
        createBasicExcerptTokens("AI_OPERATION_NAME", ""),
        '"ai.operation.name"',
      );

      const { tokens } = variableTokenGenerator.generate(mockVariable, false);

      const colonToken = tokens.find((t) => t.Value === ":");
      expect(colonToken).toBeUndefined();

      const equalsToken = tokens.find((t) => t.Value === "=");
      expect(equalsToken).toBeDefined();

      const valueToken = tokens.find((t) => t.Value === '"ai.operation.name"');
      expect(valueToken).toBeDefined();
    });
  });
});
