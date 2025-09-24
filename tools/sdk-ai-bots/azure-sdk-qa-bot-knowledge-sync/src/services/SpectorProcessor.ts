import * as fs from 'fs';
import * as path from 'path';
import { InvocationContext } from '@azure/functions';

/**
 * Preprocessor for TypeSpec spector cases
 * Processes generated test case files and converts them to markdown format
 */

/**
 * Process spector cases in a directory
 */
export async function preprocessSpectorCases(specsDir: string, context: InvocationContext): Promise<void> {
    if (!fs.existsSync(specsDir)) {
        context.warn(`Spector specs directory not found: ${specsDir}`);
        return;
    }
    
    const generatedDir = path.join(specsDir, 'generated');
    if (!fs.existsSync(generatedDir)) {
        context.warn(`Generated specs directory not found: ${generatedDir}`);
        return;
    }
    
    context.log(`Processing spector cases in: ${generatedDir}`);
    
    processSpectorDirectory(generatedDir, context);
}

/**
 * Recursively process spector case files
 */
function processSpectorDirectory(dir: string, context: InvocationContext): void {
    const entries = fs.readdirSync(dir, { withFileTypes: true });
    
    for (const entry of entries) {
        const fullPath = path.join(dir, entry.name);
        
        if (entry.isDirectory()) {
            processSpectorDirectory(fullPath, context);
        } else if (entry.name.endsWith('.json')) {
            try {
                processSpectorFile(fullPath, context);
            } catch (error) {
                context.error(`Error processing spector file ${fullPath}:`, error);
            }
        }
    }
}

/**
 * Process a single spector case JSON file
 */
function processSpectorFile(filePath: string, context: InvocationContext): void {
    const content = fs.readFileSync(filePath, 'utf-8');
    
    try {
        const spectorData = JSON.parse(content);
        const markdownContent = convertSpectorToMarkdown(spectorData, path.basename(filePath));
        
        // Replace .json extension with .md
        const markdownPath = filePath.replace(/\.json$/, '.md');
        fs.writeFileSync(markdownPath, markdownContent);
        
        context.log(`Converted spector case: ${path.basename(filePath)} -> ${path.basename(markdownPath)}`);
    } catch (error) {
        context.error(`Error parsing JSON in ${filePath}:`, error);
    }
}

/**
 * Convert spector case data to markdown format
 */
function convertSpectorToMarkdown(spectorData: any, fileName: string): string {
    let markdown = `# ${fileName.replace('.json', '')}\n\n`;
    
    // Add metadata if available
    if (spectorData.metadata) {
        markdown += '## Metadata\n\n';
        markdown += '```json\n';
        markdown += JSON.stringify(spectorData.metadata, null, 2);
        markdown += '\n```\n\n';
    }
    
    // Add description if available
    if (spectorData.description) {
        markdown += '## Description\n\n';
        markdown += `${spectorData.description}\n\n`;
    }
    
    // Add request information
    if (spectorData.request) {
        markdown += '## Request\n\n';
        markdown += '```json\n';
        markdown += JSON.stringify(spectorData.request, null, 2);
        markdown += '\n```\n\n';
    }
    
    // Add response information
    if (spectorData.response) {
        markdown += '## Response\n\n';
        markdown += '```json\n';
        markdown += JSON.stringify(spectorData.response, null, 2);
        markdown += '\n```\n\n';
    }
    
    // Add any test cases
    if (spectorData.testCases && Array.isArray(spectorData.testCases)) {
        markdown += '## Test Cases\n\n';
        
        spectorData.testCases.forEach((testCase: any, index: number) => {
            markdown += `### Test Case ${index + 1}\n\n`;
            markdown += '```json\n';
            markdown += JSON.stringify(testCase, null, 2);
            markdown += '\n```\n\n';
        });
    }
    
    // Add any additional properties
    const knownProperties = ['metadata', 'description', 'request', 'response', 'testCases'];
    const additionalProperties = Object.keys(spectorData).filter(key => !knownProperties.includes(key));
    
    if (additionalProperties.length > 0) {
        markdown += '## Additional Information\n\n';
        
        additionalProperties.forEach(prop => {
            markdown += `### ${prop}\n\n`;
            markdown += '```json\n';
            markdown += JSON.stringify(spectorData[prop], null, 2);
            markdown += '\n```\n\n';
        });
    }
    
    return markdown;
}
