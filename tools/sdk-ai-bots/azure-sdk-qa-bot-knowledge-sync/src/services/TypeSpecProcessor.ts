import * as fs from 'fs';

interface TypeSpecDefinition {
    type: 'model' | 'operation' | 'interface' | 'enum' | 'union' | 'alias' | 'namespace' | 'scalar';
    name: string;
    code: string;
    decorators: string[];
    description: string;
    comments: string[];
}

export class TypeSpecProcessor {
    /**
     * Convert a TypeSpec file to a Markdown file.
     * Each definition (model, operation, interface, enum, etc.) becomes a chapter
     * with the name as title and the TypeSpec code as content.
     */
    public convertTypeSpecToMarkdown(tspFile: string, mkFile: string): void {
        const content = fs.readFileSync(tspFile, 'utf-8');
        const definitions = this.parseTypeSpecDefinitions(content);
        const markdown = this.generateMarkdown(definitions, tspFile);
        fs.writeFileSync(mkFile, markdown, 'utf-8');
    }

    /**
     * Parse TypeSpec content and extract all definitions.
     */
    private parseTypeSpecDefinitions(content: string): TypeSpecDefinition[] {
        const definitions: TypeSpecDefinition[] = [];
        const lines = content.split('\n');
        
        let currentDecorators: string[] = [];
        let currentComments: string[] = [];
        let braceCount = 0;
        let inDefinition = false;
        let inBlockComment = false;
        let blockCommentLines: string[] = [];
        let definitionLines: string[] = [];
        let currentType: TypeSpecDefinition['type'] | null = null;
        let currentName = '';

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();

            // Handle block comments /** ... */
            if (!inDefinition) {
                if (trimmedLine.startsWith('/**') && !inBlockComment) {
                    inBlockComment = true;
                    blockCommentLines = [trimmedLine];
                    // Check if block comment ends on same line
                    if (trimmedLine.endsWith('*/') && trimmedLine !== '/**') {
                        inBlockComment = false;
                        currentComments = blockCommentLines;
                        blockCommentLines = [];
                    }
                    continue;
                }
                
                if (inBlockComment) {
                    blockCommentLines.push(trimmedLine);
                    if (trimmedLine.endsWith('*/')) {
                        inBlockComment = false;
                        currentComments = blockCommentLines;
                        blockCommentLines = [];
                    }
                    continue;
                }

                // Handle single-line comments //
                if (trimmedLine.startsWith('//')) {
                    currentComments.push(trimmedLine);
                    continue;
                }
            }

            // Collect decorators
            if (trimmedLine.startsWith('@') && !inDefinition) {
                currentDecorators.push(trimmedLine);
                continue;
            }

            // Detect definition start
            if (!inDefinition) {
                const definitionMatch = this.matchDefinitionStart(trimmedLine);
                if (definitionMatch) {
                    currentType = definitionMatch.type;
                    currentName = definitionMatch.name;
                    inDefinition = true;
                    definitionLines = [...currentDecorators, line];
                    braceCount = (line.match(/{/g) || []).length - (line.match(/}/g) || []).length;
                    
                    // Handle single-line definitions (like aliases or simple scalars)
                    if (trimmedLine.endsWith(';') && braceCount === 0) {
                        const description = this.extractDescription(currentDecorators, currentComments);
                        const codeWithComments = this.buildCodeWithComments(currentComments, definitionLines);
                        definitions.push({
                            type: currentType,
                            name: currentName,
                            code: codeWithComments,
                            decorators: currentDecorators,
                            description,
                            comments: currentComments
                        });
                        this.resetState();
                        currentDecorators = [];
                        currentComments = [];
                        inDefinition = false;
                        definitionLines = [];
                        currentType = null;
                        currentName = '';
                    }
                    continue;
                } else {
                    // Non-decorator, non-definition line - reset decorators and comments
                    if (trimmedLine !== '' && !trimmedLine.startsWith('//') && !trimmedLine.startsWith('import') && !trimmedLine.startsWith('using')) {
                        currentDecorators = [];
                        currentComments = [];
                    }
                }
            }

            // Inside a definition - track braces
            if (inDefinition) {
                definitionLines.push(line);
                braceCount += (line.match(/{/g) || []).length;
                braceCount -= (line.match(/}/g) || []).length;

                // Definition complete
                if (braceCount === 0) {
                    const description = this.extractDescription(currentDecorators, currentComments);
                    const codeWithComments = this.buildCodeWithComments(currentComments, definitionLines);
                    definitions.push({
                        type: currentType!,
                        name: currentName,
                        code: codeWithComments,
                        decorators: currentDecorators,
                        description,
                        comments: currentComments
                    });
                    currentDecorators = [];
                    currentComments = [];
                    inDefinition = false;
                    definitionLines = [];
                    currentType = null;
                    currentName = '';
                }
            }
        }

        return definitions;
    }

    /**
     * Build the complete code block including comments and decorators.
     */
    private buildCodeWithComments(comments: string[], definitionLines: string[]): string {
        const codeLines: string[] = [];
        
        // Add comments first (JSDoc or single-line)
        if (comments.length > 0) {
            for (const comment of comments) {
                codeLines.push(comment);
            }
        }
        
        // Add the definition lines (which already include decorators)
        codeLines.push(...definitionLines);
        
        return codeLines.join('\n');
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

        // Group definitions by type
        const groupedDefs = this.groupByType(definitions);

        // Generate table of contents
        lines.push('## Table of Contents');
        lines.push('');
        for (const [type, defs] of Object.entries(groupedDefs)) {
            if (defs.length > 0) {
                lines.push(`### ${this.capitalizeType(type)}s`);
                for (const def of defs) {
                    const anchor = this.toAnchor(def.name);
                    lines.push(`- [${def.name}](#${anchor})`);
                }
                lines.push('');
            }
        }
        lines.push('---');
        lines.push('');

        // Generate chapters for each definition
        for (const def of definitions) {
            lines.push(`## ${def.name}`);
            lines.push('');
            lines.push(`**Type:** ${this.capitalizeType(def.type)}`);
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