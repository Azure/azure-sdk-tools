import * as fs from 'fs';
import * as path from 'path';
import { InvocationContext } from '@azure/functions';
import { AzureOpenAI} from "openai";

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

interface ScenarioAnalysis {
    heading: string;
    description: string;
}

interface AnalysisResult {
    title: string;
    scenarios: ScenarioAnalysis[];
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
        const deploymentName = process.env.AOAI_CHAT_REASONING_MODEL;
        const apiVersion = "2024-12-01-preview";
        const apiKey = process.env.AOAI_CHAT_COMPLETIONS_API_KEY;
        const endpoint = process.env.AOAI_CHAT_COMPLETIONS_ENDPOINT;
        const options = { deploymentName, apiVersion, apiKey, endpoint }
        this.openAIClient = new AzureOpenAI(options);
    }
    /**
     * Process spector cases in the given directories
     */
    static async processSpectorCases(docsDir: string): Promise<void> {
        await this.initOpenAIClient();
        
        const results = await Promise.all([
            this.convertSpectorCasesToMarkdown(
                path.join(docsDir, 'typespec/packages/http-specs/specs'),
                path.join(docsDir, 'typespec/packages/http-specs/specs/generated')
            ),
            this.convertSpectorCasesToMarkdown(
                path.join(docsDir, 'typespec-azure/packages/azure-http-specs/specs'),
                path.join(docsDir, 'typespec-azure/packages/azure-http-specs/specs/generated')
            )
        ]);
        
        for (const result of results) {
            if (result.error) {
                console.error(`Error processing specs: ${result.error.message}`);
            }
        }

        console.log('Spector case processing completed');
    }
    
    /**
     * Convert spector cases to markdown for a specific directory
     */
    private static async convertSpectorCasesToMarkdown(
        root: string,
        targetRoot: string
    ): Promise<ConvertResult> {
        if (!fs.existsSync(root)) {
            console.error(`Spector specs directory not found: ${root}`);
            throw new Error(`Spector specs directory not found: ${root}`);
        }

        console.log(`Contents of folder: ${root}`);

        try {
            const { specs, paths } = this.getSpecs(root);

            // Process specs with concurrency limit
            const results: Promise<void>[] = [];
            for (let i = 0; i < specs.length; i++) {
                const spec = specs[i];
                const specPath = paths[i];
                
                results.push(this.processSpecFile(spec, specPath, root, targetRoot));
                
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
            console.error(`Error processing specs in ${root}:`, error);
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
        targetRoot: string
    ): Promise<void> {
        try {
            const dir = path.dirname(specPath);
            const relativeDir = path.relative(root, dir);
            console.log(`Processing spec path: ${relativeDir}`);

            const scenarios = this.getScenarios('@scenario\n', spec);
            if (scenarios.length === 0) {
                console.log(`No scenarios found in spec path: ${relativeDir}, skipping.`);
                return;
            }
            const doc = await this.createMarkdownDoc(scenarios, spec);
            
            // Create target directory if it doesn't exist
            const targetDir = path.join(targetRoot, relativeDir);
            const targetPath = this.getTargetPath(specPath, targetDir);
            
            await this.save(doc, targetDir, targetPath);
        } catch (error) {
            console.error(`Error processing spec file ${specPath}:`, error);
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
        targetPath: string
    ): Promise<void> {
        try {
            if (!fs.existsSync(targetDir)) {
                fs.mkdirSync(targetDir, { recursive: true });
            }
            
            fs.writeFileSync(targetPath, doc, 'utf-8');
            console.log(`Saved markdown to: ${targetPath}`);
        } catch (error) {
            console.error(`Error writing file ${targetPath}:`, error);
            throw error;
        }
    }
    
    /**
     * Create markdown document from scenarios and spec content
     */
    private static async createMarkdownDoc(
        scenarios: string[],
        spec: string
    ): Promise<string> {
        try {
            // Single comprehensive call to process all scenarios at once
            const analysisResult = await this.analyzeScenariosAndSpec(scenarios, spec);
            
            let doc = `# Usages for ${analysisResult.title}\n\n`;
            
            // Process each scenario with the pre-analyzed data
            for (let i = 0; i < scenarios.length; i++) {
                const scenario = scenarios[i];
                const scenarioData = analysisResult.scenarios[i];
                
                const cleanedScenario = this.removeSpectorContent(scenario);
                const finalDescription = scenarioData.description === scenarioData.heading ? '' : scenarioData.description;
                
                const section = 
                    `## Scenario: ${scenarioData.heading}\n` +
                    `${finalDescription}\n` +
                    '``` typespec\n' +
                    `${cleanedScenario}\n` +
                    '```\n';
                
                doc += section + '\n';
            }
            
            const removed = this.removeSpectorContent(spec);
            doc += "## Full Sample: \n" +
                "``` typespec\n" +
                removed + "\n" +
                "```\n";
                
            return doc;
        } catch (error) {
            console.error('Error creating markdown document:', error);
            throw error;
        }
    }
    
    /**
     * Analyze scenarios and spec content in a single LLM call to reduce API calls
     */
    private static async analyzeScenariosAndSpec(
        scenarios: string[],
        spec: string
    ): Promise<AnalysisResult> {
        // Prepare scenarios with indices for reference
        const scenariosWithIndex = scenarios.map((scenario, index) => 
            `=== SCENARIO ${index + 1} ===\n${scenario}\n`
        ).join('\n');

        const prompt = `Analyze the following TypeSpec content and scenarios to extract structured information.

            MAIN SPEC CONTENT:
            ${spec}

            SCENARIOS:
            ${scenariosWithIndex}

            Please provide a JSON response with the following structure:
            {
            "title": "A concise title from @scenarioService or @doc that is closest to @scenarioService (one line only, no extra characters)",
            "scenarios": [
                {
                    "heading": "Title for scenario 1 from @scenarioDoc or @doc (one line, no 'expected' test results)",
                    "description": "Description from @scenarioDoc or @doc (one or more paragraphs, exclude 'expected' test results, include clarifying details)"
                },
                {
                    "heading": "Title for scenario 2...",
                    "description": "Description for scenario 2..."
                }
            ]
            }

            Requirements:
            - Extract title from @scenarioService or @doc closest to @scenarioService only
            - For each scenario, extract heading and description from @scenarioDoc or @doc
            - Headings should be one line suitable for markdown headers
            - Descriptions should exclude 'expected' test results but include clarifying details
            - If description is same as heading, make description empty string
            - Provide exactly ${scenarios.length} scenario objects in the response
            - Return only valid JSON, no additional text or formatting`;

        const response = await this.getChatCompletions(prompt);
        
        // Parse the JSON response
        let analysisResult: AnalysisResult;
        try {
            // Clean the response to ensure it's valid JSON
            let cleanResponse = response.trim();
            
            // Remove any markdown code block formatting if present
            if (cleanResponse.startsWith('```json')) {
                cleanResponse = cleanResponse.replace(/^```json\s*/, '').replace(/\s*```$/, '');
            } else if (cleanResponse.startsWith('```')) {
                cleanResponse = cleanResponse.replace(/^```\s*/, '').replace(/\s*```$/, '');
            }
            
            analysisResult = JSON.parse(cleanResponse);
        } catch (parseError) {
            console.error('Failed to parse LLM response as JSON:', parseError);
            console.error('Raw response:', response);
            throw new Error(`Invalid JSON response from LLM: ${parseError}`);
        }
        
        // Validate the response structure
        if (!analysisResult.title || !Array.isArray(analysisResult.scenarios)) {
            throw new Error('Invalid response structure from LLM');
        }
        
        if (analysisResult.scenarios.length !== scenarios.length) {
            throw new Error(`Expected ${scenarios.length} scenarios, got ${analysisResult.scenarios.length}`);
        }
        
        return analysisResult;
    }

    /**
     * Get chat completions from OpenAI with retry logic
     */
    private static async getChatCompletions(
        question: string
    ): Promise<string> {
        if (!this.openAIClient) {
            throw new Error('OpenAI client not initialized');
        }
        
        const deploymentName = process.env.AOAI_CHAT_REASONING_MODEL!;
        
        // Implement retry logic with exponential backoff
        for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
            try {
                const response = await this.openAIClient.chat.completions.create({
                    messages:[
                            {
                                role: 'system',
                                content: 'You are a TypeSpec expert assistant. Extract structured information from TypeSpec files and return only valid JSON responses as requested.'
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
                        console.log(`Successfully completed request after ${attempt} retries`);
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
                        
                        console.log(`Rate limit hit (attempt ${attempt + 1}/${MAX_RETRIES + 1}), retrying after ${delay}ms...`);
                        await new Promise(resolve => setTimeout(resolve, delay));
                        continue;
                    }
                }
                
                // For non-429 errors or after max retries, throw the error
                console.error(`ERROR after ${attempt + 1} attempts:`, error);
                throw error;
            }
        }
        
        throw new Error(`Failed to get valid response after ${MAX_RETRIES + 1} attempts`);
    }
    
    /**
     * Get specs from directory
     */
    private static getSpecs(root: string): { specs: string[], paths: string[] } {
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
                        console.log(`Ignoring spec path: ${fullPath}`);
                        continue;
                    }
                    
                    try {
                        console.log(`Found spec path: ${fullPath}`);
                        const content = fs.readFileSync(fullPath, 'utf-8');
                        specs.push(content);
                        paths.push(fullPath);
                    } catch (error) {
                        console.error(`Failed to read file ${fullPath}:`, error);
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
