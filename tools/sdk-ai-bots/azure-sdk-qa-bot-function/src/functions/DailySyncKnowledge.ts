import { app, HttpRequest, HttpResponseInit, InvocationContext, Timer } from '@azure/functions';
import { execSync } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { StorageService } from '../services/StorageService';
import { SpectorCaseProcessor } from '../services/SpectorCaseProcessor';

/**
 * Daily sync knowledge function that processes documentation from various repositories
 * and uploads processed content to blob storage for the Azure SDK QA Bot.
 * 
 * This function:
 * 1. Clones/updates multiple documentation repositories
 * 2. Processes markdown files to extract content
 * 3. Uploads processed content to Azure Blob Storage
 * 4. Cleans up temporary files
 * 
 * Triggered daily via timer or manually via HTTP request
 */

// Configuration for documentation sources
interface DocumentationSource {
    path: string;
    folder: string;
    name?: string;
    fileNameLowerCase?: boolean;
}

// Configuration for repository setup
interface RepositoryConfig {
    name: string;
    url: string;
    path: string;
    branch?: string;
    sparseCheckout?: string[];
    authType?: 'public' | 'token' | 'ssh';
    sshHost?: string;
    token?: string;
}

const documentationSources: DocumentationSource[] = [
    {
        path: "docs/typespec/website/src/content/docs/docs",
        folder: "typespec_docs",
        name: "TypeSpec",
        fileNameLowerCase: true,
    },
    {
        path: "docs/typespec-azure/website/src/content/docs/docs",
        folder: "typespec_azure_docs",
        name: "TypeSpec Azure",
        fileNameLowerCase: true,
    },
    {
        path: "docs/azure-rest-api-specs.wiki",
        folder: "azure_rest_api_specs_wiki",
    },
    {
        path: "docs/azure-sdk-for-python.wiki",
        folder: "azure_sdk_for_python_wiki",
    },
    {
        path: "docs/azure-sdk-for-python/doc",
        folder: "azure_sdk_for_python_docs",
    },
    {
        path: "docs/api-guidelines",
        folder: "azure_api_guidelines",
    },
    {
        path: "docs/resource-provider-contract",
        folder: "azure_resource_manager_rpc",
    },
    {
        path: "docs/azure-sdk-docs-eng.ms",
        folder: "azure-sdk-docs-eng",
    },
    {
        path: "docs/azure-sdk",
        folder: "azure-sdk-guidelines",
    },
    {
        path: "docs/typespec/packages/http-specs/specs/generated",
        folder: "typespec_http_specs",
        name: "TypeSpec HTTP Spector Cases",
        fileNameLowerCase: true,
    },
    {
        path: "docs/typespec-azure/packages/azure-http-specs/specs/generated",
        folder: "typespec_azure_http_specs",
        name: "TypeSpec Azure HTTP Spector Cases",
        fileNameLowerCase: true,
    },
];

/**
 * Timer-triggered function that runs daily to sync knowledge base
 */
export async function dailySyncKnowledgeTimer(myTimer: Timer, context: InvocationContext): Promise<void> {
    context.log('Daily sync knowledge timer function started');
    
    try {
        await processDailySyncKnowledge(context);
        context.log('Daily sync knowledge completed successfully');
    } catch (error) {
        context.error('Daily sync knowledge failed:', error);
        throw error;
    }
}

/**
 * HTTP-triggered function for manual sync
 */
export async function dailySyncKnowledgeHttp(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    context.log('Daily sync knowledge HTTP function started');
    
    try {
        await processDailySyncKnowledge(context);
        
        return {
            status: 200,
            jsonBody: {
                message: 'Daily sync knowledge completed successfully',
                timestamp: new Date().toISOString()
            }
        };
    } catch (error) {
        context.error('Daily sync knowledge failed:', error);
        
        return {
            status: 500,
            jsonBody: {
                error: 'Daily sync knowledge failed',
                details: error instanceof Error ? error.message : 'Unknown error',
                timestamp: new Date().toISOString()
            }
        };
    }
}

/**
 * Core processing logic for daily sync knowledge
 */
async function processDailySyncKnowledge(context: InvocationContext): Promise<void> {
    const workingDir = '/tmp/daily-sync-work';
    const docsDir = path.join(workingDir, 'docs');
    const tempDocsDir = path.join(workingDir, 'temp_docs');
    
    try {
        // Create working directories
        if (fs.existsSync(workingDir)) {
            fs.rmSync(workingDir, { recursive: true, force: true });
        }
        fs.mkdirSync(workingDir, { recursive: true });
        fs.mkdirSync(docsDir, { recursive: true });
        
        context.log('Setting up documentation repositories...');
        
        // Setup documentation repositories
        await setupDocumentationRepositories(docsDir, context);
        
        context.log('Preprocessing spector cases...');
        
        // Preprocess spector cases
        await preprocessSpectorCases(docsDir, context);

        context.log('Processing documentation sources...');
        
        // Process each documentation source
        for (const source of documentationSources) {
            const sourceDir = path.join(workingDir, source.path);
            const targetDir = path.join(tempDocsDir, source.folder);
            
            if (!fs.existsSync(sourceDir)) {
                context.warn(`Source directory not found: ${sourceDir}`);
                continue;
            }
            
            // Create target directory
            fs.mkdirSync(targetDir, { recursive: true });
            
            // Create release notes index
            try {
                await createReleaseNotesIndex(source, sourceDir, targetDir, context);
            } catch (error) {
                context.error(`Error creating release notes index: ${error}`);
                throw error;
            }
            
            // Process files in source directory
            try {
                await processSourceDirectory(sourceDir, source, targetDir, context);
            } catch (error) {
                context.error(`Error processing source directory: ${error}`);
                throw error;
            }
        }
        
        // Upload files to blob storage
        const currentFiles = await uploadFilesToBlobStorage(tempDocsDir, documentationSources, context);
        
        // Clean up expired blobs
        await cleanupExpiredBlobs(currentFiles, context);
        
        context.log('Daily sync knowledge processing completed');
        
    } finally {
        // Cleanup working directory
        if (fs.existsSync(workingDir)) {
            fs.rmSync(workingDir, { recursive: true, force: true });
        }
    }
}

/**
 * Get authenticated URL for repository cloning
 */
function getAuthenticatedUrl(repo: RepositoryConfig, context: InvocationContext): string {
    if (repo.authType === 'public') {
        return repo.url;
    }
    
    if (repo.authType === 'token') {
        if (!repo.token) {
            context.error(`Token is missing for repository ${repo.name}. Please check environment variable.`);
            throw new Error(`Authentication token missing for ${repo.name}`);
        }
        context.log(`Using token authentication for ${repo.name}`);
        return repo.url.replace('https://', `https://${repo.token}@`);
    }
    
    if (repo.authType === 'ssh') {
        // SSH URLs should be used as-is, assuming SSH keys are configured
        context.log(`Using SSH authentication for ${repo.name} with host ${repo.sshHost || 'default'}`);
        return repo.url;
    }
    
    return repo.url;
}

/**
 * Setup SSH configuration for git operations (Windows and Linux compatible)
 */
async function setupSSHConfig(context: InvocationContext): Promise<void> {
    const sshPrivateKey = process.env.SSH_PRIVATE_KEY;
    
    if (!sshPrivateKey) {
        context.warn('SSH_PRIVATE_KEY environment variable not found');
        return;
    }
    
    try {
        // Determine home directory based on platform
        const homeDir = process.env.HOME;
        
        const sshDir = path.join(homeDir, '.ssh');
        
        // Create .ssh directory if it doesn't exist
        if (!fs.existsSync(sshDir)) {
            fs.mkdirSync(sshDir, { recursive: true });
        }
        
        // Set correct permissions on .ssh directory (700 = owner read/write/execute only)
        fs.chmodSync(sshDir, 0o700);
        
        // Decode and write the private key
        const privateKeyPath = path.join(sshDir, 'id_ed25519');
        const decodedKey = Buffer.from(sshPrivateKey, 'base64').toString('utf-8');
        
        fs.writeFileSync(privateKeyPath, decodedKey);
        
        // Set correct permissions on the private key file (600 = owner read/write only)
        fs.chmodSync(privateKeyPath, 0o600);
    
        // Create SSH config for github-microsoft
        const sshConfigPath = path.join(sshDir, 'config');
        const sshConfig = `# For cloud-and-ai-microsoft repositories
Host github-microsoft
    HostName github.com
    User git
    IdentityFile ${privateKeyPath.replace(/\\/g, '/')}
    IdentitiesOnly yes
    StrictHostKeyChecking no
    UserKnownHostsFile /dev/null

# For general GitHub repositories  
Host github.com
    HostName github.com
    User git
    StrictHostKeyChecking no
    UserKnownHostsFile /dev/null
`;
        
        fs.writeFileSync(sshConfigPath, sshConfig);
        
        // Set correct permissions on SSH config file (644 = owner read/write, group/others read only)
        fs.chmodSync(sshConfigPath, 0o644);
        
        context.log(`SSH configuration setup completed`);
        context.log(`SSH directory: ${sshDir}`);
        context.log(`Private key path: ${privateKeyPath}`);
        context.log(`SSH config path: ${sshConfigPath}`);
        
        // Set GIT_SSH_COMMAND environment variable to use the custom SSH config
        process.env.GIT_SSH_COMMAND = `ssh -F "${sshConfigPath}" -o StrictHostKeyChecking=no`;
        
        // Test SSH connection
        context.log('Testing SSH connection to github-microsoft...');
        try {
            const { execSync } = require('child_process');
            const testResult = execSync(`ssh -F "${sshConfigPath}" -T git@github-microsoft -o ConnectTimeout=10`, {
                stdio: 'pipe',
                timeout: 15000
            }).toString();
            context.log('SSH test result:', testResult);
        } catch (testError) {
            context.warn('SSH test failed (this might be normal):', testError.message);
        }
        
    } catch (error) {
        context.error('Error setting up SSH configuration:', error);
        throw error;
    }
}

/**
 * Setup documentation repositories by cloning or updating them
 */
async function setupDocumentationRepositories(docsDir: string, context: InvocationContext): Promise<void> {
    // Setup SSH configuration first
    await setupSSHConfig(context);
    
    const repositories = [
        {
            name: 'TypeSpec',
            url: 'https://github.com/microsoft/typespec.git',
            path: 'typespec',
            sparseCheckout: ['website/src/content/docs/docs', 'packages/http-specs/specs']
        },
        {
            name: 'TypeSpec Azure',
            url: 'https://github.com/Azure/typespec-azure.git',
            path: 'typespec-azure',
            sparseCheckout: ['website/src/content/docs/docs', 'packages/azure-http-specs/specs']
        },
        {
            name: 'Azure REST API Specs Wiki',
            url: 'https://github.com/Azure/azure-rest-api-specs.wiki.git',
            path: 'azure-rest-api-specs.wiki'
        },
        {
            name: 'Azure SDK for Python Wiki',
            url: 'https://github.com/Azure/azure-sdk-for-python.wiki.git',
            path: 'azure-sdk-for-python.wiki'
        },
        {
            name: 'Azure SDK for Python',
            url: 'https://github.com/Azure/azure-sdk-for-python.git',
            path: 'azure-sdk-for-python',
            sparseCheckout: ['doc']
        },
        {
            name: 'Azure API Guidelines',
            url: 'https://github.com/microsoft/api-guidelines.git',
            path: 'api-guidelines',
            branch: 'vNext',
            sparseCheckout: ['azure'],
        },
        {
            name: 'Azure Resource Provider Contract',
            url: 'git@github-microsoft:cloud-and-ai-microsoft/resource-provider-contract.git',
            path: 'resource-provider-contract',
            branch: 'master',
            sparseCheckout: ['v1.0'],
            authType: 'ssh',
            sshHost: 'github-microsoft'
        },
        {
            name: 'Azure SDK Engineering System',
            url: 'https://dev.azure.com/azure-sdk/internal/_git/azure-sdk-docs-eng.ms',
            path: 'azure-sdk-docs-eng.ms',
            branch: 'main',
            sparseCheckout: ['docs'],
            authType: 'token',
            token: process.env.AZURE_SDK_ENG_HUB_TOKEN
        },
        {
            name: 'Azure SDK',
            url: 'https://github.com/Azure/azure-sdk.git',
            path: 'azure-sdk',
            branch: 'main',
            sparseCheckout: ['docs'],
        }
    ] as RepositoryConfig[];
    
    for (const repo of repositories) {
        try {
            context.log(`Setting up ${repo.name}...`);
            const repoPath = path.join(docsDir, repo.path);
            
            // Get authenticated URL if required
            const cloneUrl = getAuthenticatedUrl(repo, context);
            
            if (fs.existsSync(repoPath)) {
                // Update existing repository
                process.chdir(repoPath);
                
                execSync('git fetch origin', { stdio: 'pipe', env: process.env });
                execSync('git reset --hard origin/main || git reset --hard origin/master', { stdio: 'pipe', env: process.env });
            } else {
                // Clone new repository
                process.chdir(docsDir);
                
                if (repo.sparseCheckout) {
                    // Use sparse checkout for large repositories
                    execSync(`git clone --filter=blob:none --sparse ${cloneUrl} ${repo.path}`, { stdio: 'pipe', env: process.env });
                    process.chdir(repoPath);
                    execSync('git config core.sparseCheckout true', { stdio: 'pipe', env: process.env });
                    
                    const sparseCheckoutFile = path.join(repoPath, '.git/info/sparse-checkout');
                    fs.writeFileSync(sparseCheckoutFile, repo.sparseCheckout.join('\n'));
                    
                    execSync('git checkout main || git checkout master', { stdio: 'pipe', env: process.env });
                } else {
                    execSync(`git clone ${cloneUrl} ${repo.path}`, { stdio: 'pipe', env: process.env });
                }
            }
            
            context.log(`${repo.name} setup completed`);
        } catch (error) {
            context.error(`Error setting up ${repo.name}:`, error);
            throw error;
        }
    }
}

/**
 * Process files in a source directory
 */
async function processSourceDirectory(
    sourceDir: string,
    source: DocumentationSource,
    targetDir: string,
    context: InvocationContext
): Promise<void> {
    
    function walkDirectory(dir: string): void {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        
        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            
            if (entry.isDirectory()) {
                walkDirectory(fullPath);
            } else if (entry.name.endsWith('.md') || entry.name.endsWith('.mdx')) {
                const relativePath = path.relative(sourceDir, fullPath);
                
                // Skip reference files and release notes
                if (relativePath.startsWith('reference') || entry.name.startsWith('release-')) {
                    continue;
                }
                
                try {
                    processMarkdownFile(fullPath, source, targetDir, sourceDir);
                } catch (error) {
                    context.error(`Error processing file ${fullPath}:`, error);
                    throw error;
                }
            }
        }
    }
    
    walkDirectory(sourceDir);
}

/**
 * Create release notes index with content from the 10 most recent release notes
 */
async function createReleaseNotesIndex(
    source: DocumentationSource,
    sourceDir: string,
    targetDir: string,
    context: InvocationContext
): Promise<void> {
    // Path to release notes directory
    const releaseNotesDir = path.join(sourceDir, 'release-notes');
    
    // Check if release notes directory exists
    if (!fs.existsSync(releaseNotesDir)) {
        context.log(`Release notes directory not found for ${source.folder}, skipping index creation`);
        return;
    }
    
    const releaseFiles: string[] = [];
    
    function walkReleaseNotes(dir: string): void {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        
        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            
            if (entry.isDirectory()) {
                walkReleaseNotes(fullPath);
            } else {
                // Match files with pattern release-YYYY-MM-DD.md or release-YYYY-MM-DD.mdx
                if (/release-\d{4}-\d{2}-\d{2}\.(md|mdx)$/.test(entry.name)) {
                    releaseFiles.push(fullPath);
                }
            }
        }
    }
    
    walkReleaseNotes(releaseNotesDir);
    
    // Sort files by date (newest first)
    releaseFiles.sort((a, b) => {
        const dateA = extractDateFromFilename(a);
        const dateB = extractDateFromFilename(b);
        return dateB.getTime() - dateA.getTime();
    });
    
    // Take only the 10 most recent files (or fewer if less than 10 exist)
    const maxFiles = 10;
    const recentReleaseFiles = releaseFiles.slice(0, Math.min(maxFiles, releaseFiles.length));
    
    // Create index content
    const indexTitle = `# ${source.folder} - Recent Version Release Notes\n`;
    const description = `This contains latest release version and changes of ${source.name || source.folder}\n\n`;
    let content = indexTitle + description;
    
    // Add content from each release note file
    for (const filePath of recentReleaseFiles) {
        try {
            const fileContent = fs.readFileSync(filePath, 'utf-8');
            
            // Get relative file path for building the release link
            const relFilePath = path.relative(sourceDir, filePath);
            
            // Prepare release link based on the source repository
            let releaseLink = '';
            if (source.folder === 'typespec_docs') {
                releaseLink = `https://typespec.io/docs/${relFilePath}`;
            } else if (source.folder === 'typespec_azure_docs') {
                releaseLink = `https://azure.github.io/typespec-azure/docs/${relFilePath}`;
            }
            
            // Remove file extension from link
            releaseLink = releaseLink.replace(/\.(md|mdx)$/, '');
            
            // Extract title, release date, and version from frontmatter
            const { title, releaseDate, version } = extractReleaseInfo(fileContent);
            
            // Create section header with extracted information and link
            let releaseHeader = `## [version-${title}-${releaseDate}](${releaseLink})\n`;
            if (version) {
                releaseHeader = `## [version-${title}-${releaseDate} (v${version})](${releaseLink})\n`;
            }
            
            // Extract and organize sections
            const sections = extractSections(fileContent);
            
            // Add the release header and sections to content
            content += releaseHeader + sections + '\n';
        } catch (error) {
            context.warn(`Error reading release note file ${filePath}: ${error}`);
            continue;
        }
    }
    
    // Create the index file in the target directory
    const indexPath = path.join(targetDir, 'version-release-notes-index.md');
    fs.writeFileSync(indexPath, content);
    
    context.log(`Created release notes index for ${source.folder}`);
}

/**
 * Extract date from a filename in the format release-YYYY-MM-DD.md
 */
function extractDateFromFilename(filePath: string): Date {
    const filename = path.basename(filePath);
    const match = filename.match(/release-(\d{4}-\d{2}-\d{2})/);
    
    if (!match) {
        return new Date(0); // Return epoch time if no match found
    }
    
    try {
        return new Date(match[1]);
    } catch {
        return new Date(0); // Return epoch time on error
    }
}

/**
 * Extract release info from release note frontmatter
 */
function extractReleaseInfo(content: string): { title: string; releaseDate: string; version: string } {
    let title = '';
    let releaseDate = '';
    let version = '';
    
    const frontmatterMatch = content.match(/^---\s*\n([\s\S]*?)\n---\s*/);
    if (!frontmatterMatch) {
        return { title, releaseDate, version };
    }
    
    const frontmatter = frontmatterMatch[1];
    const lines = frontmatter.split('\n');
    
    for (const line of lines) {
        const trimmedLine = line.trim();
        
        if (trimmedLine.startsWith('title:')) {
            title = trimmedLine.substring(6).trim().replace(/^["']|["']$/g, '');
        } else if (trimmedLine.startsWith('releaseDate:')) {
            releaseDate = trimmedLine.substring(12).trim();
        } else if (trimmedLine.startsWith('version:')) {
            version = trimmedLine.substring(8).trim().replace(/^["']|["']$/g, '');
        }
    }
    
    return { title, releaseDate, version };
}

/**
 * Process a single markdown file
 */
function processMarkdownFile(
    filePath: string,
    source: DocumentationSource,
    targetDir: string,
    sourceDir: string
): void {
    const content = fs.readFileSync(filePath, 'utf-8');
    
    // Process file content
    const processed = convertMarkdown(content, filePath, source, sourceDir);
    
    if (!processed.filename) {
        // If no filename was found in frontmatter, generate one from file path
        const relativePath = path.relative(sourceDir, filePath);
        processed.filename = relativePath.replace(/[/\\]/g, '#');
        
        if (source.folder === 'azure-sdk-guidelines') {
            return; // Skip processing empty filename case for azure-sdk-guidelines
        }
    }
    
    // Create target file and write processed content
    const targetFilePath = path.join(targetDir, processed.filename);
    fs.writeFileSync(targetFilePath, processed.content);
}

/**
 * Convert markdown similar to Go version
 */
function convertMarkdown(content: string, filePath: string, source: DocumentationSource, sourceDir: string): { title: string; filename: string; content: string } {
    let title = '';
    let filename = '';
    let foundTitle = false;
    let inFrontmatter = false;
    let firstContentLine = true;
    
    const lines = content.split('\n');
    const contentLines: string[] = [];
    
    for (const line of lines) {
        // Process frontmatter
        if (line.trim() === '---') {
            if (!inFrontmatter) {
                inFrontmatter = true;
                continue;
            } else {
                inFrontmatter = false;
                continue;
            }
        }
        
        if (inFrontmatter) {
            if (line.startsWith('title:')) {
                title = line.substring(6).trim().replace(/^["']|["']$/g, '');
                foundTitle = true;
            }
            if (line.startsWith('permalink:')) {
                filename = line.substring(10).trim().replace(/^["']|["']$/g, '');
            }
            continue;
        }
        
        // Add title at the beginning of file content
        if (!inFrontmatter && firstContentLine) {
            if (foundTitle) {
                contentLines.push(`# ${title}`, '');
            }
            firstContentLine = false;
        }
        
        // Write non-empty lines or preserve empty lines in content
        if (!inFrontmatter) {
            contentLines.push(line);
        }
    }
    
    return {
        title,
        filename,
        content: contentLines.join('\n')
    };
}

/**
 * Extract sections and downgrade headers (kept for release notes processing)
 */
function extractSections(content: string): string {
    // Remove frontmatter
    const contentWithoutFrontmatter = content.replace(/^---\s*\n[\s\S]*?\n---\s*\n/, '');
    
    // Remove caution blocks
    const contentWithoutCaution = contentWithoutFrontmatter.replace(/:::caution[\s\S]*?:::\s*/g, '');
    
    // Downgrade headers (add one more # to each header)
    const downgradedContent = contentWithoutCaution.replace(/^(#+)\s+(.+)$/gm, '#$1 $2');
    
    return downgradedContent.trim();
}

/**
 * Upload files to blob storage
 */
async function uploadFilesToBlobStorage(tempDocsDir: string, sources: DocumentationSource[], context: InvocationContext): Promise<string[]> {
    try {
        const storageService = new StorageService();
        const containerName = process.env.STORAGE_KNOWLEDGE_CONTAINER || 'knowledge';
        
        const currentFiles: string[] = [];
        let uploadedCount = 0;
        for (const source of sources) {
            const targetDir = path.join(tempDocsDir, source.folder);
            
            if (!fs.existsSync(targetDir)) {
                context.warn(`Target directory not found: ${targetDir}`);
                continue;
            }
            
            function walkTargetDirectory(dir: string): void {
                const entries = fs.readdirSync(dir, { withFileTypes: true });
                
                for (const entry of entries) {
                    const fullPath = path.join(dir, entry.name);
                    
                    if (entry.isDirectory()) {
                        walkTargetDirectory(fullPath);
                    } else {
                        let fileName = entry.name;
                        if (source.fileNameLowerCase) {
                            fileName = fileName.toLowerCase().replace(/\s+/g, '-');
                        }
                        const blobPath = path.join(source.folder, fileName);
                        const content = fs.readFileSync(fullPath);
                        storageService.putBlob(containerName, blobPath, content);
                        uploadedCount++;
                        currentFiles.push(blobPath);
                    }
                }
            }
            
            walkTargetDirectory(targetDir);
        }
        
        context.log(`Successfully uploaded ${uploadedCount} files to blob storage`);
        return currentFiles;
    } catch (error) {
        context.error('Error uploading files to blob storage:', error);
        throw error;
    }
}

/**
 * Clean up expired blobs
 */
async function cleanupExpiredBlobs(currentFiles: string[], context: InvocationContext): Promise<void> {
    try {
        const storageService = new StorageService();
        const containerName = process.env.STORAGE_KNOWLEDGE_CONTAINER || 'knowledge';
        
        context.log('Cleaning up expired blobs...');
        
        const deletedCount = await storageService.deleteExpiredBlobs(containerName, currentFiles);
        
        context.log(`Cleaned up ${deletedCount} expired blobs`);
    } catch (error) {
        context.error('Error cleaning up expired blobs:', error);
        throw error;
    }
}

/**
 * Preprocess spector cases in the documentation directories
 */
async function preprocessSpectorCases(docsDir: string, context: InvocationContext): Promise<void> {
    context.log('Starting spector case processing...');
    
    try {
        await SpectorCaseProcessor.processSpectorCases(docsDir, context);
        context.log('Spector case processing completed successfully');
    } catch (error) {
        context.error('Error processing spector cases:', error);
        throw error;
    }
}

// Register the functions
app.timer('dailySyncKnowledgeTimer', {
    schedule: '0 0 9 * * *',
    handler: dailySyncKnowledgeTimer,
});

app.http('dailySyncKnowledgeHttp', {
    methods: ['GET'],
    authLevel: 'function',
    handler: dailySyncKnowledgeHttp,
});
