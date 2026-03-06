import * as fs from 'fs';
import path from 'path';

type TypeSpecDefinitionType = 'model' | 'operation' | 'interface' | 'enum' | 'union' | 'alias' | 'namespace' | 'scalar' | 'decorator';
interface TypeSpecDefinition {
    type: TypeSpecDefinitionType;
    name: string;
    fullName: string;
    code: string;
    decorators: string[];
    description: string;
    comments: string[];
    parent?: string;
    children?: TypeSpecDefinition[];
    level: number;
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

    private parseTypeSpecDefinitions(content: string): TypeSpecDefinition[] {
        const lines = content.split('\n');
        return this.parseTypeSpecDefinitionsHelper(lines);
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
    private parseTypeSpecDefinitionsHelper(lines: string[], level: number = 1): TypeSpecDefinition[] {
        const definitions: TypeSpecDefinition[] = [];
        let currentDefinitionStart = -1;
        let currentDefinitionBodyStart = -1;
        let currentType: TypeSpecDefinitionType | undefined = undefined;
        let currentName = '';
        let currentLevel = level;
        let braceCount = 0;
        let hasGlobalNamespace = false;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const trimmedLine = line.trim();
            const definitionMatch = this.matchDefinitionStart(trimmedLine);
            if (definitionMatch && braceCount === 0) {
                braceCount = (trimmedLine.match(/\{/g) || []).length - (trimmedLine.match(/\}/g) || []).length;
                if (definitionMatch.type === 'namespace') {
                    if (trimmedLine.endsWith(';')) {
                        // global namespace
                        hasGlobalNamespace = true;
                        currentLevel++;
                    }
                }
                if (currentDefinitionStart == -1) {
                    // calculate the start of the first definition
                    let inCommentBlock = false;
                    for (let l = i -1; l > 0; l--) {
                        let trim = lines[l].trim();
                        if (trim.endsWith('*/')) inCommentBlock = true;
                        if (trim.startsWith('/*')) inCommentBlock = false;
                        let isComment = inCommentBlock || trim.startsWith("//");
                        if (!isComment && (trim.endsWith(';') || trim.endsWith('}'))) {
                            currentDefinitionStart = l + 1;
                            break;
                        }
                    }
                    if (currentDefinitionStart == -1) currentDefinitionStart = 0;
                    currentDefinitionBodyStart = i;
                    currentType = definitionMatch.type;
                    currentName = definitionMatch.name;
                }
                else {
                    let l = i - 1;
                    let inCommentBlock = false;
                    for (; l > currentDefinitionStart; l--) {
                        let trim = lines[l].trim();
                        if (trim.endsWith('*/')) inCommentBlock = true;
                        if (trim.startsWith('/*')) inCommentBlock = false;
                        let isComment = inCommentBlock || trim.startsWith("//");
                        if (!isComment && (trim.endsWith(';') || trim.endsWith('}'))) {
                            break;
                        }
                    }
                    
                    const definition = this.parseDefinition(currentType, currentName, lines, currentDefinitionStart, currentDefinitionBodyStart, i, currentLevel);
                    definitions.push(definition);
                    
                    currentDefinitionStart = l + 1;
                    currentDefinitionBodyStart = i;
                    currentType = definitionMatch.type;
                    currentName = definitionMatch.name;
                }
                
            } else {
                braceCount += (trimmedLine.match(/\{/g) || []).length - (trimmedLine.match(/\}/g) || []).length;
            }
        }

        //handle the last definition
        if (currentDefinitionStart !== -1 && currentType && currentName) {
            const definition = this.parseDefinition(currentType, currentName, lines, currentDefinitionStart, currentDefinitionBodyStart, lines.length, currentLevel);
            definitions.push(definition);
        }
        // correct first global namespace level
        if(hasGlobalNamespace) {
            definitions[0].level = definitions[0].level - 1;
        }
        return definitions;
    }


    private parseDefinition(definitionType: TypeSpecDefinitionType, definitionName: string, lines: string[], definitionStart: number, definitionBodyStart: number, nextDefinitionBodyStart: number, level: number): TypeSpecDefinition {
        let definitionEnd = -1;
        let inCommentBlock = false;
        /* skip the comments for the next definition if any. */
        for (let l = nextDefinitionBodyStart - 1; l > definitionStart; l--) {
            let trim = lines[l].trim();
            if (trim.endsWith('*/')) inCommentBlock = true;
            if (trim.startsWith('/*')) inCommentBlock = false;
            let isComment = inCommentBlock || trim.startsWith("//");
            if (!isComment && (trim.endsWith(';') || trim.endsWith('}'))) {
                definitionEnd = l;
                break;
            }
        }

        if (definitionEnd === -1) {
            const clampedNext = Math.min(nextDefinitionBodyStart -1, lines.length - 1);
            definitionEnd = Math.max(definitionStart, clampedNext);
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
        // parse out children
        let children: TypeSpecDefinition[] = [];
        if (definitionType === 'namespace' /*|| definitionType === 'interface'*/) {
            children = this.parseTypeSpecDefinitionsHelper(lines.slice(definitionBodyStart + 1, definitionEnd), level + 1);
        }

        if (definitionType === 'interface') {
            children = this.parseInterfaceOperations(definitionType, definitionName, lines, definitionBodyStart, definitionEnd, level + 1);
            
        }
        const description = this.extractDescription(currentDecorators, blockCommentLines);
        return {
            type: definitionType,
            name: definitionName,
            fullName: definitionName, //TODO: need to append interface name
            code: lines.slice(definitionStart, definitionEnd + 1).join('\n'),
            decorators: currentDecorators,
            description,
            comments: blockCommentLines,
            level: level,
            children: children.length > 0 ? children:  undefined
        };
    }

    /**
     * Parse operations from an interface definition.
     * Extracts individual operations including:
     * - Standard operations: @get op list(): Widget[];
     * - Operations with parameters: @get read(@path id: Widget.id): Widget | Error;
     * - Operations with body: @post create(@body widget: Widget): Widget | Error;
     * - Operations with custom routes: @route("{id}/analyze") @post analyze(@path id: Widget.id): string | Error;
     * - Template operations: createOrUpdate is ArmResourceCreateOrReplaceAsync<Employee>;
     * - Complex template operations with multi-line generics
     */
    private parseInterfaceOperations(definitionType: TypeSpecDefinitionType, definitionName: string, lines: string[], definitionStart: number, definitionEnd: number, level: number): TypeSpecDefinition[] {
        const operations: TypeSpecDefinition[] = [];
        if (definitionType !== 'interface') {
            return [];
        }
        
        
        let currentComments: string[] = [];
        let currentDecorators: string[] = [];
        let operationLines: string[] = [];
        let inBlockComment = false;
        let inDecoratorBlock = false;
        let inOperationBlock = false;
        let braceCount = 0;
        let angleCount = 0;
        let parenCount = 0;
        
        for (let i = definitionStart; i <= definitionEnd; i++) {
            const line = lines[i];
            const trimmed = line.trim();
            
            // Skip empty lines when not collecting operation
            if (!trimmed && !inOperationBlock && !inBlockComment && !inDecoratorBlock) {
                continue;
            }
            
            // Handle block comments /** ... */
            if (trimmed.startsWith('/**') && !inBlockComment) {
                inBlockComment = true;
                currentComments.push(trimmed);
                if (trimmed.endsWith('*/')) {
                    inBlockComment = false;
                }
                continue;
            }
            
            if (inBlockComment) {
                currentComments.push(trimmed);
                if (trimmed.endsWith('*/')) {
                    inBlockComment = false;
                }
                continue;
            }
            
            // Handle single-line comments
            if (trimmed.startsWith('//')) {
                currentComments.push(trimmed);
                continue;
            }
            
            // Handle decorators
            if (trimmed.startsWith('@') && !inOperationBlock) {
                inDecoratorBlock = true;
                currentDecorators.push(trimmed);
                parenCount = (trimmed.match(/\(/g) || []).length - (trimmed.match(/\)/g) || []).length;
                if (parenCount <= 0) {
                    inDecoratorBlock = false;
                }
                continue;
            }
            
            if (inDecoratorBlock) {
                currentDecorators[currentDecorators.length - 1] += '\n' + trimmed;
                parenCount += (trimmed.match(/\(/g) || []).length - (trimmed.match(/\)/g) || []).length;
                if (parenCount <= 0) {
                    inDecoratorBlock = false;
                }
                continue;
            }
            
            // Detect operation start patterns:
            // 1. "op <name>" - standard operation with op keyword
            // 2. "<name> is <TemplateName>" - template operation
            // 3. "<name>(" - operation without op keyword (shorthand in interfaces)
            // 4. "<name><...>(" - operation template with generic parameters
            const opStartMatch = trimmed.match(/^op\s+(\w+)/) || 
                                 trimmed.match(/^(\w+)\s+is\s+/) ||
                                 trimmed.match(/^(\w+)\s*</) ||
                                 trimmed.match(/^(\w+)\s*\(/);
            
            if (opStartMatch && !inOperationBlock) {
                inOperationBlock = true;
                operationLines = [line];
                
                // Count brackets to handle multi-line operations
                braceCount = (trimmed.match(/\{/g) || []).length - (trimmed.match(/\}/g) || []).length;
                angleCount = (trimmed.match(/</g) || []).length - (trimmed.match(/>/g) || []).length;
                parenCount = (trimmed.match(/\(/g) || []).length - (trimmed.match(/\)/g) || []).length;
                
                // Check if operation is complete on single line
                if (trimmed.endsWith(';') && braceCount <= 0 && angleCount <= 0 && parenCount <= 0) {
                    const operation = this.buildOperationDefinition(
                        opStartMatch[1],
                        operationLines,
                        currentDecorators,
                        currentComments,
                        level
                    );
                    operations.push(operation);
                    
                    // Reset state
                    currentComments = [];
                    currentDecorators = [];
                    operationLines = [];
                    inOperationBlock = false;
                    braceCount = 0;
                    angleCount = 0;
                    parenCount = 0;
                }
                continue;
            }
            
            // Continue collecting multi-line operation
            if (inOperationBlock) {
                operationLines.push(line);
                braceCount += (trimmed.match(/\{/g) || []).length - (trimmed.match(/\}/g) || []).length;
                angleCount += (trimmed.match(/</g) || []).length - (trimmed.match(/>/g) || []).length;
                parenCount += (trimmed.match(/\(/g) || []).length - (trimmed.match(/\)/g) || []).length;
                
                // Operation complete when semicolon found and all brackets balanced
                if (trimmed.endsWith(';') && braceCount <= 0 && angleCount <= 0 && parenCount <= 0) {
                    const opName = this.extractOperationName(operationLines[0]);
                    const operation = this.buildOperationDefinition(
                        opName,
                        operationLines,
                        currentDecorators,
                        currentComments,
                        level
                    );
                    operations.push(operation);
                    
                    // Reset state
                    currentComments = [];
                    currentDecorators = [];
                    operationLines = [];
                    inOperationBlock = false;
                    braceCount = 0;
                    angleCount = 0;
                    parenCount = 0;
                }
            }
        }
        
        return operations;
    }
    
    /**
     * Extract operation name from the first line of an operation definition.
     */
    private extractOperationName(line: string): string {
        const trimmed = line.trim();
        
        // Match "op <name>" pattern
        const opMatch = trimmed.match(/^op\s+(\w+)/);
        if (opMatch) {
            return opMatch[1];
        }
        
        // Match "<name> is" pattern (template operations)
        const templateMatch = trimmed.match(/^(\w+)\s+is\s+/);
        if (templateMatch) {
            return templateMatch[1];
        }
        
        // Match "<name><" pattern (operation templates with generic parameters)
        const genericMatch = trimmed.match(/^(\w+)\s*</);
        if (genericMatch) {
            return genericMatch[1];
        }
        
        // Match "<name>(" pattern (shorthand operations without op keyword)
        const shorthandMatch = trimmed.match(/^(\w+)\s*\(/);
        if (shorthandMatch) {
            return shorthandMatch[1];
        }
        
        return 'unknown';
    }
    
    /**
     * Build a TypeSpecDefinition object for an operation.
     */
    private buildOperationDefinition(
        name: string,
        operationLines: string[],
        decorators: string[],
        comments: string[],
        level: number
    ): TypeSpecDefinition {
        const code = [...comments, ...decorators, ...operationLines].join('\n');
        const description = this.extractDescription(decorators, comments);
        const fullName = name; //TODO: need to combine namespace
        
        return {
            type: 'operation',
            name,
            fullName,
            code,
            decorators,
            description,
            comments,
            level: level,
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

        // Decorator definition: extern dec name ... or dec name ...
        const decoratorMatch = line.match(/^(?:extern\s+)?dec\s+(\w+)/);
        if (decoratorMatch) {
            return { type: 'decorator', name: decoratorMatch[1] };
        }

        return null;
    }

    /**
     * Generate Markdown content from parsed definitions.
     */
    private generateMarkdown(definitions: TypeSpecDefinition[], sourceFile: string, excluded: TypeSpecDefinitionType[]|undefined = undefined): string {
        const lines: string[] = [];

        // Generate chapters for each definition
        for (const def of definitions) {
            this.generateDefinition(def, excluded, lines);
            if (def.children) {
                for (const child of def.children) {
                    this.generateDefinition(child, excluded, lines);
                }
            }
        }

        return lines.join('\n');
    }

    private generateDefinition(definition: TypeSpecDefinition, excluded: TypeSpecDefinitionType[]|undefined = undefined, output: string[]): void {
        if (excluded && excluded.includes(definition.type)) return;
            let chapterHead: string = "";
            for (let i = 0; i < definition.level; i++) {
                chapterHead += "#";
            }
            output.push(`${chapterHead} ${definition.fullName ?? definition.name}`);
            output.push('');
            if (definition.type) output.push(`**Type:** ${this.capitalizeType(definition.type)}`);
            output.push('');
            
            // Add description as chapter body if available
            if (definition.description) {
                output.push(definition.description);
                output.push('');
            }
            
            output.push('```typespec');
            output.push(definition.code);
            output.push('```');
            output.push('');
            output.push('');
    }

    /**
     * Capitalize the type name for display.
     */
    private capitalizeType(type: string): string {
        return type.charAt(0).toUpperCase() + type.slice(1);
    }
}
