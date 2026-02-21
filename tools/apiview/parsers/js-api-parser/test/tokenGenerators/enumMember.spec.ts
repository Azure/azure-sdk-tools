import { describe, expect, it } from "vitest";
import { enumMemberTokenGenerator } from "../../src/tokenGenerators/enumMember";
import {
  ApiEnumMember,
  ApiItem,
  ApiItemKind,
  ExcerptToken,
  ExcerptTokenKind,
} from "@microsoft/api-extractor-model";
import { TokenKind } from "../../src/models";

function createMockEnumMember(
  displayName: string,
  initializerTokens?: ExcerptToken[],
): ApiEnumMember {
  const mock: any = {
    kind: ApiItemKind.EnumMember,
    displayName,
    canonicalReference: {
      toString: () => `@test!MyEnum.${displayName}:member`,
    },
    containerKey: "",
    getContainerKey: () => "",
    getSortKey: () => "",
    parent: undefined,
    members: [],
    fileUrlPath: undefined,
    excerpt: {
      text: displayName,
      tokenRange: { startIndex: 0, endIndex: 0 },
      tokens: [],
    },
    excerptTokens: [],
    initializerExcerpt: initializerTokens
      ? {
          text: initializerTokens.map((t) => t.text).join(""),
          spannedTokens: initializerTokens,
        }
      : undefined,
    releaseTag: undefined,
    tsdocComment: undefined,
  };

  return mock as ApiEnumMember;
}

describe("enumMemberTokenGenerator", () => {
  describe("isValid", () => {
    it("returns true for enum member items", () => {
      const mockEnumMember = { kind: ApiItemKind.EnumMember } as ApiEnumMember;
      expect(enumMemberTokenGenerator.isValid(mockEnumMember)).toBe(true);
    });

    it("returns false for non-enum member items", () => {
      const mockEnum = { kind: ApiItemKind.Enum } as ApiItem;
      expect(enumMemberTokenGenerator.isValid(mockEnum)).toBe(false);
    });
  });

  describe("generate", () => {
    it("throws for non-enum member items", () => {
      const mockEnum = {
        kind: ApiItemKind.Enum,
        displayName: "MyEnum",
      } as unknown as ApiEnumMember;

      expect(() => enumMemberTokenGenerator.generate(mockEnum)).toThrow(
        "Invalid item MyEnum of kind Enum for EnumMember token generator.",
      );
    });

    it("generates member name without initializer", () => {
      const mockMember = createMockEnumMember("None");
      const { tokens } = enumMemberTokenGenerator.generate(mockMember, false);

      expect(tokens).toHaveLength(1);
      expect(tokens[0]).toEqual({
        Kind: TokenKind.MemberName,
        Value: "None",
        HasSuffixSpace: false,
        HasPrefixSpace: false,
        NavigateToId: undefined,
        IsDeprecated: false,
      });
    });

    it("generates numeric initializer", () => {
      const mockMember = createMockEnumMember("One", [
        { kind: ExcerptTokenKind.Content, text: "1" } as ExcerptToken,
      ]);

      const { tokens } = enumMemberTokenGenerator.generate(mockMember, false);
      const values = tokens.map((t) => t.Value);
      expect(values).toEqual(["One", "=", "1"]);
    });

    it("generates string initializer", () => {
      const mockMember = createMockEnumMember("Ready", [
        { kind: ExcerptTokenKind.Content, text: '"ready"' } as ExcerptToken,
      ]);

      const { tokens } = enumMemberTokenGenerator.generate(mockMember, false);
      expect(tokens.map((t) => t.Value)).toEqual(["Ready", "=", '"ready"']);
    });

    it("generates computed initializer with references", () => {
      const mockMember = createMockEnumMember("Computed", [
        {
          kind: ExcerptTokenKind.Reference,
          text: "Base",
          canonicalReference: { toString: () => "@test!Base:member" },
        } as ExcerptToken,
        { kind: ExcerptTokenKind.Content, text: " << 2" } as ExcerptToken,
      ]);

      const { tokens } = enumMemberTokenGenerator.generate(mockMember, false);
      const valueText = tokens.map((t) => t.Value).join(" ");
      expect(valueText).toContain("Computed = Base");
      expect(valueText).toContain("<< 2");

      const baseToken = tokens.find((t) => t.Value === "Base");
      expect(baseToken?.Kind).toBe(TokenKind.TypeName);
      expect(baseToken?.NavigateToId).toBe("@test!Base:member");
    });

    it("marks tokens as deprecated", () => {
      const mockMember = createMockEnumMember("Deprecated", [
        { kind: ExcerptTokenKind.Content, text: "0" } as ExcerptToken,
      ]);

      const { tokens } = enumMemberTokenGenerator.generate(mockMember, true);
      expect(tokens.every((t) => t.IsDeprecated === true)).toBe(true);
    });
  });
});