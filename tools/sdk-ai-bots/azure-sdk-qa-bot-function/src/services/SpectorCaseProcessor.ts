import * as fs from 'fs';
import * as path from 'path';
import { InvocationContext } from '@azure/functions';
import { AzureOpenAI} from "openai";
import { DefaultAzureCredential, getBearerTokenProvider } from "@azure/identity";

// Retry configuration for OpenAI API calls
const MAX_RETRIES = 5;
const BASE_DELAY = 2000; // 2 seconds in milliseconds
const MAX_DELAY = 60000; // 60 seconds in milliseconds
const MAX_CONCURRENT_COUNT = 5;
const IGNORED_SPECS = ['special-words'];

interface ConvertResult {
    count: number;
    error?: Error;
}

/**
 * Spector case processor for TypeSpec HTTP specifications
 * Converts TypeSpec (.tsp) files with @scenario annotations to markdown documentation
 * Uses OpenAI to generate meaningful titles and descriptions
 */
export class SpectorCaseProcessor {
    private static openAIClient: AzureOpenAI | null = null;
    
    /**
     * Initialize OpenAI client
     */
    private static async initOpenAIClient(): Promise<void> {
        const deploymentName = process.env.AZURE_OPENAI_DEPLOYMENT_NAME;
        const apiVersion = "2024-12-01-preview";
        const options = { deploymentName, apiVersion }
        this.openAIClient = new AzureOpenAI(options);
    }
    /**
     * Process spector cases in the given directories
     */
    static async processSpectorCases(docsDir: string, context: InvocationContext): Promise<void> {
        await this.initOpenAIClient();
        
        const results = await Promise.all([
            this.convertSpectorCasesToMarkdown(
                path.join(docsDir, 'typespec/packages/http-specs/specs'),
                path.join(docsDir, 'typespec/packages/http-specs/specs/generated'),
                context
            ),
            this.convertSpectorCasesToMarkdown(
                path.join(docsDir, 'typespec-azure/packages/azure-http-specs/specs'),
                path.join(docsDir, 'typespec-azure/packages/azure-http-specs/specs/generated'),
                context
            )
        ]);
        
        for (const result of results) {
            if (result.error) {
                context.error(`Error processing specs: ${result.error.message}`);
            }
        }
        
        context.log('Spector case processing completed');
    }
    
    /**
     * Convert spector cases to markdown for a specific directory
     */
    private static async convertSpectorCasesToMarkdown(
        root: string,
        targetRoot: string,
        context: InvocationContext
    ): Promise<ConvertResult> {
        if (!fs.existsSync(root)) {
            context.error(`Spector specs directory not found: ${root}`);
            throw new Error(`Spector specs directory not found: ${root}`);
        }
        
        context.log(`Contents of folder: ${root}`);
        
        try {
            const { specs, paths } = this.getSpecs(root, context);
            
            // Process specs with concurrency limit
            const results: Promise<void>[] = [];
            for (let i = 0; i < specs.length; i++) {
                const spec = specs[i];
                const specPath = paths[i];
                
                results.push(this.processSpecFile(spec, specPath, root, targetRoot, context));
                
                // Limit concurrency
                if (results.length >= MAX_CONCURRENT_COUNT) {
                    await Promise.all(results);
                    results.length = 0;
                }
            }
            
            // Process remaining files
            if (results.length > 0) {
                await Promise.all(results);
            }
            
            return { count: specs.length };
        } catch (error) {
            context.error(`Error processing specs in ${root}:`, error);
            return { count: 0, error: error as Error };
        }
    }
    
    /**
     * Process a single spec file
     */
    private static async processSpecFile(
        spec: string,
        specPath: string,
        root: string,
        targetRoot: string,
        context: InvocationContext
    ): Promise<void> {
        try {
            const dir = path.dirname(specPath);
            const relativeDir = path.relative(root, dir);
            context.log(`Processing spec path: ${relativeDir}`);
            
            const scenarios = this.getScenarios('@scenario\n', spec);
            const doc = await this.createMarkdownDoc(scenarios, spec, context);
            
            // Create target directory if it doesn't exist
            const targetDir = path.join(targetRoot, relativeDir);
            const targetPath = this.getTargetPath(specPath, targetDir);
            
            await this.save(doc, targetDir, targetPath, context);
        } catch (error) {
            context.error(`Error processing spec file ${specPath}:`, error);
        }
    }
    
    /**
     * Get target path for generated markdown file
     */
    private static getTargetPath(sourcePath: string, targetDir: string): string {
        const originalFilename = path.basename(sourcePath);
        const markdownFilename = originalFilename.replace(/\.tsp$/, '.md');
        return path.join(targetDir, markdownFilename);
    }
    
    /**
     * Save markdown document to file
     */
    private static async save(
        doc: string,
        targetDir: string,
        targetPath: string,
        context: InvocationContext
    ): Promise<void> {
        try {
            if (!fs.existsSync(targetDir)) {
                fs.mkdirSync(targetDir, { recursive: true });
            }
            
            fs.writeFileSync(targetPath, doc, 'utf-8');
            context.log(`Saved markdown to: ${targetPath}`);
        } catch (error) {
            context.error(`Error writing file ${targetPath}:`, error);
            throw error;
        }
    }
    
    /**
     * Create markdown document from scenarios and spec content
     */
    private static async createMarkdownDoc(
        scenarios: string[],
        spec: string,
        context: InvocationContext
    ): Promise<string> {
        try {
            const title = await this.getChatCompletions(
                "Get a title from the @scenarioService or @doc that is closest to @scenarioService.\n" +
                "do not get title from other @doc. @doc for @scenarioService maybe not existed\n" +
                "the title will be used as markdown heading, so should be one line.\n" +
                "the reply should only contains the title, no extra characters\n" +
                "the below is the typespec content\n\n" +
                spec,
                context
            );
            
            let doc = `# Usages for ${title}\n\n`;
            
            for (const scenario of scenarios) {
                const scenarioMarkdownSection = await this.createScenarioSection(scenario, context);
                doc += scenarioMarkdownSection + '\n';
            }
            
            const removed = this.removeSpectorContent(spec);
            doc += "## Full Sample: \n" +
                "``` typespec\n" +
                removed + "\n" +
                "```\n";
                
            return doc;
        } catch (error) {
            context.error('Error creating markdown document:', error);
            throw error;
        }
    }
    
    /**
     * Get chat completions from OpenAI with retry logic
     */
    private static async getChatCompletions(
        question: string,
        context: InvocationContext
    ): Promise<string> {
        if (!this.openAIClient) {
            throw new Error('OpenAI client not initialized');
        }
        
        const deploymentName = process.env.AZURE_OPENAI_DEPLOYMENT_NAME!;
        
        // Implement retry logic with exponential backoff
        for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
            try {
                const response = await this.openAIClient.chat.completions.create({
                    messages:[
                            {
                                role: 'system',
                                content: 'You are a helpful assistant. And you are a great TypeSpec expert.'
                            },
                            {
                                role: 'user',
                                content: question
                            }
                        ],
                    model: deploymentName,
                });
                
                if (response.choices && response.choices.length > 0 && response.choices[0].message?.content) {
                    if (attempt > 0) {
                        context.log(`Successfully completed request after ${attempt} retries`);
                    }
                    return response.choices[0].message.content;
                }
                
                break; // If we got a response but no content, don't retry
            } catch (error: any) {
                // Check if this is a rate limit error (429)
                if (error?.status === 429 || error?.message?.includes('429') || error?.message?.includes('Too Many Requests')) {
                    if (attempt < MAX_RETRIES) {
                        // Calculate delay with exponential backoff
                        let delay = Math.min(BASE_DELAY * Math.pow(2, attempt), MAX_DELAY);
                        
                        context.log(`Rate limit hit (attempt ${attempt + 1}/${MAX_RETRIES + 1}), retrying after ${delay}ms...`);
                        await new Promise(resolve => setTimeout(resolve, delay));
                        continue;
                    }
                }
                
                // For non-429 errors or after max retries, throw the error
                context.error(`ERROR after ${attempt + 1} attempts:`, error);
                throw error;
            }
        }
        
        throw new Error(`Failed to get valid response after ${MAX_RETRIES + 1} attempts`);
    }
    
    /**
     * Get heading for a scenario
     */
    private static async getHeading(scenario: string, context: InvocationContext): Promise<string> {
        try {
            return await this.getChatCompletions(
                "Get a title from the @scenarioDoc or @doc.\n" +
                "the title will be used as markdown heading, so should be one line.\n" +
                "If the first line is good, just copy the first line in @scenarioDoc or @doc.\n" +
                "do not make the 'expected' test result in the title.\n" +
                "the reply should only contains the title, no extra characters\n" +
                "the below is the typespec content\n\n" +
                scenario,
                context
            );
        } catch (error) {
            context.error('Error getting heading:', error);
            return 'no-title';
        }
    }
    
    /**
     * Get description for a scenario
     */
    private static async getDescription(scenario: string, context: InvocationContext): Promise<string> {
        try {
            return await this.getChatCompletions(
                "Get (do not modify words) a description from the @scenarioDoc or @doc.\n" +
                "the description will be used in a markdown description, so should be one or more paragraphs.\n" +
                "If the first line is good, just copy the first line in @scenarioDoc or @doc.\n" +
                "do not make the 'expected' test result in the @scenarioDoc or @doc.\n" +
                "must contains the clarify or details other than the 'expected' test result\n" +
                "the reply should only contains the description, no extra characters\n" +
                "the below is the typespec content\n\n" +
                scenario,
                context
            );
        } catch (error) {
            context.error('Error getting description:', error);
            return 'no-description';
        }
    }
    
    /**
     * Create scenario section
     */
    private static async createScenarioSection(scenario: string, context: InvocationContext): Promise<string> {
        try {
            const heading = await this.getHeading(scenario, context);
            const description = await this.getDescription(scenario, context);
            
            const cleanedScenario = this.removeSpectorContent(scenario);
            const finalDescription = description === heading ? '' : description;
            
            const section = 
                `## Scenario: ${heading}\n` +
                `${finalDescription}\n` +
                '``` typespec\n' +
                `${cleanedScenario}\n` +
                '```\n';
                
            return section;
        } catch (error) {
            context.error('Error creating scenario section:', error);
            throw error;
        }
    }
    
    /**
     * Get specs from directory
     */
    private static getSpecs(root: string, context: InvocationContext): { specs: string[], paths: string[] } {
        const specs: string[] = [];
        const paths: string[] = [];
        
        const walkDir = (dir: string): void => {
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            
            for (const entry of entries) {
                const fullPath = path.join(dir, entry.name);
                
                if (entry.isDirectory()) {
                    walkDir(fullPath);
                } else if (entry.isFile() && path.extname(entry.name) === '.tsp') {
                    // Check if this spec should be ignored
                    const shouldIgnore = IGNORED_SPECS.some(ignored => fullPath.includes(ignored));
                    if (shouldIgnore) {
                        context.log(`Ignoring spec path: ${fullPath}`);
                        continue;
                    }
                    
                    try {
                        context.log(`Found spec path: ${fullPath}`);
                        const content = fs.readFileSync(fullPath, 'utf-8');
                        specs.push(content);
                        paths.push(fullPath);
                    } catch (error) {
                        context.error(`Failed to read file ${fullPath}:`, error);
                    }
                }
            }
        };
        
        walkDir(root);
        return { specs, paths };
    }
    
    /**
     * Find indexes of search string in spec
     */
    private static findIndexes(searchStr: string, spec: string): number[] {
        const findPreviousNewLine = (end: number): number => {
            for (let ind = end - 1; ind >= 1; ind--) {
                if (spec[ind] === '\n' && spec[ind - 1] === '\n') {
                    return ind + 1; // Return the index after the newline character
                }
            }
            return end;
        };
        
        const indexes: number[] = [];
        let startIndex = 0;
        
        while (true) {
            const pos = spec.indexOf(searchStr, startIndex);
            if (pos === -1) {
                break;
            }
            
            const blockStartIndex = findPreviousNewLine(pos);
            indexes.push(blockStartIndex);
            startIndex = pos + searchStr.length;
        }
        
        return indexes;
    }
    
    /**
     * Get scenarios from spec content
     */
    private static getScenarios(searchStr: string, spec: string): string[] {
        const indexes = this.findIndexes(searchStr, spec);
        const scenarios: string[] = [];
        
        for (let i = 0; i < indexes.length; i++) {
            const startIndex = indexes[i];
            const endIndex = i < indexes.length - 1 ? indexes[i + 1] : spec.length;
            const scenarioContent = spec.substring(startIndex, endIndex);
            scenarios.push(scenarioContent);
        }
        
        return scenarios;
    }
    
    /**
     * Remove spector content from TypeSpec content
     */
    private static removeSpectorContent(content: string): string {
        let cleanedContent = content;
        
        // Regular expressions to match various spector annotations
        // Using string replacement methods for better compatibility
        
        // @scenarioDoc("...") patterns
        cleanedContent = cleanedContent.replace(/@scenarioDoc\("[\s\S]*?"\)\n/g, '');
        cleanedContent = cleanedContent.replace(/@scenarioDoc\("""[\s\S]*?"""\)\n/g, '');
        
        // @scenarioService patterns
        cleanedContent = cleanedContent.replace(/@scenarioService\("[\s\S]*?"\)\n/g, '');
        cleanedContent = cleanedContent.replace(/@scenarioService\(\n[\s\S]*?\n\)\n/g, '');
        
        // Other patterns
        cleanedContent = cleanedContent.replace(/@scenario/g, '');
        cleanedContent = cleanedContent.replace(/import "@typespec\/spector";\n/g, '');
        cleanedContent = cleanedContent.replace(/using Spector;\n/g, '');
        
        // Remove lines containing #suppress and missing-scenario
        const lines = cleanedContent.split('\n');
        const cleanedLines = lines.filter(line => 
            !line.includes('#suppress ') && !line.includes('missing-scenario')
        );
        
        return cleanedLines.join('\n');
    }
}
