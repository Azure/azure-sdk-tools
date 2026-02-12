import * as fs from 'fs';
import path from 'path';

type TypeSpecDefinitionType = 'model' | 'operation' | 'interface' | 'enum' | 'union' | 'alias' | 'namespace' | 'scalar';
interface TypeSpecDefinition {
    type: TypeSpecDefinitionType;
    name: string;
    code: string;
    decorators: string[];
    description: string;
    comments: string[];
}

export class TypeSpecProcessor {
    private workDir: string;
    private relativeLibDir: string;
    private srcDir: string;
    private destDir: string;
    constructor(workDir: string, relativeLibDir: string) {
        this.workDir = workDir;
        this.relativeLibDir = relativeLibDir;
        this.srcDir = path.join(this.workDir, this.relativeLibDir);
        this.destDir = path.join(this.workDir, this.relativeLibDir, "generated");
    }
    public processTypeSpecLibraries(): void {
        if (!fs.existsSync(this.srcDir) || !fs.statSync(this.srcDir).isDirectory()) {
            console.error(`Typespec library directory not found or is not a directory: ${this.srcDir}`);
            throw new Error(`Typespec library directory not found or is not a directory: ${this.srcDir}`);
        }

        if (!fs.existsSync(this.destDir)) {
            try {
                fs.mkdirSync(this.destDir);
            } catch(error) {
                console.error(`Failed to create destination directory ${this.destDir}.`, error);
                throw error;
            }
        }
        // Recursively list all .tsp files in srcDir
        const tspFiles: string[] = [];
        function walk(dir: string) {
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                if (entry.isDirectory()) {
                    walk(fullPath);
                } else if (entry.isFile() && entry.name.endsWith('.tsp')) {
                    tspFiles.push(fullPath);
                }
            }
        }
        walk(this.srcDir);
        console.log('Found .tsp files:', tspFiles);
        
        for (const tspFile of tspFiles) {
            const relativepath = path.relative(this.srcDir, tspFile);
            // Replace all / or \ with -
            const safeName = relativepath.replace(/[\\/]/g, "#");
            const mdFile = path.join(this.destDir, safeName.replace(".tsp", ".md"));
            this.convertTypeSpecToMarkdown(tspFile, mdFile);
        }
    }

    /**
     * Convert a TypeSpec file to a Markdown file.
     * Each definition (model, operation, interface, enum, etc.) becomes a chapter
     * with the name as title and the TypeSpec code as content.
     */
    private convertTypeSpecToMarkdown(tspFile: string, mdFile: string): void {
        const content = fs.readFileSync(tspFile, 'utf-8');
        const definitions = this.parseTypeSpecDefinitions(content);
        const relativepath = path.relative(this.workDir, tspFile);
        const markdown = this.generateMarkdown(definitions, relativepath);
        fs.writeFileSync(mdFile, markdown, 'utf-8');
    }

    /**
     * Parse TypeSpec content and extract all definitions.
     */
    /**
     * Parses raw TypeSpec file content into an array of structured `TypeSpecDefinition` objects.
     *
     * This method performs a line-by-line scan of the input string, identifying and extracting
     * TypeSpec constructs such as models, enums, unions, interfaces, operations, scalars, aliases,
     * and namespaces. For each definition found, it captures:
     *
     * - **Comments**: Both single-line (`//`) and block (`/** ... *​/`) comments that precede a definition.
     * - **Decorators**: Lines starting with `@` that annotate the subsequent definition.
     * - **Definition body**: The full code of the definition, including multi-line brace-delimited
     *   blocks (e.g., `model Foo { ... }`) and single/multi-line semicolon-terminated statements
     *   (e.g., `alias Foo = string;`).
     *
     * @param content - The raw string content of a TypeSpec (`.tsp`) file to parse.
     * @returns An array of {@link TypeSpecDefinition} objects representing each parsed definition,
     *          including its type, name, full code (with preceding comments), decorators,
     *          extracted description, and associated comments.
     */
    private parseTypeSpecDefinitions(content: string): TypeSpecDefinition[] {
        const definitions: TypeSpecDefinition[] = [];
        const lines = content.split('\n');
        let currentDefinitionStart = -1;
        let currentDefinitionEnd = -1;
        let currentDefinitionBodyStart = -1;
        let currentType: TypeSpecDefinition['type'] | null = null;
        let currentName = '';

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();
            const definitionMatch = this.matchDefinitionStart(trimmedLine);
            if (definitionMatch) {
                if (currentDefinitionStart == -1) {
                    currentDefinitionStart = 0;
                    currentDefinitionBodyStart = i;
                    currentType = definitionMatch.type;
                    currentName = definitionMatch.name;
                }
                else {
                    let l = i;
                    for (; l > currentDefinitionStart; l--) {
                        let trim = lines[l].trim();
                        if (trim.endsWith(';') || trim.endsWith('}')) {
                            currentDefinitionEnd = l;
                            break;
                        }
                    }
                    const definition = this.parseDefinition(currentType, currentName, lines, currentDefinitionStart, currentDefinitionBodyStart, i);
                    definitions.push(definition);
                    
                    currentDefinitionStart = l + 1;
                    currentDefinitionBodyStart = i;
                    currentType = definitionMatch.type;
                    currentName = definitionMatch.name;
                }
            }
        }

        //handle the last definition
        definitions.push(this.parseDefinition(currentType, currentName, lines, currentDefinitionStart, currentDefinitionBodyStart, lines.length -1));
        return definitions;
    }


    private parseDefinition(definitionType: TypeSpecDefinitionType, definitionName: string, lines: string[], definitionStart: number, definitionBodyStart: number, nextDefinitionStart: number): TypeSpecDefinition {
        let definitionEnd = -1;
        for (let l = nextDefinitionStart; l > definitionStart; l--) {
            let trim = lines[l].trim();
            if (trim.endsWith(';') || trim.endsWith('}')) {
                definitionEnd = l;
                break;
            }
        }

        let blockCommentLines: string[] = [];
        let currentDecorators: string[] = [];
        let inBlockComment = false;
        let inDecoratorBlock = false;
        let parenthesisCount = 0;
        for (let n = definitionStart; n < definitionBodyStart; n++) {
            const definitionLine = lines[n];
            const trimmed = definitionLine.trim();
            // Handle block comments /** ... */
            if (trimmed.startsWith('/**') && !inBlockComment) {
                // Check if block comment ends on same line
                if (!trimmed.endsWith('*/')) {
                    blockCommentLines.push(trimmed);
                    inBlockComment = true;
                }
                continue;
            }
            if (inBlockComment) {
                blockCommentLines.push(trimmed);
                if (trimmed.endsWith('*/')) {
                    inBlockComment = false;
                }
            }

            // Handle single-line comments //
            if (trimmed.startsWith('//')) {
                blockCommentLines.push(trimmed);
                continue;
            }
            if (trimmed.startsWith('@') && !inDecoratorBlock) {
                parenthesisCount = parenthesisCount + (trimmed.match(/\(/g) || []).length - (trimmed.match(/\)/g) || []).length;
                currentDecorators.push(trimmed);
                inDecoratorBlock = true;
                if ((trimmed.match(/\(/g) || []).length === 0 || (trimmed.endsWith(')') && parenthesisCount === 0)) {
                    inDecoratorBlock = false;
                }
                continue;
            }
            if (inDecoratorBlock) {
                // currentDecorators.push(trimmed);
                currentDecorators[currentDecorators.length - 1] = currentDecorators[currentDecorators.length - 1].concat("\n", trimmed);
                parenthesisCount = parenthesisCount + (trimmed.match(/\(/g) || []).length - (trimmed.match(/\)/g) || []).length;
                if (trimmed.endsWith(')') && parenthesisCount === 0) {
                    inDecoratorBlock = false;
                }
            }
        }
        const description = this.extractDescription(currentDecorators, blockCommentLines);
        return {
            type: definitionType,
            name: definitionName,
            code: lines.slice(definitionStart, definitionEnd + 1).join('\n'),
            decorators: currentDecorators,
            description,
            comments: blockCommentLines
        };
    }
    /**
     * Extract description from @doc decorator or JSDoc-style comments.
     */
    private extractDescription(decorators: string[], comments: string[]): string {
        // First try @doc decorator
        for (const decorator of decorators) {
            // Match @doc("...") or @doc("""...""")
            const singleLineMatch = decorator.match(/^@doc\s*\(\s*"([^"]*)"\s*\)/);
            if (singleLineMatch) {
                return singleLineMatch[1];
            }
            
            // Match multi-line @doc("""...""")
            const multiLineMatch = decorator.match(/^@doc\s*\(\s*"""([\s\S]*?)"""\s*\)/);
            if (multiLineMatch) {
                return multiLineMatch[1].trim();
            }

            // Match @summary("...")
            const summaryMatch = decorator.match(/^@summary\s*\(\s*"([^"]*)"\s*\)/);
            if (summaryMatch) {
                return summaryMatch[1];
            }
        }

        // Then try JSDoc-style comments /** ... */ or // comments
        if (comments.length > 0) {
            return this.parseJSDocComments(comments);
        }

        return '';
    }

    /**
     * Parse JSDoc-style comments and extract the description.
     * Excludes @param, @template, @returns, and other tag lines.
     */
    private parseJSDocComments(comments: string[]): string {
        const descriptionLines: string[] = [];
        let inTagSection = false;
        
        for (const line of comments) {
            let cleanLine = line;
            
            // Remove /** or /* from start
            cleanLine = cleanLine.replace(/^\/\*\*?\s*/, '');
            // Remove */ from end
            cleanLine = cleanLine.replace(/\s*\*\/$/, '');
            // Remove leading * (for middle lines of block comments)
            cleanLine = cleanLine.replace(/^\*\s?/, '');
            // Remove // from start (for single-line comments)
            cleanLine = cleanLine.replace(/^\/\/\s?/, '');
            
            const trimmedClean = cleanLine.trim();
            
            // Skip @param, @template, @returns, @example, @see, @deprecated etc. tags and their continuation
            if (trimmedClean.startsWith('@param') || 
                trimmedClean.startsWith('@template') ||
                trimmedClean.startsWith('@returns') ||
                trimmedClean.startsWith('@return') ||
                trimmedClean.startsWith('@example') ||
                trimmedClean.startsWith('@see') ||
                trimmedClean.startsWith('@deprecated') ||
                trimmedClean.startsWith('@throws') ||
                trimmedClean.startsWith('@type') ||
                trimmedClean.startsWith('@typedef') ||
                trimmedClean.startsWith('@callback') ||
                trimmedClean.startsWith('@property') ||
                trimmedClean.startsWith('@prop') ||
                trimmedClean.startsWith('@arg') ||
                trimmedClean.startsWith('@argument')) {
                inTagSection = true;
                continue;
            }
            
            // If we hit another @ tag or a new description section, reset
            if (trimmedClean.startsWith('@')) {
                inTagSection = true;
                continue;
            }
            
            // Skip lines that are part of a tag section (continuation lines)
            if (inTagSection) {
                // If the line looks like a continuation (indented or starts with lowercase after tag)
                // but if it's empty or looks like a new sentence, it might be back to description
                if (trimmedClean === '' || /^[A-Z]/.test(trimmedClean)) {
                    inTagSection = false;
                } else {
                    continue;
                }
            }
            
            if (trimmedClean) {
                descriptionLines.push(trimmedClean);
            }
        }
        
        return descriptionLines.join(' ').trim();
    }

    /**
     * Match the start of a TypeSpec definition.
     */
    private matchDefinitionStart(line: string): { type: TypeSpecDefinition['type']; name: string } | null {
        // Model definition: model Name { ... } or model Name extends Base { ... }
        const modelMatch = line.match(/^model\s+(\w+)/);
        if (modelMatch) {
            return { type: 'model', name: modelMatch[1] };
        }

        // Operation definition: op name(...): ... { ... }
        const opMatch = line.match(/^op\s+(\w+)/);
        if (opMatch) {
            return { type: 'operation', name: opMatch[1] };
        }

        // Interface definition: interface Name { ... }
        const interfaceMatch = line.match(/^interface\s+(\w+)/);
        if (interfaceMatch) {
            return { type: 'interface', name: interfaceMatch[1] };
        }

        // Enum definition: enum Name { ... }
        const enumMatch = line.match(/^enum\s+(\w+)/);
        if (enumMatch) {
            return { type: 'enum', name: enumMatch[1] };
        }

        // Union definition: union Name { ... }
        const unionMatch = line.match(/^union\s+(\w+)/);
        if (unionMatch) {
            return { type: 'union', name: unionMatch[1] };
        }

        // Alias definition: alias Name = ...;
        const aliasMatch = line.match(/^alias\s+(\w+)/);
        if (aliasMatch) {
            return { type: 'alias', name: aliasMatch[1] };
        }

        // Namespace definition: namespace Name { ... }
        const namespaceMatch = line.match(/^namespace\s+([\w.]+)/);
        if (namespaceMatch) {
            return { type: 'namespace', name: namespaceMatch[1] };
        }

        // Scalar definition: scalar Name ...
        const scalarMatch = line.match(/^scalar\s+(\w+)/);
        if (scalarMatch) {
            return { type: 'scalar', name: scalarMatch[1] };
        }

        return null;
    }

    /**
     * Generate Markdown content from parsed definitions.
     */
    private generateMarkdown(definitions: TypeSpecDefinition[], sourceFile: string): string {
        const lines: string[] = [];
        
        // Add header
        lines.push(`# TypeSpec Definitions`);
        lines.push('');
        lines.push(`Source: \`${sourceFile}\``);
        lines.push('');
        lines.push('---');
        lines.push('');

        // Generate chapters for each definition
        for (const def of definitions) {
            lines.push(`## ${def.name}`);
            lines.push('');
            if (def.type) lines.push(`**Type:** ${this.capitalizeType(def.type)}`);
            lines.push('');
            
            // Add description as chapter body if available
            if (def.description) {
                lines.push(def.description);
                lines.push('');
            }
            
            lines.push('```typespec');
            lines.push(def.code);
            lines.push('```');
            lines.push('');
            lines.push('---');
            lines.push('');
        }

        return lines.join('\n');
    }

    /**
     * Group definitions by their type.
     */
    private groupByType(definitions: TypeSpecDefinition[]): Record<string, TypeSpecDefinition[]> {
        const grouped: Record<string, TypeSpecDefinition[]> = {
            namespace: [],
            interface: [],
            model: [],
            operation: [],
            enum: [],
            union: [],
            alias: [],
            scalar: []
        };

        for (const def of definitions) {
            if (grouped[def.type]) {
                grouped[def.type].push(def);
            }
        }

        return grouped;
    }

    /**
     * Capitalize the type name for display.
     */
    private capitalizeType(type: string): string {
        return type.charAt(0).toUpperCase() + type.slice(1);
    }

    /**
     * Convert a name to a markdown anchor.
     */
    private toAnchor(name: string): string {
        return name.toLowerCase().replace(/[^a-z0-9-]/g, '-');
    }

    /**
     * Reset parsing state (helper for clarity).
     */
    private resetState(): void {
        // This is a no-op helper method for code clarity
    }
}
