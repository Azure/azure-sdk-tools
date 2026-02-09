import { describe, expect, it } from "vitest";
import { enumTokenGenerator } from "../../src/tokenGenerators/enum";
import { ApiEnum, ApiItem, ApiItemKind } from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

describe("enumTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for enum items", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
      } as ApiEnum;

      expect(enumTokenGenerator.isValid(mockEnum)).toBe(true);
    });

    it("returns false for non-enum items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as ApiItem;

      expect(enumTokenGenerator.isValid(mockInterface)).toBe(false);
    });

    it("returns false for class items", () => {
      const mockClass = {
        kind: ApiItemKind.Class,
        displayName: "TestClass",
      } as ApiItem;

      expect(enumTokenGenerator.isValid(mockClass)).toBe(false);
    });
  });

  describe("generate", () => {
    it("generates correct tokens for a non-deprecated enum", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "KnownFoo",
      } as ApiEnum;

      const { tokens } = enumTokenGenerator.generate(mockEnum, false);

      expect(tokens).toHaveLength(3);
      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        IsDeprecated: false,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "enum",
        HasSuffixSpace: true,
        IsDeprecated: false,
      });
      expect(tokens[2]).toEqual({
        Kind: TokenKind.MemberName,
        Value: "KnownFoo",
        IsDeprecated: false,
      });
    });

    it("generates correct tokens for a deprecated enum", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "OldEnum",
      } as ApiEnum;

      const { tokens } = enumTokenGenerator.generate(mockEnum, true);

      expect(tokens).toHaveLength(3);
      expect(tokens[0]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "export",
        HasSuffixSpace: true,
        IsDeprecated: true,
      });
      expect(tokens[1]).toEqual({
        Kind: TokenKind.Keyword,
        Value: "enum",
        HasSuffixSpace: true,
        IsDeprecated: true,
      });
      expect(tokens[2]).toEqual({
        Kind: TokenKind.MemberName,
        Value: "OldEnum",
        IsDeprecated: true,
      });
    });

    it("does not include braces or enum members", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "TestEnum",
      } as ApiEnum;

      const { tokens } = enumTokenGenerator.generate(mockEnum, false);

      // Should only have export, enum, and name - no braces or member tokens
      expect(tokens).toHaveLength(3);
      expect(tokens.every((t) => t.Value !== "{" && t.Value !== "}")).toBe(true);
    });

    it("throws error for non-enum items", () => {
      const mockInterface = {
        kind: ApiItemKind.Interface,
        displayName: "TestInterface",
      } as unknown as ApiEnum;

      expect(() => enumTokenGenerator.generate(mockInterface, false)).toThrow(
        "Invalid item TestInterface of kind Interface for Enum token generator.",
      );
    });

    it("generates tokens with correct spacing", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "MyEnum",
      } as ApiEnum;

      const { tokens } = enumTokenGenerator.generate(mockEnum, false);

      // First two tokens should have suffix space, last one should not
      expect(tokens[0].HasSuffixSpace).toBe(true);
      expect(tokens[1].HasSuffixSpace).toBe(true);
      expect(tokens[2].HasSuffixSpace).toBeUndefined();
    });

    it("preserves enum display name exactly", () => {
      const testCases = [
        "SimpleEnum",
        "KnownStatusCodes",
        "HTTP_METHODS",
        "Enum123",
        "ɵInternalEnum",
      ];

      testCases.forEach((displayName) => {
        const mockEnum = {
          kind: ApiItemKind.Enum,
          displayName,
        } as ApiEnum;

        const { tokens } = enumTokenGenerator.generate(mockEnum, false);
        expect(tokens[2].Value).toBe(displayName);
      });
    });
  });
});
