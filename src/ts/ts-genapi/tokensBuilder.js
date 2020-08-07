"use strict";
exports.__esModule = true;
exports.TokensBuilder = void 0;
var jsTokens = require("js-tokens");
var TokensBuilder = /** @class */ (function () {
    function TokensBuilder() {
        this.tokens = [];
        this.indentString = "";
        this.keywords = [
            "break",
            "case",
            "catch",
            "class",
            "const",
            "continue",
            "debugger",
            "default",
            "delete",
            "do",
            "else",
            "enum",
            "export",
            "extends",
            "false",
            "finally",
            "for",
            "function",
            "if",
            "import",
            "in",
            "instanceof",
            "new",
            "null",
            "return",
            "super",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "var",
            "void",
            "while",
            "with",
            "as",
            "implements",
            "interface",
            "let",
            "package",
            "private",
            "protected",
            "public",
            "static",
            "yield",
            "any",
            "boolean",
            "constructor",
            "declare",
            "get",
            "module",
            "require",
            "number",
            "set",
            "string",
            "symbol",
            "type",
            "from",
            "of",
            "keyof",
            "readonly"
        ];
    }
    TokensBuilder.prototype.indent = function () {
        this.tokens.push({
            Kind: 2 /* Whitespace */,
            Value: this.indentString
        });
        return this;
    };
    TokensBuilder.prototype.incIndent = function () {
        this.indentString = ' '.repeat(this.indentString.length + 4);
        return this;
    };
    TokensBuilder.prototype.decIndent = function () {
        this.indentString = ' '.repeat(this.indentString.length - 4);
        return this;
    };
    TokensBuilder.prototype.newline = function () {
        this.tokens.push({
            Kind: 1 /* Newline */
        });
        return this;
    };
    TokensBuilder.prototype.lineId = function (id) {
        this.tokens.push({
            Kind: 5 /* LineIdMarker */,
            DefinitionId: id
        });
        return this;
    };
    TokensBuilder.prototype.typeReference = function (id, name) {
        this.tokens.push({
            Kind: 6 /* TypeName */,
            NavigateToId: id,
            Value: name
        });
        return this;
    };
    TokensBuilder.prototype.space = function (s) {
        if (s === void 0) { s = " "; }
        this.tokens.push({
            Kind: 2 /* Whitespace */,
            Value: s
        });
        return this;
    };
    TokensBuilder.prototype.punct = function (s) {
        this.tokens.push({
            Kind: 3 /* Punctuation */,
            Value: s
        });
        return this;
    };
    TokensBuilder.prototype.string = function (s) {
        this.tokens.push({
            Kind: 8 /* StringLiteral */,
            Value: s
        });
        return this;
    };
    TokensBuilder.prototype.text = function (s) {
        this.tokens.push({
            Kind: 0 /* Text */,
            Value: s
        });
        return this;
    };
    TokensBuilder.prototype.keyword = function (s) {
        this.tokens.push({
            Kind: 4 /* Keyword */,
            Value: s
        });
        return this;
    };
    TokensBuilder.prototype.splitAppend = function (s, currentTypeId, currentTypeName) {
        var _this = this;
        s.split("\n").forEach(function (line, index, array) {
            if (index > 0) {
                _this.indent();
            }
            var tokens = Array.from(jsTokens(line));
            tokens.forEach(function (token) {
                if (_this.keywords.indexOf(token.value) > 0) {
                    _this.keyword(token.value);
                }
                else if (token.value === currentTypeName) {
                    _this.tokens.push({ Kind: 6 /* TypeName */, DefinitionId: currentTypeId, Value: token.value });
                }
                else if (token.type === "StringLiteral") {
                    _this.string(token.value);
                }
                else if (token.type === "Punctuator") {
                    _this.punct(token.value);
                }
                else if (token.type === "WhiteSpace") {
                    _this.space(token.value);
                }
                else {
                    _this.text(token.value);
                }
            });
            if (index < array.length - 1) {
                _this.newline();
            }
        });
    };
    return TokensBuilder;
}());
exports.TokensBuilder = TokensBuilder;
