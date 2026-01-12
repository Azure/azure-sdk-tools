import { describe, it, expect } from "vitest";
import { classTokenGenerator } from "../../src/tokenGenerators/class";
import {
  ApiClass,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

// Helper function to create a mock ApiClass with all required properties
function createMockClass(displayName: string, excerptTokens: ExcerptToken[]): ApiClass {
  const mock: any = {
    kind: ApiItemKind.Class,
    displayName,
    excerptTokens,
    // Required properties from ApiItem
    canonicalReference: {
      toString: () => `@test!${displayName}:class`,
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
    // Properties needed by class generator
    isAbstract: false,
    typeParameters: [],
    extendsType: undefined,
    implementsTypes: [],
  };

  return mock as ApiClass;
}

describe("classTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for class items", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
      } as ApiClass;

      expect(classTokenGenerator.isValid(mockClass)).toBe(true);
    });

    it("returns false for non-class items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as ApiItem;

      expect(classTokenGenerator.isValid(mockInterface)).toBe(false);
    });

    it("returns false for enum items", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
      } as ApiItem;

      expect(classTokenGenerator.isValid(mockEnum)).toBe(false);
    });
  });

  describe("generate", () => {
    it("throws error for non-class items", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
        excerptTokens: [],
      } as unknown as ApiClass;

      expect(() => classTokenGenerator.generate(mockEnum)).toThrow(
        "Invalid item TestEnum of kind Enum for Class token generator.",
      );
    });

    it("generates tokens for a simple class", () => {
      const mockClass = createMockClass("TestClass", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "TestClass" } as ExcerptToken,
      ]);

      const tokens = classTokenGenerator.generate(mockClass);

      expect(tokens.length).toBeGreaterThan(0);
      expect(tokens.some((t) => t.Value === "export")).toBe(true);
      expect(tokens.some((t) => t.Value === "class")).toBe(true);
      expect(tokens.some((t) => t.Value === "TestClass")).toBe(true);
    });

    it("handles deprecated class", () => {
      const mockClass = createMockClass("DeprecatedClass", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "DeprecatedClass" } as ExcerptToken,
      ]);

      const tokens = classTokenGenerator.generate(mockClass, true);

      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });

    it("handles class with extends", () => {
      const mockClass = createMockClass("ExtendedClass", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "ExtendedClass " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "extends " } as ExcerptToken,
        {
          kind: ExcerptTokenKind.Reference,
          text: "BaseClass",
          canonicalReference: {
            toString: () => "@test!BaseClass:class",
          },
        } as ExcerptToken,
      ]);

      // Add extendsType property
      (mockClass as any).extendsType = {
        excerpt: {
          spannedTokens: [
            {
              kind: ExcerptTokenKind.Reference,
              text: "BaseClass",
              canonicalReference: {
                toString: () => "@test!BaseClass:class",
              },
            },
          ],
        },
      };

      const tokens = classTokenGenerator.generate(mockClass);

      const referenceToken = tokens.find(
        (t) => t.Kind === TokenKind.TypeName && t.NavigateToId === "@test!BaseClass:class",
      );
      expect(referenceToken).toBeDefined();
      expect(referenceToken?.Value).toBe("BaseClass");
    });

    it("handles class with implements", () => {
      const mockClass = createMockClass("ImplementingClass", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "ImplementingClass " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "implements " } as ExcerptToken,
        {
          kind: ExcerptTokenKind.Reference,
          text: "IInterface",
          canonicalReference: {
            toString: () => "@test!IInterface:interface",
          },
        } as ExcerptToken,
      ]);

      // Add implementsTypes property
      (mockClass as any).implementsTypes = [
        {
          excerpt: {
            spannedTokens: [
              {
                kind: ExcerptTokenKind.Reference,
                text: "IInterface",
                canonicalReference: {
                  toString: () => "@test!IInterface:interface",
                },
              },
            ],
          },
        },
      ];

      const tokens = classTokenGenerator.generate(mockClass);

      expect(tokens.length).toBeGreaterThan(0);
      const referenceToken = tokens.find((t) => t.NavigateToId === "@test!IInterface:interface");
      expect(referenceToken).toBeDefined();
    });

    it("handles generic class", () => {
      const mockClass = createMockClass("GenericClass", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "GenericClass" } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "<" } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "T" } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: ">" } as ExcerptToken,
      ]);

      // Add typeParameters property
      (mockClass as any).typeParameters = [
        {
          name: "T",
          constraintExcerpt: { text: "", spannedTokens: [] },
          defaultTypeExcerpt: { text: "", spannedTokens: [] },
        },
      ];

      const tokens = classTokenGenerator.generate(mockClass);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("GenericClass");
      expect(tokenValues).toContain("<");
      expect(tokenValues).toContain("T");
      expect(tokenValues).toContain(">");
    });

    it("handles class with multiple interfaces", () => {
      const mockClass = createMockClass("MultiImpl", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "MultiImpl " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "implements " } as ExcerptToken,
        {
          kind: ExcerptTokenKind.Reference,
          text: "IFirst",
          canonicalReference: {
            toString: () => "@test!IFirst:interface",
          },
        } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: ", " } as ExcerptToken,
        {
          kind: ExcerptTokenKind.Reference,
          text: "ISecond",
          canonicalReference: {
            toString: () => "@test!ISecond:interface",
          },
        } as ExcerptToken,
      ]);

      // Add implementsTypes property
      (mockClass as any).implementsTypes = [
        {
          excerpt: {
            spannedTokens: [
              {
                kind: ExcerptTokenKind.Reference,
                text: "IFirst",
                canonicalReference: {
                  toString: () => "@test!IFirst:interface",
                },
              },
            ],
          },
        },
        {
          excerpt: {
            spannedTokens: [
              {
                kind: ExcerptTokenKind.Reference,
                text: "ISecond",
                canonicalReference: {
                  toString: () => "@test!ISecond:interface",
                },
              },
            ],
          },
        },
      ];

      const tokens = classTokenGenerator.generate(mockClass);

      const firstRef = tokens.find((t) => t.NavigateToId === "@test!IFirst:interface");
      const secondRef = tokens.find((t) => t.NavigateToId === "@test!ISecond:interface");
      expect(firstRef).toBeDefined();
      expect(secondRef).toBeDefined();
    });

    it("handles abstract class", () => {
      const mockClass = createMockClass("AbstractClass", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "abstract " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "AbstractClass" } as ExcerptToken,
      ]);

      // Add isAbstract property
      (mockClass as any).isAbstract = true;

      const tokens = classTokenGenerator.generate(mockClass);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("abstract");
      expect(tokenValues).toContain("class");
      expect(tokenValues).toContain("AbstractClass");
    });

    it("preserves class display name exactly", () => {
      const testCases = ["SimpleClass", "MyClass123", "ɵInternalClass", "_PrivateClass"];

      testCases.forEach((displayName) => {
        const mockClass = createMockClass(displayName, [
          { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
          { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
          { kind: ExcerptTokenKind.Content, text: displayName } as ExcerptToken,
        ]);

        const tokens = classTokenGenerator.generate(mockClass);
        expect(tokens.some((t) => t.Value === displayName)).toBe(true);
      });
    });

    it("handles declare keyword", () => {
      const mockClass = createMockClass("CryptographyClient", [
        { kind: ExcerptTokenKind.Content, text: "export " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "declare " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "class " } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: "CryptographyClient" } as ExcerptToken,
      ]);

      const tokens = classTokenGenerator.generate(mockClass);

      const tokenValues = tokens.map((t) => t.Value);
      expect(tokenValues).toContain("export");
      expect(tokenValues).toContain("class");
      expect(tokenValues).toContain("CryptographyClient");
    });
  });
});
