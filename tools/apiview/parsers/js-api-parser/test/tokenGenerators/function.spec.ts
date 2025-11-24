import { describe, expect, it } from "vitest";
import { functionTokenGenerator } from "../../src/tokenGenerators/function";
import {
  ApiFunction,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiFunction with all required properties
function createMockFunction(displayName: string, excerptTokens: ExcerptToken[]): ApiFunction {
  const mock: any = {
    kind: ApiItemKind.Function,
    displayName,
    excerptTokens,
    // Required properties from ApiItem
    canonicalReference: {
      toString: () => `@test!${displayName}:function`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    // Required properties from ApiDeclaredItem
    fileUrlPath: undefined,
    excerpt: {
      text: excerptTokens.map((t) => t.text).join(""),
      tokenRange: { startIndex: 0, endIndex: excerptTokens.length },
      tokens: excerptTokens,
    },
    // Additional required properties
    releaseTag: undefined,
    tsdocComment: undefined,
    docComment: undefined,
  };

  // Add methods that reference 'this'
  mock.getExcerpt = function () {
    return this.excerpt;
  };
  mock.buildCanonicalReference = function () {
    return this.canonicalReference;
  };
  mock.getScopedNameWithinPackage = () => displayName;
  mock.getHierarchy = () => [];
  mock.getMergedSiblings = () => [];

  return mock as ApiFunction;
}

describe("functionTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for function items", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "testFunction",
      } as ApiFunction;

      expect(functionTokenGenerator.isValid(mockFunction)).toBe(true);
    });

    it("returns false for non-function items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as ApiItem;

      expect(functionTokenGenerator.isValid(mockInterface)).toBe(false);
    });

    it("returns false for method items", () => {
      const mockMethod = {
        kind: ApiItemKind.Method,
        displayName: "testMethod",
      } as ApiItem;

      expect(functionTokenGenerator.isValid(mockMethod)).toBe(false);
    });

    it("returns false for class items", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
      } as ApiItem;

      expect(functionTokenGenerator.isValid(mockClass)).toBe(false);
    });
  });

  describe("generate", () => {
    it("generates correct tokens for a simple function with no parameters", () => {
      const mockFunction = createMockFunction("simpleFunction", [
        { kind: ExcerptTokenKind.Content, text: "(): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        IsDeprecated: false,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "function",
        HasSuffixSpace: true,
        IsDeprecated: false,
      });
      expect(tokens[2]).toEqual({
        Kind: TokenKind.MemberName,
        Value: "simpleFunction",
        IsDeprecated: false,
      });
    });

    it("generates correct tokens for a deprecated function", () => {
      const mockFunction = createMockFunction("oldFunction", [
        { kind: ExcerptTokenKind.Content, text: "(): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, true);

      expect(tokens[0].IsDeprecated).toBe(true);
      expect(tokens[1].IsDeprecated).toBe(true);
      expect(tokens[2].IsDeprecated).toBe(true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("generates correct tokens for a function with parameters", () => {
      const mockFunction = createMockFunction("addNumbers", [
        { kind: ExcerptTokenKind.Content, text: "(a: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: ", b: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "number" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      expect(tokens.length).toBeGreaterThan(3);
      expect(tokens[0].Value).toBe("export");
      expect(tokens[1].Value).toBe("function");
      expect(tokens[2].Value).toBe("addNumbers");

      // Check that parameter tokens are included
      const tokenValues = tokens.map((t) => t.Value).join("");
      expect(tokenValues).toContain("a:");
      expect(tokenValues).toContain("number");
      expect(tokenValues).toContain("b:");
    });

    it("generates TypeName tokens with navigation for type references", () => {
      const mockFunction = createMockFunction("processUser", [
        { kind: ExcerptTokenKind.Content, text: "(user: " },
        {
          kind: ExcerptTokenKind.Reference,
          text: "User",
          canonicalReference: {
            toString: () => "@azure/test!User:interface",
          },
        },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const typeNameToken = tokens.find((t) => t.Kind === TokenKind.TypeName && t.Value === "User");
      expect(typeNameToken).toBeDefined();
      expect(typeNameToken?.NavigateToId).toBe("@azure/test!User:interface");
    });

    it("generates correct tokens for a function with return type reference", () => {
      const mockFunction = createMockFunction("getUser", [
        { kind: ExcerptTokenKind.Content, text: "(id: " },
        { kind: ExcerptTokenKind.Content, text: "string" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        {
          kind: ExcerptTokenKind.Reference,
          text: "Promise<User>",
          canonicalReference: {
            toString: () => "!Promise:interface",
          },
        },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const returnTypeToken = tokens.find(
        (t) => t.Kind === TokenKind.TypeName && t.Value === "Promise<User>",
      );
      expect(returnTypeToken).toBeDefined();
      expect(returnTypeToken?.NavigateToId).toBe("!Promise:interface");
    });

    it("skips empty excerpt tokens", () => {
      const mockFunction = createMockFunction("testFunc", [
        { kind: ExcerptTokenKind.Content, text: "" },
        { kind: ExcerptTokenKind.Content, text: "   " },
        { kind: ExcerptTokenKind.Content, text: "(): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      // Should not include tokens for empty or whitespace-only excerpt text
      const textTokens = tokens.slice(3); // Skip export, function, name
      expect(textTokens.every((t) => t.Value.trim().length > 0)).toBe(true);
    });

    it("throws error for non-function items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
        excerptTokens: [],
      } as unknown as ApiFunction;

      expect(() => functionTokenGenerator.generate(mockInterface, false)).toThrow(
        "Invalid item TestInterface of kind Interface for Function token generator.",
      );
    });

    it("generates tokens with correct spacing for keywords", () => {
      const mockFunction = createMockFunction("myFunc", [
        { kind: ExcerptTokenKind.Content, text: "(): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      expect(tokens[0].HasSuffixSpace).toBe(true); // export
      expect(tokens[1].HasSuffixSpace).toBe(true); // function
      expect(tokens[2].HasSuffixSpace).toBeUndefined(); // function name
    });

    it("preserves function display name exactly", () => {
      const testCases = [
        "simpleFunction",
        "camelCaseFunc",
        "snake_case_func",
        "PascalCaseFunc",
        "func123",
        "ɵInternalFunction",
      ];

      testCases.forEach((displayName) => {
        const mockFunction = createMockFunction(displayName, [
          { kind: ExcerptTokenKind.Content, text: "(): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[]);

        const tokens = functionTokenGenerator.generate(mockFunction, false);
        expect(tokens[2].Value).toBe(displayName);
      });
    });

    it("handles functions with optional parameters", () => {
      const mockFunction = createMockFunction("optionalFunc", [
        { kind: ExcerptTokenKind.Content, text: "(required: " },
        { kind: ExcerptTokenKind.Content, text: "string" },
        { kind: ExcerptTokenKind.Content, text: ", optional?: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value).join("");
      expect(tokenValues).toContain("optional?:");
    });

    it("handles functions with generic type parameters", () => {
      const mockFunction = createMockFunction("genericFunc", [
        { kind: ExcerptTokenKind.Content, text: "<T>(value: T): T" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value).join("");
      expect(tokenValues).toContain("<T>");
      expect(tokenValues).toContain("value: T");
    });

    it("generates exact token structure for parameters", () => {
      const mockFunction = createMockFunction("addNumbers", [
        { kind: ExcerptTokenKind.Content, text: "(a: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: ", b: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "number" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      // Verify the complete token sequence
      expect(tokens[0]).toMatchObject({ Kind: TokenKind.Keyword, Value: "export" });
      expect(tokens[1]).toMatchObject({ Kind: TokenKind.Keyword, Value: "function" });
      expect(tokens[2]).toMatchObject({ Kind: TokenKind.MemberName, Value: "addNumbers" });
      expect(tokens[3]).toMatchObject({ Kind: TokenKind.Text, Value: "(a:" });
      expect(tokens[4]).toMatchObject({ Kind: TokenKind.Text, Value: "number" });
      expect(tokens[5]).toMatchObject({ Kind: TokenKind.Text, Value: ", b:" });
      expect(tokens[6]).toMatchObject({ Kind: TokenKind.Text, Value: "number" });
      expect(tokens[7]).toMatchObject({ Kind: TokenKind.Text, Value: "):" });
      expect(tokens[8]).toMatchObject({ Kind: TokenKind.Text, Value: "number" });
    });

    it("generates TypeName tokens for parameter type references", () => {
      const mockFunction = createMockFunction("processData", [
        { kind: ExcerptTokenKind.Content, text: "(input: " },
        {
          kind: ExcerptTokenKind.Reference,
          text: "InputData",
          canonicalReference: {
            toString: () => "@azure/test!InputData:interface",
          },
        },
        { kind: ExcerptTokenKind.Content, text: ", options?: " },
        {
          kind: ExcerptTokenKind.Reference,
          text: "ProcessOptions",
          canonicalReference: {
            toString: () => "@azure/test!ProcessOptions:interface",
          },
        },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      // Find parameter type tokens
      const inputDataToken = tokens.find((t) => t.Value === "InputData");
      expect(inputDataToken).toBeDefined();
      expect(inputDataToken?.Kind).toBe(TokenKind.TypeName);
      expect(inputDataToken?.NavigateToId).toBe("@azure/test!InputData:interface");

      const processOptionsToken = tokens.find((t) => t.Value === "ProcessOptions");
      expect(processOptionsToken).toBeDefined();
      expect(processOptionsToken?.Kind).toBe(TokenKind.TypeName);
      expect(processOptionsToken?.NavigateToId).toBe("@azure/test!ProcessOptions:interface");

      // Parameter names and punctuation should be Text tokens
      const textTokens = tokens.filter((t) => t.Kind === TokenKind.Text);
      expect(textTokens.some((t) => t.Value === "(input:")).toBe(true);
      expect(textTokens.some((t) => t.Value === ", options?:")).toBe(true);
    });

    it("handles rest parameters correctly", () => {
      const mockFunction = createMockFunction("concat", [
        { kind: ExcerptTokenKind.Content, text: "(...items: " },
        { kind: ExcerptTokenKind.Content, text: "string" },
        { kind: ExcerptTokenKind.Content, text: "[]): " },
        { kind: ExcerptTokenKind.Content, text: "string" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("(...items:");
      expect(tokenValues).toContain("string");
      expect(tokenValues).toContain("[]):");

      // All parameter-related tokens should be Text type
      const restParamToken = tokens.find((t) => t.Value === "(...items:");
      expect(restParamToken?.Kind).toBe(TokenKind.Text);
    });

    it("handles destructured parameters", () => {
      const mockFunction = createMockFunction("destructure", [
        { kind: ExcerptTokenKind.Content, text: "({ a, b }: " },
        { kind: ExcerptTokenKind.Content, text: "{ a: number; b: number }" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "number" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("({ a, b }:");
      expect(tokenValues).toContain("{ a: number; b: number }");
    });

    it("handles parameters with default values", () => {
      const mockFunction = createMockFunction("withDefaults", [
        { kind: ExcerptTokenKind.Content, text: "(x: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: " = 10, y: " },
        { kind: ExcerptTokenKind.Content, text: "string" },
        { kind: ExcerptTokenKind.Content, text: ' = "hello"): ' },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("(x:");
      expect(tokenValues).toContain("= 10, y:");
      expect(tokenValues).toContain('= "hello"):');
    });

    it("handles complex parameter types with generics", () => {
      const mockFunction = createMockFunction("complex", [
        { kind: ExcerptTokenKind.Content, text: "(data: " },
        {
          kind: ExcerptTokenKind.Reference,
          text: "Array<Record<string, unknown>>",
          canonicalReference: {
            toString: () => "!Array:interface",
          },
        },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const arrayToken = tokens.find((t) => t.Value === "Array<Record<string, unknown>>");
      expect(arrayToken).toBeDefined();
      expect(arrayToken?.Kind).toBe(TokenKind.TypeName);
      expect(arrayToken?.NavigateToId).toBe("!Array:interface");
    });

    it("handles union type parameters", () => {
      const mockFunction = createMockFunction("unionParam", [
        { kind: ExcerptTokenKind.Content, text: "(value: " },
        { kind: ExcerptTokenKind.Content, text: "string | number | boolean" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const unionTypeToken = tokens.find((t) => t.Value === "string | number | boolean");
      expect(unionTypeToken).toBeDefined();
      expect(unionTypeToken?.Kind).toBe(TokenKind.Text);
    });

    it("handles function type parameters", () => {
      const mockFunction = createMockFunction("callback", [
        { kind: ExcerptTokenKind.Content, text: "(fn: " },
        { kind: ExcerptTokenKind.Content, text: "(x: number) => string" },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const fnTypeToken = tokens.find((t) => t.Value === "(x: number) => string");
      expect(fnTypeToken).toBeDefined();
      expect(fnTypeToken?.Kind).toBe(TokenKind.Text);
    });

    it("verifies all parameter tokens have correct deprecation flag", () => {
      const mockFunction = createMockFunction("deprecatedFunc", [
        { kind: ExcerptTokenKind.Content, text: "(a: " },
        { kind: ExcerptTokenKind.Content, text: "number" },
        { kind: ExcerptTokenKind.Content, text: ", b: " },
        {
          kind: ExcerptTokenKind.Reference,
          text: "CustomType",
          canonicalReference: {
            toString: () => "@test!CustomType:type",
          },
        },
        { kind: ExcerptTokenKind.Content, text: "): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const tokens = functionTokenGenerator.generate(mockFunction, true);

      // All tokens should be deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);

      // Verify specific parameter tokens are deprecated
      const customTypeToken = tokens.find((t) => t.Value === "CustomType");
      expect(customTypeToken?.IsDeprecated).toBe(true);
      expect(customTypeToken?.Kind).toBe(TokenKind.TypeName);
    });
  });
});
