
import {ApiViewTokenKind, IApiViewToken} from './models';

const jsTokens = require("js-tokens");
const ANNOTATION_TOKEN = "@";

export class TokensBuilder
{
    tokens: IApiViewToken[] = [];
    indentString: string = "";
    keywords: string[] = [
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
        "namespace",
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
        "readonly"];

    annotate(value: string): TokensBuilder {
        this.tokens.push({
            Kind: ApiViewTokenKind.StringLiteral,
            Value: `${ANNOTATION_TOKEN}${value}`,
        });

        this.newline().indent()
        return this;
    }

    indent(): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.Whitespace,
            Value: this.indentString
        });
        return this;
    }

    incIndent(): TokensBuilder
    {
        this.indentString = ' '.repeat(this.indentString.length + 4); 
        return this;
    }

    decIndent(): TokensBuilder
    {
        this.indentString = ' '.repeat(this.indentString.length - 4); 
        return this;
    }

    newline(): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.Newline
        });
        return this;
    }

    lineId(id: string): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.LineIdMarker,
            DefinitionId: id
        });
        return this;
    }

    typeReference(id: string, name: string): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.TypeName,
            NavigateToId: id,
            Value: name
        });
        return this;
    }

    space(s: string = " "): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.Whitespace,
            Value: s
        });
        return this;
    }

    punct(s: string): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.Punctuation,
            Value: s
        });
        return this;
    }

    string(s: string): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.StringLiteral,
            Value: s
        });
        return this;
    }

    text(s: string): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.Text,
            Value: s
        });
        return this;
    }

    keyword(s: string): TokensBuilder
    {
        this.tokens.push({
            Kind: ApiViewTokenKind.Keyword,
            Value: s
        });
        return this;
    }

    splitAppend(s: string, currentTypeId: string, currentTypeName: string)
    {
        s.split("\n").forEach((line, index, array)  => {
            if (index > 0)
            {
                this.indent();
            }

            var tokens: any[] = Array.from(jsTokens(line));
            tokens.forEach(token => 
            {
                if (this.keywords.indexOf(token.value) > 0)
                {
                    this.keyword(token.value);
                }
                else if (token.value === currentTypeName)
                {
                    this.tokens.push({ Kind: ApiViewTokenKind.TypeName, DefinitionId: currentTypeId, Value: token.value });
                }
                else if (token.type === "StringLiteral")
                {
                    this.string(token.value);
                }
                else if (token.type === "Punctuator")
                {
                    this.punct(token.value);
                }
                else if (token.type === "WhiteSpace")
                {
                    this.space(token.value);
                }
                else
                {
                    this.text(token.value);
                }
            });
            
            if (index < array.length - 1)
            {
                this.newline();
            }
        });
    }
}
