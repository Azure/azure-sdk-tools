import { describe, expect, it } from "vitest";
import { functionTokenGenerator } from "../../src/tokenGenerators/function";
import { ApiFunction, ApiItem, ApiItemKind, ExcerptToken, ExcerptTokenKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

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
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "simpleFunction",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[],
      } as ApiFunction;

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
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "oldFunction",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[],
      } as ApiFunction;

      const tokens = functionTokenGenerator.generate(mockFunction, true);

      expect(tokens[0].IsDeprecated).toBe(true);
      expect(tokens[1].IsDeprecated).toBe(true);
      expect(tokens[2].IsDeprecated).toBe(true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("generates correct tokens for a function with parameters", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "addNumbers",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(a: " },
          { kind: ExcerptTokenKind.Content, text: "number" },
          { kind: ExcerptTokenKind.Content, text: ", b: " },
          { kind: ExcerptTokenKind.Content, text: "number" },
          { kind: ExcerptTokenKind.Content, text: "): " },
          { kind: ExcerptTokenKind.Content, text: "number" },
        ] as ExcerptToken[],
      } as ApiFunction;

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
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "processUser",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(user: " },
          { 
            kind: ExcerptTokenKind.Reference, 
            text: "User",
            canonicalReference: {
              toString: () => "@azure/test!User:interface"
            }
          },
          { kind: ExcerptTokenKind.Content, text: "): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[],
      } as ApiFunction;

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const typeNameToken = tokens.find((t) => t.Kind === TokenKind.TypeName && t.Value === "User");
      expect(typeNameToken).toBeDefined();
      expect(typeNameToken?.NavigateToId).toBe("@azure/test!User:interface");
    });

    it("generates correct tokens for a function with return type reference", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "getUser",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(id: " },
          { kind: ExcerptTokenKind.Content, text: "string" },
          { kind: ExcerptTokenKind.Content, text: "): " },
          { 
            kind: ExcerptTokenKind.Reference, 
            text: "Promise<User>",
            canonicalReference: {
              toString: () => "!Promise:interface"
            }
          },
        ] as ExcerptToken[],
      } as ApiFunction;

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const returnTypeToken = tokens.find((t) => t.Kind === TokenKind.TypeName && t.Value === "Promise<User>");
      expect(returnTypeToken).toBeDefined();
      expect(returnTypeToken?.NavigateToId).toBe("!Promise:interface");
    });

    it("skips empty excerpt tokens", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "testFunc",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "" },
          { kind: ExcerptTokenKind.Content, text: "   " },
          { kind: ExcerptTokenKind.Content, text: "(): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[],
      } as ApiFunction;

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
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "myFunc",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[],
      } as ApiFunction;

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
        const mockFunction = {
          kind: ApiItemKind.Function,
          displayName,
          excerptTokens: [
            { kind: ExcerptTokenKind.Content, text: "(): " },
            { kind: ExcerptTokenKind.Content, text: "void" },
          ] as ExcerptToken[],
        } as ApiFunction;

        const tokens = functionTokenGenerator.generate(mockFunction, false);
        expect(tokens[2].Value).toBe(displayName);
      });
    });

    it("handles functions with optional parameters", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "optionalFunc",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "(required: " },
          { kind: ExcerptTokenKind.Content, text: "string" },
          { kind: ExcerptTokenKind.Content, text: ", optional?: " },
          { kind: ExcerptTokenKind.Content, text: "number" },
          { kind: ExcerptTokenKind.Content, text: "): " },
          { kind: ExcerptTokenKind.Content, text: "void" },
        ] as ExcerptToken[],
      } as ApiFunction;

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value).join("");
      expect(tokenValues).toContain("optional?:");
    });

    it("handles functions with generic type parameters", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "genericFunc",
        excerptTokens: [
          { kind: ExcerptTokenKind.Content, text: "<T>(value: T): T" },
        ] as ExcerptToken[],
      } as ApiFunction;

      const tokens = functionTokenGenerator.generate(mockFunction, false);

      const tokenValues = tokens.map((t) => t.Value).join("");
      expect(tokenValues).toContain("<T>");
      expect(tokenValues).toContain("value: T");
    });
  });
});
