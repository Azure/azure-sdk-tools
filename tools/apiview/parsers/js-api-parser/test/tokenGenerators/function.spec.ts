import { describe, expect, it } from "vitest";
import { functionTokenGenerator } from "../../src/tokenGenerators/function";
import {
  ApiFunction,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
  Parameter,
  TypeParameter,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiFunction with all required properties
function createMockFunction(
  displayName: string,
  excerptTokens: ExcerptToken[],
  parameters?: Parameter[],
  typeParameters?: TypeParameter[],
): ApiFunction {
  const mock: any = {
    kind: ApiItemKind.Function,
    displayName,
    excerptTokens,
    parameters: parameters || [],
    typeParameters: typeParameters || [],
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
    // Return type excerpt (defaults to void if not provided)
    returnTypeExcerpt: {
      text: "void",
      spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "void" }],
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

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasPrefixSpace: false,
        HasSuffixSpace: true,
        IsDeprecated: false,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "function",
        HasPrefixSpace: false,
        HasSuffixSpace: true,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
      expect(tokens[2]).toEqual({
        Kind: TokenKind.MemberName,
        Value: "simpleFunction",
        HasPrefixSpace: false,
        HasSuffixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
    });

    it("generates correct tokens for a deprecated function", () => {
      const mockFunction = createMockFunction("oldFunction", [
        { kind: ExcerptTokenKind.Content, text: "(): " },
        { kind: ExcerptTokenKind.Content, text: "void" },
      ] as ExcerptToken[]);

      const { tokens } = functionTokenGenerator.generate(mockFunction, true);

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
      // Update return type
      mockFunction.returnTypeExcerpt = {
        text: "number",
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
      } as any;

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      expect(tokens.length).toBeGreaterThan(3);
      expect(tokens[0].Value).toBe("export");
      expect(tokens[1].Value).toBe("function");
      expect(tokens[2].Value).toBe("addNumbers");

      // Check that parameter tokens are included
      const tokenValues = tokens.map((t) => t.Value).join("");
      expect(tokenValues).toContain("a");
      expect(tokenValues).toContain(":");
      expect(tokenValues).toContain("number");
      expect(tokenValues).toContain("b");
    });

    it("generates TypeName tokens with navigation for type references", () => {
      const mockFunction = createMockFunction(
        "processUser",
        [],
        [
          {
            name: "user",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "User",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "User",
                  canonicalReference: {
                    toString: () => "@azure/test!User:interface",
                  },
                } as ExcerptToken,
              ],
            } as any,
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const typeNameToken = tokens.find((t) => t.Kind === TokenKind.TypeName && t.Value === "User");
      expect(typeNameToken).toBeDefined();
      expect(typeNameToken?.NavigateToId).toBe("@azure/test!User:interface");
    });

    it("generates correct tokens for a function with return type reference", () => {
      const mockFunction = createMockFunction(
        "getUser",
        [],
        [
          {
            name: "id",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string" }],
            },
          } as unknown as Parameter,
        ],
      );

      // Update return type
      mockFunction.returnTypeExcerpt = {
        text: "Promise<User>",
        spannedTokens: [
          {
            kind: ExcerptTokenKind.Reference,
            text: "Promise<User>",
            canonicalReference: {
              toString: () => "!Promise:interface",
            },
          } as ExcerptToken,
        ],
      } as any;

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

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

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

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

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      expect(tokens[0].HasSuffixSpace).toBe(true); // export
      expect(tokens[1].HasSuffixSpace).toBe(true); // function
      expect(tokens[2].HasSuffixSpace).toBe(false); // function name
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

        const { tokens } = functionTokenGenerator.generate(mockFunction, false);
        expect(tokens[2].Value).toBe(displayName);
      });
    });

    it("handles functions with optional parameters", () => {
      const mockFunction = createMockFunction(
        "optionalFunc",
        [],
        [
          {
            name: "required",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string" }],
            },
          } as unknown as Parameter,
          {
            name: "optional",
            isOptional: true,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("optional");
      expect(tokenValues).toContain("?");
    });

    it("handles functions with generic type parameters", () => {
      const mockFunction = createMockFunction(
        "genericFunc",
        [],
        [
          {
            name: "value",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "T",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "T" }],
            },
          } as unknown as Parameter,
        ],
        [
          {
            name: "T",
            constraintExcerpt: {
              text: "",
              spannedTokens: [],
            },
          } as unknown as TypeParameter,
        ],
      );

      // Update return type
      mockFunction.returnTypeExcerpt = {
        text: "T",
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "T" }],
      } as any;

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("<");
      expect(tokenValues).toContain("T");
      expect(tokenValues).toContain(">");
      expect(tokenValues).toContain("value");
    });

    it("generates exact token structure for parameters", () => {
      const mockFunction = createMockFunction(
        "addNumbers",
        [],
        [
          {
            name: "a",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
          {
            name: "b",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
        ],
      );

      // Update return type
      mockFunction.returnTypeExcerpt = {
        text: "number",
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
      } as any;

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      // Verify the complete token sequence
      expect(tokens[0]).toMatchObject({ Kind: TokenKind.Keyword, Value: "export" });
      expect(tokens[1]).toMatchObject({ Kind: TokenKind.Keyword, Value: "function" });
      expect(tokens[2]).toMatchObject({ Kind: TokenKind.MemberName, Value: "addNumbers" });
      expect(tokens[3]).toMatchObject({ Kind: TokenKind.Punctuation, Value: "(" });
      expect(tokens[4]).toMatchObject({ Kind: TokenKind.Text, Value: "a" });
      expect(tokens[5]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[6]).toMatchObject({ Kind: TokenKind.Keyword, Value: "number" });
      expect(tokens[7]).toMatchObject({ Kind: TokenKind.Punctuation, Value: "," });
      expect(tokens[8]).toMatchObject({ Kind: TokenKind.Text, Value: "b" });
      expect(tokens[9]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[10]).toMatchObject({ Kind: TokenKind.Keyword, Value: "number" });
      expect(tokens[11]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ")" });
      expect(tokens[12]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[13]).toMatchObject({ Kind: TokenKind.Keyword, Value: "number" });
    });

    it("generates TypeName tokens for parameter type references", () => {
      const mockFunction = createMockFunction(
        "processData",
        [],
        [
          {
            name: "input",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "InputData",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "InputData",
                  canonicalReference: {
                    toString: () => "@azure/test!InputData:interface",
                  },
                } as ExcerptToken,
              ],
            } as any,
          } as unknown as Parameter,
          {
            name: "options",
            isOptional: true,
            parameterTypeExcerpt: {
              text: "ProcessOptions",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "ProcessOptions",
                  canonicalReference: {
                    toString: () => "@azure/test!ProcessOptions:interface",
                  },
                } as ExcerptToken,
              ],
            } as any,
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

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
      expect(textTokens.some((t) => t.Value === "input")).toBe(true);
      expect(textTokens.some((t) => t.Value === "options")).toBe(true);
    });

    it("handles rest parameters correctly", () => {
      const mockFunction = createMockFunction(
        "concat",
        [],
        [
          {
            name: "...items",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string[]",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string[]" }],
            },
          } as unknown as Parameter,
        ],
      );

      // Update return type
      mockFunction.returnTypeExcerpt = {
        text: "string",
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string" }],
      } as any;

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("...items");
      expect(tokenValues).toContain("string[]");

      // All parameter-related tokens should be Text type
      const restParamToken = tokens.find((t) => t.Value === "...items");
      expect(restParamToken?.Kind).toBe(TokenKind.Text);
    });

    it("handles destructured parameters", () => {
      const mockFunction = createMockFunction(
        "destructure",
        [],
        [
          {
            name: "{ a, b }",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "{ a: number; b: number }",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "{ a: number; b: number }" }],
            },
          } as unknown as Parameter,
        ],
      );

      // Update return type
      mockFunction.returnTypeExcerpt = {
        text: "number",
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
      } as any;

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("{ a, b }");
      expect(tokenValues).toContain("{ a: number; b: number }");
    });

    it("handles parameters with default values", () => {
      const mockFunction = createMockFunction(
        "withDefaults",
        [],
        [
          {
            name: "x",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
          {
            name: "y",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string" }],
            },
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("x");
      expect(tokenValues).toContain("number");
      expect(tokenValues).toContain("y");
      expect(tokenValues).toContain("string");
    });

    it("handles complex parameter types with generics", () => {
      const mockFunction = createMockFunction(
        "complex",
        [],
        [
          {
            name: "data",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "Array<Record<string, unknown>>",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "Array<Record<string, unknown>>",
                  canonicalReference: {
                    toString: () => "!Array:interface",
                  },
                } as ExcerptToken,
              ],
            } as any,
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const arrayToken = tokens.find((t) => t.Value === "Array<Record<string, unknown>>");
      expect(arrayToken).toBeDefined();
      expect(arrayToken?.Kind).toBe(TokenKind.TypeName);
      expect(arrayToken?.NavigateToId).toBe("!Array:interface");
    });

    it("handles union type parameters", () => {
      const mockFunction = createMockFunction(
        "unionParam",
        [],
        [
          {
            name: "value",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string | number | boolean",
              spannedTokens: [
                { kind: ExcerptTokenKind.Content, text: "string | number | boolean" },
              ],
            },
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const unionTypeToken = tokens.find((t) => t.Value === "string | number | boolean");
      expect(unionTypeToken).toBeDefined();
      expect(unionTypeToken?.Kind).toBe(TokenKind.Text);
    });

    it("handles function type parameters", () => {
      const mockFunction = createMockFunction(
        "callback",
        [],
        [
          {
            name: "fn",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "(x: number) => string",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "(x: number) => string" }],
            },
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const fnTypeToken = tokens.find((t) => t.Value === "(x: number) => string");
      expect(fnTypeToken).toBeDefined();
      expect(fnTypeToken?.Kind).toBe(TokenKind.Text);
    });

    it("verifies all parameter tokens have correct deprecation flag", () => {
      const mockFunction = createMockFunction(
        "deprecatedFunc",
        [],
        [
          {
            name: "a",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
          {
            name: "b",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "CustomType",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "CustomType",
                  canonicalReference: {
                    toString: () => "@test!CustomType:type",
                  },
                } as ExcerptToken,
              ],
            } as any,
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, true);

      // All tokens should be deprecated
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);

      // Verify specific parameter tokens are deprecated
      const customTypeToken = tokens.find((t) => t.Value === "CustomType");
      expect(customTypeToken?.IsDeprecated).toBe(true);
      expect(customTypeToken?.Kind).toBe(TokenKind.TypeName);
    });

    // Tests for structured parameters/typeParameters
    it("uses structured parameters when available", () => {
      const mockFunction = createMockFunction(
        "structuredFunc",
        [], // excerptTokens not used when parameters are available
        [
          {
            name: "input",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string" }],
            },
          } as unknown as Parameter,
          {
            name: "count",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      // Should have: export, function, name, (, input, :, string, ,, count, :, number, ), :, void
      expect(tokens[0]).toMatchObject({ Kind: TokenKind.Keyword, Value: "export" });
      expect(tokens[1]).toMatchObject({ Kind: TokenKind.Keyword, Value: "function" });
      expect(tokens[2]).toMatchObject({ Kind: TokenKind.MemberName, Value: "structuredFunc" });
      expect(tokens[3]).toMatchObject({ Kind: TokenKind.Punctuation, Value: "(" });
      expect(tokens[4]).toMatchObject({ Kind: TokenKind.Text, Value: "input" });
      expect(tokens[5]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[6]).toMatchObject({ Kind: TokenKind.Keyword, Value: "string" }); // Changed from Text to Keyword
      expect(tokens[7]).toMatchObject({ Kind: TokenKind.Punctuation, Value: "," });
      expect(tokens[8]).toMatchObject({ Kind: TokenKind.Text, Value: "count" });
      expect(tokens[9]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[10]).toMatchObject({ Kind: TokenKind.Keyword, Value: "number" }); // Changed from Text to Keyword
      expect(tokens[11]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ")" });
      expect(tokens[12]).toMatchObject({ Kind: TokenKind.Punctuation, Value: ":" });
      expect(tokens[13]).toMatchObject({ Kind: TokenKind.Keyword, Value: "void" }); // Changed from Text to Keyword
    });

    it("handles optional parameters with structured approach", () => {
      const mockFunction = createMockFunction(
        "optionalStructured",
        [],
        [
          {
            name: "required",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "string",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string" }],
            },
          } as unknown as Parameter,
          {
            name: "optional",
            isOptional: true,
            parameterTypeExcerpt: {
              text: "number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "number" }],
            },
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("required");
      expect(tokenValues).toContain("optional");
      expect(tokenValues).toContain("?");

      // Verify the optional marker is after the parameter name
      const optionalIndex = tokenValues.indexOf("optional");
      const questionIndex = tokenValues.indexOf("?");
      expect(questionIndex).toBe(optionalIndex + 1);
    });

    it("uses type parameters when available", () => {
      const mockFunction = createMockFunction(
        "genericStructured",
        [],
        [
          {
            name: "value",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "T",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "T" }],
            },
          } as unknown as Parameter,
        ],
        [
          {
            name: "T",
            constraintExcerpt: {
              text: "",
              spannedTokens: [],
            },
          } as unknown as TypeParameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("<");
      expect(tokenValues).toContain(">");

      // Find the type parameter
      const tToken = tokens.find((t) => t.Value === "T" && t.Kind === TokenKind.TypeName);
      expect(tToken).toBeDefined();
    });

    it("handles type parameter with constraint", () => {
      const mockFunction = createMockFunction(
        "constrainedGeneric",
        [],
        [
          {
            name: "value",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "T",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "T" }],
            },
          } as unknown as Parameter,
        ],
        [
          {
            name: "T",
            constraintExcerpt: {
              text: "string | number",
              spannedTokens: [{ kind: ExcerptTokenKind.Content, text: "string | number" }],
            },
          } as unknown as TypeParameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("T");
      expect(tokenValues).toContain("extends");
      expect(tokenValues).toContain("string | number");
    });

    it("handles parameter type references with structured approach", () => {
      const mockFunction = createMockFunction(
        "withTypeRef",
        [],
        [
          {
            name: "user",
            isOptional: false,
            parameterTypeExcerpt: {
              text: "User",
              spannedTokens: [
                {
                  kind: ExcerptTokenKind.Reference,
                  text: "User",
                  canonicalReference: {
                    toString: () => "@azure/test!User:interface",
                  } as any,
                } as ExcerptToken,
              ],
            } as any,
          } as unknown as Parameter,
        ],
      );

      const { tokens } = functionTokenGenerator.generate(mockFunction, false);

      const userTypeToken = tokens.find((t) => t.Value === "User" && t.Kind === TokenKind.TypeName);
      expect(userTypeToken).toBeDefined();
      expect(userTypeToken?.NavigateToId).toBe("@azure/test!User:interface");
    });
  });
});
