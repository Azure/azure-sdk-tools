import { ApiItemKind, ApiProperty, ApiPropertySignature } from "@microsoft/api-extractor-model";
import { expect } from "chai";
import { propertyTokenGenerator } from "../../src/tokenGenerators/property";
import { TokenKind } from "../../src/models";
import { describe, it } from "vitest";

describe("Property Token Generator", () => {
    describe("isValid", () => {
        it("should return true for ApiProperty", () => {
            const mockItem = { kind: ApiItemKind.Property } as ApiProperty;
            expect(propertyTokenGenerator.isValid(mockItem)).to.be.true;
        });

        it("should return true for ApiPropertySignature", () => {
            const mockItem = { kind: ApiItemKind.PropertySignature } as ApiPropertySignature;
            expect(propertyTokenGenerator.isValid(mockItem)).to.be.true;
        });

        it("should return false for other kinds", () => {
            const mockItem = { kind: ApiItemKind.Method } as any;
            expect(propertyTokenGenerator.isValid(mockItem)).to.be.false;
        });
    });

    describe("generate", () => {
        it("should generate tokens for a simple property", () => {
            const mockItem = {
                kind: ApiItemKind.Property,
                displayName: "myProperty",
                isReadonly: false,
                isOptional: false,
                propertyTypeExcerpt: { text: "string" },
            } as unknown as ApiProperty;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens).to.have.lengthOf(3);
            expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "myProperty" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.TypeName, Value: "string" });
        });

        it("should generate tokens for a readonly property", () => {
            const mockItem = {
                kind: ApiItemKind.Property,
                displayName: "readonlyProp",
                isReadonly: true,
                isOptional: false,
                propertyTypeExcerpt: { text: "number" },
            } as unknown as ApiProperty;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "readonlyProp" });
        });

        it("should generate tokens for an optional property", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "optionalProp",
                isReadonly: false,
                isOptional: true,
                propertyTypeExcerpt: { text: "boolean" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "optionalProp" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
        });

        it("should generate tokens for a static property", () => {
            const mockItem = Object.create(ApiProperty.prototype);
            Object.defineProperties(mockItem, {
                kind: { value: ApiItemKind.Property, writable: false },
                displayName: { value: "staticProp", writable: false },
                isStatic: { value: true, writable: false },
                isReadonly: { value: false, writable: false },
                isOptional: { value: false, writable: false },
                propertyTypeExcerpt: { value: { text: "string" }, writable: false },
            });

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "static" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "staticProp" });
        });

        it("should generate tokens for a static readonly property", () => {
            const mockItem = Object.create(ApiProperty.prototype);
            Object.defineProperties(mockItem, {
                kind: { value: ApiItemKind.Property, writable: false },
                displayName: { value: "staticReadonlyProp", writable: false },
                isStatic: { value: true, writable: false },
                isReadonly: { value: true, writable: false },
                isOptional: { value: false, writable: false },
                propertyTypeExcerpt: { value: { text: "string" }, writable: false },
            });

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "static" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.MemberName, Value: "staticReadonlyProp" });
        });

        it("should mark tokens as deprecated when deprecated flag is set", () => {
            const mockItem = {
                kind: ApiItemKind.Property,
                displayName: "deprecatedProp",
                isReadonly: false,
                isOptional: false,
                propertyTypeExcerpt: { text: "string" },
            } as unknown as ApiProperty;

            const result = propertyTokenGenerator.generate(mockItem, true);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "deprecatedProp", IsDeprecated: true });
        });

        it("should throw error for invalid item kind", () => {
            const mockItem = {
                kind: ApiItemKind.Method,
                displayName: "invalidItem",
            } as any;

            expect(() => propertyTokenGenerator.generate(mockItem)).to.throw();
        });
    });

    describe("generate - PropertySignature", () => {
        it("should generate tokens for a simple property signature", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "signatureProp",
                isReadonly: false,
                isOptional: false,
                propertyTypeExcerpt: { text: "string" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens).to.have.lengthOf(3);
            expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "signatureProp" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.TypeName, Value: "string" });
        });

        it("should generate tokens for a readonly property signature", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "readonlySignature",
                isReadonly: true,
                isOptional: false,
                propertyTypeExcerpt: { text: "number" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "readonlySignature" });
        });

        it("should generate tokens for an optional property signature", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "optionalSignature",
                isReadonly: false,
                isOptional: true,
                propertyTypeExcerpt: { text: "boolean" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "optionalSignature" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
            expect(tokens[3]).to.deep.include({ Kind: TokenKind.TypeName, Value: "boolean" });
        });

        it("should generate tokens for a readonly optional property signature", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "readonlyOptional",
                isReadonly: true,
                isOptional: true,
                propertyTypeExcerpt: { text: "string[]" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.Keyword, Value: "readonly" });
            expect(tokens[1]).to.deep.include({ Kind: TokenKind.MemberName, Value: "readonlyOptional" });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.Punctuation, Value: "?" });
            expect(tokens[3]).to.deep.include({ Kind: TokenKind.Punctuation, Value: ":" });
            expect(tokens[4]).to.deep.include({ Kind: TokenKind.TypeName, Value: "string[]" });
        });

        it("should not add static keyword for property signature", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "interfaceProp",
                isReadonly: false,
                isOptional: false,
                propertyTypeExcerpt: { text: "string" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem);
            const tokens = result.Tokens;

            const hasStatic = tokens.some(t => t.Value === "static");
            expect(hasStatic).to.be.false;
        });

        it("should mark property signature tokens as deprecated", () => {
            const mockItem = {
                kind: ApiItemKind.PropertySignature,
                displayName: "deprecatedSignature",
                isReadonly: false,
                isOptional: false,
                propertyTypeExcerpt: { text: "string" },
            } as unknown as ApiPropertySignature;

            const result = propertyTokenGenerator.generate(mockItem, true);
            const tokens = result.Tokens;

            expect(tokens[0]).to.deep.include({ Kind: TokenKind.MemberName, Value: "deprecatedSignature", IsDeprecated: true });
            expect(tokens[2]).to.deep.include({ Kind: TokenKind.TypeName, Value: "string", IsDeprecated: true });
        });
    });
});
