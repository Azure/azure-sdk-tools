import { describe, expect, it } from "vitest";
import { methodTokenGenerator } from "../../src/tokenGenerators/method";
import {
  ApiItem,
  ApiItemKind,
  ApiMethod,
  ApiMethodSignature,
  ExcerptToken,
  ExcerptTokenKind,
  Parameter,
  TypeParameter,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiMethod with all required properties
function createMockMethod(
  displayName: string,
  options: {
    isStatic?: boolean;
    isProtected?: boolean;
    isAbstract?: boolean;
    isOptional?: boolean;
    parameters?: Parameter[];
    typeParameters?: TypeParameter[];
    returnType?: string;
    returnTypeTokens?: ExcerptToken[];
  } = {},
): ApiMethod {
  const {
    isStatic = false,
    isProtected = false,
    isAbstract = false,
    isOptional = false,
    parameters = [],
    typeParameters = [],
    returnType = "void",
    returnTypeTokens,
  } = options;

  const mock: any = {
    kind: ApiItemKind.Method,
    displayName,
    excerptTokens: [],
    parameters,
    typeParameters,
    isStatic,
    isProtected,
    isAbstract,
    isOptional,
    // Required properties from ApiItem
    canonicalReference: {
      toString: () => `@test!TestClass#${displayName}:member`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    // Required properties from ApiDeclaredItem
    fileUrlPath: undefined,
    excerpt: {
      text: "",
      tokenRange: { startIndex: 0, endIndex: 0 },
      tokens: [],
    },
    // Return type excerpt
    returnTypeExcerpt: {
      text: returnType,
      spannedTokens: returnTypeTokens || [{ kind: ExcerptTokenKind.Content, text: returnType }],
    },
    // Additional required properties
    releaseTag: undefined,
    tsdocComment: undefined,
    overloadIndex: 1,
  };

  return mock as ApiMethod;
}

// Helper function to create a mock ApiMethodSignature with all required properties
function createMockMethodSignature(
  displayName: string,
  options: {
    isOptional?: boolean;
    parameters?: Parameter[];
    typeParameters?: TypeParameter[];
    returnType?: string;
    returnTypeTokens?: ExcerptToken[];
  } = {},
): ApiMethodSignature {
  const {
    isOptional = false,
    parameters = [],
    typeParameters = [],
    returnType = "void",
    returnTypeTokens,
  } = options;

  const mock: any = {
    kind: ApiItemKind.MethodSignature,
    displayName,
    excerptTokens: [],
    parameters,
    typeParameters,
    isOptional,
    // Required properties from ApiItem
    canonicalReference: {
      toString: () => `@test!TestInterface#${displayName}:member`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    // Required properties from ApiDeclaredItem
    fileUrlPath: undefined,
    excerpt: {
      text: "",
      tokenRange: { startIndex: 0, endIndex: 0 },
      tokens: [],
    },
    // Return type excerpt
    returnTypeExcerpt: {
      text: returnType,
      spannedTokens: returnTypeTokens || [{ kind: ExcerptTokenKind.Content, text: returnType }],
    },
    // Additional required properties
    releaseTag: undefined,
    tsdocComment: undefined,
    overloadIndex: 1,
  };

  return mock as ApiMethodSignature;
}

// Helper to create a mock Parameter
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

// Helper to create a mock TypeParameter
function createMockTypeParameter(
  name: string,
  constraintText?: string,
  defaultTypeText?: string,
): TypeParameter {
  const constraint = constraintText
    ? {
        text: constraintText,
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: constraintText }],
      }
    : { text: "", spannedTokens: [] };

  const defaultType = defaultTypeText
    ? {
        text: defaultTypeText,
        spannedTokens: [{ kind: ExcerptTokenKind.Content, text: defaultTypeText }],
      }
    : { text: "", spannedTokens: [] };

  return {
    name,
    constraintExcerpt: constraint,
    defaultTypeExcerpt: defaultType,
  } as unknown as TypeParameter;
}

// Helper to find token index by value
function findTokenIndex(tokens: { Value: string }[], value: string, startFrom = 0): number {
  return tokens.findIndex((t, i) => i >= startFrom && t.Value === value);
}

// Helper to assert token ordering
function assertTokenOrder(tokens: { Value: string }[], ...values: string[]): void {
  let lastIndex = -1;
  let lastValue = "(start)";
  for (const value of values) {
    const index = findTokenIndex(tokens, value, lastIndex + 1);
    expect(
      index,
      `Expected to find "${value}" after "${lastValue}" (index ${lastIndex}), but got index ${index}. Tokens: ${tokens.map((t) => t.Value).join(", ")}`,
    ).toBeGreaterThan(lastIndex);
    lastIndex = index;
    lastValue = value;
  }
}

describe("methodTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for method items", () => {
      const mockMethod = {
        kind: ApiItemKind.Method,
        displayName: "testMethod",
      } as ApiMethod;

      expect(methodTokenGenerator.isValid(mockMethod)).toBe(true);
    });

    it("returns true for method signature items", () => {
      const mockMethodSignature = {
        kind: ApiItemKind.MethodSignature,
        displayName: "testMethodSignature",
      } as ApiMethodSignature;

      expect(methodTokenGenerator.isValid(mockMethodSignature)).toBe(true);
    });

    it("returns false for function items", () => {
      const mockFunction = {
        kind: ApiItemKind.Function,
        displayName: "testFunction",
      } as ApiItem;

      expect(methodTokenGenerator.isValid(mockFunction)).toBe(false);
    });

    it("returns false for class items", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
      } as ApiItem;

      expect(methodTokenGenerator.isValid(mockClass)).toBe(false);
    });

    it("returns false for interface items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as ApiItem;

      expect(methodTokenGenerator.isValid(mockInterface)).toBe(false);
    });
  });

  describe("generate - ApiMethod", () => {
    it("generates tokens for a simple method with no parameters", () => {
      const mockMethod = createMockMethod("simpleMethod");

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "simpleMethod", "(", ")", ":", "void");
    });

    it("generates tokens for a deprecated method", () => {
      const mockMethod = createMockMethod("deprecatedMethod");

      const { tokens } = methodTokenGenerator.generate(mockMethod, true);

      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("generates tokens for a static method", () => {
      const mockMethod = createMockMethod("staticMethod", { isStatic: true });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "static", "staticMethod", "(", ")", ":", "void");
      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "static",
        HasSuffixSpace: true,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
    });

    it("generates tokens for a protected method", () => {
      const mockMethod = createMockMethod("protectedMethod", { isProtected: true });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "protected", "protectedMethod", "(", ")", ":", "void");
      expect(tokens.find((t) => t.Value === "protected")?.Kind).toBe(TokenKind.Keyword);
    });

    it("generates tokens for an abstract method", () => {
      const mockMethod = createMockMethod("abstractMethod", { isAbstract: true });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "abstract", "abstractMethod", "(", ")", ":", "void");
      expect(tokens.find((t) => t.Value === "abstract")?.Kind).toBe(TokenKind.Keyword);
    });

    it("generates tokens for a static protected abstract method", () => {
      const mockMethod = createMockMethod("complexMethod", {
        isStatic: true,
        isProtected: true,
        isAbstract: true,
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(
        tokens,
        "static",
        "protected",
        "abstract",
        "complexMethod",
        "(",
        ")", ":",
        "void",
      );
    });

    it("generates tokens for an optional method", () => {
      const mockMethod = createMockMethod("optionalMethod", { isOptional: true });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "optionalMethod", "?", "(", ")", ":", "void");
    });

    it("generates tokens for a method with parameters", () => {
      const mockMethod = createMockMethod("methodWithParams", {
        parameters: [createMockParameter("arg1", "string"), createMockParameter("arg2", "number")],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(
        tokens,
        "methodWithParams",
        "(",
        "arg1",
        ":",
        "string",
        ",",
        "arg2",
        ":",
        "number",
        ")", ":",
        "void",
      );
    });

    it("generates tokens for a method with optional parameters", () => {
      const mockMethod = createMockMethod("methodWithOptionalParam", {
        parameters: [createMockParameter("optionalArg", "string", true)],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(
        tokens,
        "methodWithOptionalParam",
        "(",
        "optionalArg",
        "?",
        ":",
        "string",
        ")", ":",
        "void",
      );
    });

    it("generates tokens for a method with type parameters", () => {
      const mockMethod = createMockMethod("genericMethod", {
        typeParameters: [createMockTypeParameter("T")],
        returnType: "T",
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "genericMethod", "<", "T", ">", "(", ")", ":", "T");
    });

    it("generates tokens for a method with constrained type parameters", () => {
      const mockMethod = createMockMethod("constrainedGenericMethod", {
        typeParameters: [createMockTypeParameter("T", "object")],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(
        tokens,
        "constrainedGenericMethod",
        "<",
        "T",
        "extends",
        "object",
        ">",
        "(",
        ")", ":",
      );
    });

    it("generates tokens for a method with default type parameters", () => {
      const mockMethod = createMockMethod("defaultGenericMethod", {
        typeParameters: [createMockTypeParameter("T", undefined, "string")],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "defaultGenericMethod", "<", "T", "=", "string", ">", "(", ")", ":");
    });

    it("generates tokens for a method with complex return type", () => {
      const mockMethod = createMockMethod("asyncMethod", {
        returnType: "Promise<void>",
        returnTypeTokens: [
          {
            kind: ExcerptTokenKind.Reference,
            text: "Promise",
            canonicalReference: { toString: () => "!Promise:interface" },
          } as ExcerptToken,
          { kind: ExcerptTokenKind.Content, text: "<void>" } as ExcerptToken,
        ],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      assertTokenOrder(tokens, "asyncMethod", "(", ")", ":", "Promise", "<void>");
    });

    it("throws error for invalid item kind", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
      } as unknown as ApiMethod;

      expect(() => methodTokenGenerator.generate(mockEnum)).toThrow(
        "Invalid item TestEnum of kind Enum for Method token generator.",
      );
    });
  });

  describe("generate - ApiMethodSignature", () => {
    it("generates tokens for a simple method signature with no parameters", () => {
      const mockMethodSig = createMockMethodSignature("simpleMethodSig");

      const { tokens } = methodTokenGenerator.generate(mockMethodSig, false);

      assertTokenOrder(tokens, "simpleMethodSig", "(", ")", ":", "void");
      // Method signatures should NOT have static/protected/abstract modifiers
      expect(tokens.some((t) => t.Value === "static")).toBe(false);
      expect(tokens.some((t) => t.Value === "protected")).toBe(false);
      expect(tokens.some((t) => t.Value === "abstract")).toBe(false);
    });

    it("generates tokens for a deprecated method signature", () => {
      const mockMethodSig = createMockMethodSignature("deprecatedMethodSig");

      const { tokens } = methodTokenGenerator.generate(mockMethodSig, true);

      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("generates tokens for an optional method signature", () => {
      const mockMethodSig = createMockMethodSignature("optionalMethodSig", { isOptional: true });

      const { tokens } = methodTokenGenerator.generate(mockMethodSig, false);

      assertTokenOrder(tokens, "optionalMethodSig", "?", "(", ")", ":", "void");
    });

    it("generates tokens for a method signature with parameters", () => {
      const mockMethodSig = createMockMethodSignature("methodSigWithParams", {
        parameters: [
          createMockParameter("arg1", "string"),
          createMockParameter("arg2", "number", true),
        ],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethodSig, false);

      assertTokenOrder(
        tokens,
        "methodSigWithParams",
        "(",
        "arg1",
        ":",
        "string",
        ",",
        "arg2",
        "?",
        ":",
        "number",
        ")", ":",
        "void",
      );
    });

    it("generates tokens for a method signature with type parameters", () => {
      const mockMethodSig = createMockMethodSignature("genericMethodSig", {
        typeParameters: [createMockTypeParameter("T", "object", "unknown")],
        returnType: "T",
      });

      const { tokens } = methodTokenGenerator.generate(mockMethodSig, false);

      assertTokenOrder(
        tokens,
        "genericMethodSig",
        "<",
        "T",
        "extends",
        "object",
        "=",
        "unknown",
        ">",
        "(",
        ")", ":",
        "T",
      );
    });

    it("generates tokens for a method signature with multiple type parameters", () => {
      const mockMethodSig = createMockMethodSignature("multiGenericMethodSig", {
        typeParameters: [createMockTypeParameter("T"), createMockTypeParameter("U")],
        returnType: "void",
      });

      const { tokens } = methodTokenGenerator.generate(mockMethodSig, false);

      assertTokenOrder(tokens, "multiGenericMethodSig", "<", "T", ",", "U", ">", "(", ")", ":", "void");
    });
  });

  describe("token structure", () => {
    it("uses MemberName token kind for method name", () => {
      const mockMethod = createMockMethod("testMethod");

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      const nameToken = tokens.find((t) => t.Value === "testMethod");
      expect(nameToken?.Kind).toBe(TokenKind.MemberName);
    });

    it("uses Keyword token kind for modifiers", () => {
      const mockMethod = createMockMethod("testMethod", {
        isStatic: true,
        isProtected: true,
        isAbstract: true,
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      expect(tokens.find((t) => t.Value === "static")?.Kind).toBe(TokenKind.Keyword);
      expect(tokens.find((t) => t.Value === "protected")?.Kind).toBe(TokenKind.Keyword);
      expect(tokens.find((t) => t.Value === "abstract")?.Kind).toBe(TokenKind.Keyword);
    });

    it("uses TypeName token kind for type parameters", () => {
      const mockMethod = createMockMethod("genericMethod", {
        typeParameters: [createMockTypeParameter("T")],
      });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      const typeParamToken = tokens.find((t) => t.Value === "T");
      expect(typeParamToken?.Kind).toBe(TokenKind.TypeName);
    });

    it("sets correct spacing on tokens", () => {
      const mockMethod = createMockMethod("spacedMethod", { isStatic: true });

      const { tokens } = methodTokenGenerator.generate(mockMethod, false);

      const staticToken = tokens.find((t) => t.Value === "static");
      expect(staticToken?.HasSuffixSpace).toBe(true);
      expect(staticToken?.HasPrefixSpace).toBe(false);
    });
  });
});
