import { execSync } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { BlobService } from './services/StorageService';
import { SpectorCaseProcessor } from './services/SpectorCaseProcessor';
import { ConfigurationLoader, RepositoryConfig, DocumentationSource, Metadata } from './services/ConfigurationLoader';
import { SearchService } from './services/SearchService';
import { MetadataResolver } from './services/MetadataResolver';
import { TypeSpecProcessor } from './services/TypeSpecProcessor';

/**
 * Daily sync knowledge function that processes documentation from various repositories
 * and uploads processed content to blob storage for the Azure SDK QA Bot.
 * 
 * This function:
 * 1. Loads configuration from knowledge-config.json
 * 2. Clones/updates multiple documentation repositories
 * 3. Processes markdown files to extract content
 * 4. Uploads processed content to Azure Blob Storage
 * 5. Cleans up temporary files
 * 
 * Triggered daily via timer or manually via HTTP request
 */


// Model for processed markdown file result
interface ProcessedMarkdownFile {
    filename: string;
    content: string;
    blobPath: string;
    isValid: boolean; // Indicates if the file should be processed further
    metadata?: Metadata;
}

// Result interface for source directory processing
interface ProcessSourceDirectoryResult {
    totalProcessed: number;
    changedDocuments: number;
    unchangedDocuments: number;
    changedFiles: ProcessedMarkdownFile[];  // Files that changed and need to be uploaded/updated
    metadataChangedFiles: ProcessedMarkdownFile[]; // Files with metadata-only changes - upload only, no index deletion
    unchangedFiles: ProcessedMarkdownFile[]; // Files that didn't change
}

/**
 * Core processing logic for daily sync knowledge
 */
export async function processDailySyncKnowledge(): Promise<void> {
    const workingDir = '/tmp/daily-sync-work';
    const docsDir = path.join(workingDir, 'docs');
    const tempDocsDir = path.join(workingDir, 'temp_docs');
    
    // Initialize services
    const blobService = new BlobService();
    const searchService = new SearchService();
    
    try {
        // Load configuration
        console.log('Loading knowledge configuration...');
        const documentationSources = ConfigurationLoader.getDocumentationSources();
        
        // Create working directories
        if (fs.existsSync(workingDir)) {
            fs.rmSync(workingDir, { recursive: true, force: true });
        }
        fs.mkdirSync(workingDir, { recursive: true });
        fs.mkdirSync(docsDir, { recursive: true });

        console.log('Setting up documentation repositories...');
        
        // Setup documentation repositories
        await setupDocumentationRepositories(docsDir);

        console.log('Preprocessing spector cases...');
        
        // Preprocess spector cases
        await preprocessSpectorCases(docsDir);

        console.log(`processing typespec-azure-resource-manager library`);
        processTypeSpec(docsDir, "typespec-azure/packages/typespec-azure-resource-manager/lib");

        console.log('Processing documentation sources...');
        
        console.log('Loading existing blob metadata for change detection...');
        // Load existing blob metadata for change detection
        const existingBlobs = await blobService.listBlobs();

        let totalProcessed = 0;
        let changedDocuments = 0;
        let unchangedDocuments = 0;
        const allChangedFiles: ProcessedMarkdownFile[] = [];
        const allMetadataChangedFiles: ProcessedMarkdownFile[] = [];
        const allUnchangedFiles: ProcessedMarkdownFile[] = [];
        
        // Process each documentation source
        for (const source of documentationSources) {
            const sourceDir = path.join(workingDir, source.path);
            const targetDir = path.join(tempDocsDir, source.folder);
            
            if (!fs.existsSync(sourceDir)) {
                console.warn(`Source directory not found: ${sourceDir}`);
                continue;
            }
            
            // Create target directory
            fs.mkdirSync(targetDir, { recursive: true });
            
            // Create release notes index
            try {
                await createReleaseNotesIndex(source, sourceDir, targetDir);
            } catch (error) {
                console.error(`Error creating release notes index: ${error}`);
                throw error;
            }
            
            // Process files in source directory
            try {
                const result = await processSourceDirectory(
                    sourceDir, 
                    source, 
                    targetDir, 
                    existingBlobs,
                    blobService
                );
                
                totalProcessed += result.totalProcessed;
                changedDocuments += result.changedDocuments;
                unchangedDocuments += result.unchangedDocuments;
                allMetadataChangedFiles.push(...result.metadataChangedFiles);
                allChangedFiles.push(...result.changedFiles);
                allUnchangedFiles.push(...result.unchangedFiles);
            } catch (error) {
                console.error(`Error processing source directory: ${error}`);
                throw error;
            }
        }

        console.log(`Processing completed: ${totalProcessed} total, ${changedDocuments} changed, ${unchangedDocuments} unchanged`);
        console.log(`Files that changed: ${allChangedFiles.length}, Files that remained unchanged: ${allUnchangedFiles.length}`);

        // Delete the AI Search index for changed files
        await deleteAISearchIndex(searchService, allChangedFiles);

        // Upload files to blob storage (only for changed documents)
        await uploadFilesToBlobStorage(allChangedFiles.concat(allMetadataChangedFiles));
        
        // Clean up expired blobs
        await cleanupExpiredBlobs(allChangedFiles.concat(allUnchangedFiles).concat(allMetadataChangedFiles));
        console.log('Daily sync knowledge processing completed');

    } finally {
        // Cleanup working directory
        if (fs.existsSync(workingDir)) {
            fs.rmSync(workingDir, { recursive: true, force: true });
        }
    }
}

/**
 * Delete AI Search index for changed files
 */
async function deleteAISearchIndex(searchService: SearchService, changeFiles: ProcessedMarkdownFile[]) {
    for (const processed of changeFiles) {
        // Delete existing chunks from AI Search if document title exists
        try {
            await searchService.deleteDocumentChunksByFileName(processed.filename);
            console.log(`Deleted AI search chunks for: "${processed.blobPath}"`);
        } catch (error) {
            console.warn(`Failed to delete chunks for: "${processed.blobPath}": ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }
}

/**
 * Get authenticated URL for repository cloning
 */
function getAuthenticatedUrl(repo: RepositoryConfig): string {
    if (repo.authType === 'public') {
        return repo.url;
    }
    
    if (repo.authType === 'token') {
        if (!repo.token) {
            console.error(`Token is missing for repository ${repo.name}. Please check environment variable.`);
            throw new Error(`Authentication token missing for ${repo.name}`);
        }
        console.log(`Using token authentication for ${repo.name}`);
        return repo.url.replace('https://', `https://${repo.token}@`);
    }
    
    if (repo.authType === 'ssh') {
        // SSH URLs should be used as-is, assuming SSH keys are configured
        console.log(`Using SSH authentication for ${repo.name} with host ${repo.sshHost || 'default'}`);
        return repo.url;
    }

    if (repo.authType === 'local') {
        repo.url = repo.localPath;
    }
    
    return repo.url;
}

/**
 * Setup SSH configuration for git operations (Windows and Linux compatible)
 */
async function setupSSHConfig(): Promise<void> {
    const sshPrivateKey = process.env.SSH_PRIVATE_KEY;
    
    if (!sshPrivateKey) {
        console.warn('SSH_PRIVATE_KEY environment variable not found');
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

        console.log(`SSH configuration setup completed`);
        console.log(`SSH directory: ${sshDir}`);
        console.log(`Private key path: ${privateKeyPath}`);
        console.log(`SSH config path: ${sshConfigPath}`);

        // Set GIT_SSH_COMMAND environment variable to use the custom SSH config
        process.env.GIT_SSH_COMMAND = `ssh -F "${sshConfigPath}" -o StrictHostKeyChecking=no`;
        
        // Test SSH connection
        console.log('Testing SSH connection to github-microsoft...');
        try {
            const { execSync } = require('child_process');
            const testResult = execSync(`ssh -F "${sshConfigPath}" -T git@github-microsoft -o ConnectTimeout=10`, {
                stdio: 'pipe',
                timeout: 15000
            }).toString();
            console.log('SSH test result:', testResult);
        } catch (testError) {
            console.warn('SSH test failed (this might be normal):', testError.message);
        }
        
    } catch (error) {
        console.error('Error setting up SSH configuration:', error);
        throw error;
    }
}

/**
 * Setup documentation repositories by cloning or updating them
 */
async function setupDocumentationRepositories(docsDir: string): Promise<void> {
    // Configure git to use HTTP/1.1 globally to avoid HTTP/2 stream issues
    try {
        execSync('git config --global http.version HTTP/1.1', { stdio: 'pipe' });
        console.log('Configured git to use HTTP/1.1 globally');
    } catch (error) {
        console.warn('Failed to configure git HTTP version, continuing anyway:', error);
    }
    
    // Setup SSH configuration first
    await setupSSHConfig();
    
    // Load repository configurations from the config file
    const repositories = ConfigurationLoader.getRepositoryConfigs();
    
    for (const repo of repositories) {
        try {
            console.log(`Setting up ${repo.name}...`);
            const repoPath = path.join(docsDir, repo.path);
            
            // Get authenticated URL if required
            const cloneUrl = getAuthenticatedUrl(repo);

            if (repo.authType === 'local') {
                // Copy from cloneUrl (local path)
                for (const folder of repo.sparseCheckout || []) {
                    const srcPath = path.join(cloneUrl, folder);
                    const destPath = path.join(repoPath, folder);
                    
                    // Ensure parent directory exists
                    const parentDir = path.dirname(destPath);
                    if (!fs.existsSync(parentDir)) {
                        fs.mkdirSync(parentDir, { recursive: true });
                    }
                    
                    fs.cpSync(srcPath, destPath, { recursive: true });
                }
            } else {
                // Clone from remote URL
                process.chdir(docsDir);
                
                if (repo.sparseCheckout) {
                    // Use sparse checkout for large repositories
                    execSync(`git clone --filter=blob:none --sparse ${cloneUrl} ${repo.path}`, { stdio: 'pipe', env: process.env });
                    process.chdir(repoPath);
                    execSync('git config core.sparseCheckout true', { stdio: 'pipe', env: process.env });
                    
                    const sparseCheckoutFile = path.join(repoPath, '.git/info/sparse-checkout');
                    fs.writeFileSync(sparseCheckoutFile, repo.sparseCheckout.join('\n'));

                    execSync(`git checkout ${repo.branch}`, { stdio: 'pipe', env: process.env });
                } else {
                    execSync(`git clone ${cloneUrl} ${repo.path}`, { stdio: 'pipe', env: process.env });
                }
            }
            console.log(`${repo.name} setup completed`);
        } catch (error) {
            console.error(`Error setting up ${repo.name}:`, error);
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
    existingBlobs: Map<string, any>,
    blobService: BlobService
): Promise<ProcessSourceDirectoryResult> {
    
    let totalProcessed = 0;
    let changedDocuments = 0;
    let unchangedDocuments = 0;
    const changedFiles: ProcessedMarkdownFile[] = [];
    const metadataChangedFiles: ProcessedMarkdownFile[] = [];
    const unchangedFiles: ProcessedMarkdownFile[] = [];
    
    // Helper function to process a single file
    const processSingleFile = (fullPath: string, fileSourceDir: string): void => {
        totalProcessed++;
        
        try {
            const processed = processMarkdownFile(fullPath, source, fileSourceDir);
            
            if (!processed.isValid) {
                return;
            }
            // Prepare blob metadata for comparison
            let blobMetadata: Record<string, string> | undefined;
            if (processed.metadata) {
                blobMetadata = {
                    scope: processed.metadata.scope
                };
                if (processed.metadata.service_type) {
                    blobMetadata.service_type = processed.metadata.service_type;
                }
            }

            // Check if content or metadata has changed
            const contentChanged = blobService.hasContentChanged(processed.blobPath, processed.content, existingBlobs);
            const metadataChanged = blobService.hasMetadataChanged(processed.blobPath, blobMetadata, existingBlobs);

            if (contentChanged || metadataChanged) {
                if (contentChanged) {
                    console.log(`Content changed for: ${processed.blobPath}`);
                    changedDocuments++;
                    changedFiles.push(processed);
                    
                    const targetFilePath = path.join(targetDir, processed.filename);
                    fs.writeFileSync(targetFilePath, processed.content);
                } else {
                    console.log(`Metadata changed for: ${processed.blobPath}`);
                    metadataChangedFiles.push(processed);
                }
            } else {
                console.log(`File unchanged for: ${processed.blobPath}`);
                unchangedDocuments++;
                unchangedFiles.push(processed);
            }
        } catch (error) {
            console.error(`Error processing file ${fullPath}:`, error);
            throw error;
        }
    };
    
    // Check if sourceDir is actually a file on the filesystem
    const isFile = fs.existsSync(sourceDir) && fs.statSync(sourceDir).isFile();
    
    if (isFile) {
        processSingleFile(sourceDir, path.dirname(sourceDir));
        
        return { totalProcessed, changedDocuments, unchangedDocuments, changedFiles, metadataChangedFiles, unchangedFiles };
    }
    
    async function walkDirectory(dir: string): Promise<void> {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        
        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            
            if (entry.isDirectory()) {
                await walkDirectory(fullPath);
            } else if (entry.name.endsWith('.md') || entry.name.endsWith('.mdx')) {
                const relativePath = path.relative(sourceDir, fullPath);

                // Skip ignored paths
                if (source.ignoredPaths) {
                    if (source.isGenerated) {
                        if (source.ignoredPaths.some(p => entry.name.startsWith(p.replace(/[\\/]/g, "#")))) continue;
                    } else {
                        if (source.ignoredPaths.some(p => relativePath.startsWith(p))) continue;
                    }
                }

                // Skip reference files and release notes
                if (relativePath.startsWith('reference') || entry.name.startsWith('release-')) {
                    continue;
                }
                
                processSingleFile(fullPath, sourceDir);
            }
        }
    }
    
    await walkDirectory(sourceDir);
    
    return {
        totalProcessed, 
        changedDocuments, 
        unchangedDocuments, 
        changedFiles, 
        metadataChangedFiles,
        unchangedFiles 
    };
}

/**
 * Create release notes index with content from the 10 most recent release notes
 */
async function createReleaseNotesIndex(
    source: DocumentationSource,
    sourceDir: string,
    targetDir: string
): Promise<void> {
    // Path to release notes directory
    const releaseNotesDir = path.join(sourceDir, 'release-notes');
    
    // Check if release notes directory exists
    if (!fs.existsSync(releaseNotesDir)) {
        console.log(`Release notes directory not found for ${source.folder}, skipping index creation`);
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
    const description = `This contains latest release version and changes of ${source.folder}\n\n`;
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
            console.warn(`Error reading release note file ${filePath}: ${error}`);
            continue;
        }
    }
    
    // Create the index file in the target directory
    const indexPath = path.join(targetDir, 'version-release-notes-index.md');
    fs.writeFileSync(indexPath, content);
    
    console.log(`Created release notes index for ${source.folder}`);
}

/**
 * Extract date from a filename in the format release-YYYY-MM-DD.md
 */
export function extractDateFromFilename(filePath: string): Date {
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
export function extractReleaseInfo(content: string): { title: string; releaseDate: string; version: string } {
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
export function processMarkdownFile(
    filePath: string,
    source: DocumentationSource,
    sourceDir: string
): ProcessedMarkdownFile {
    const content = fs.readFileSync(filePath, 'utf-8');
    
    // Process file content
    const processed = convertMarkdown(content);

    if (!processed.filename) {
        // If no filename was found in frontmatter, generate one from file path
        const relativePath = path.relative(sourceDir, filePath);
        processed.filename = relativePath.replace(/[/\\]/g, '#');
        if (source.folder === 'azure-sdk-guidelines') {
            // Skip processing empty filename case for azure-sdk-guidelines
            return { 
                filename: '',
                content: '',
                blobPath: '',
                isValid: false
            };
        }
        if (source.isGenerated) {
            // remove generated prefix
            processed.filename = processed.filename.replace(/^generated#/, '');
        }
    }

    // Create blob path based on source folder and file name
    let fileName = processed.filename;
    if (source.fileNameLowerCase) {
        fileName = fileName.toLowerCase().replace(/\s+/g, '-');
    }
    const blobPath = path.join(source.folder, fileName).replace(/\\/g, '/');
    
    // Resolve metadata if source has metadata configuration
    let metadata: Metadata;
    if (source.metadata) {
        const relativePath = path.relative(sourceDir, filePath);
        metadata = MetadataResolver.resolveMetadata(
            relativePath,
            source.metadata,
            source.overrides
        );
    }
    
    return {
        filename: processed.filename,
        content: processed.content,
        blobPath: blobPath,
        isValid: true,
        metadata: metadata
    };
}

/**
 * Preprocess content to fix Azure AI Search markdown parser issues
 */
export function preprocessContent(content: string): string {
    let processed = content;
    
    // Fix 1: Replace all # at start of lines with // within code blocks
    // This prevents Azure AI Search from treating comments as markdown headers  
    // Use \w+ to require language identifier (avoids matching empty ``` closing delimiters)
    processed = processed.replace(/```(\w+)\s*\n([\s\S]*?)```/g, (match, lang, codeContent) => {
        // Replace # at line start, preserving any following whitespace
        // This handles both "# comment" and "#comment" cases
        const transformedContent = codeContent.replace(/^#(\s*)/gm, '//$1');
        return `\`\`\`${lang}\n${transformedContent}\`\`\``;
    });
    
    // Fix 2: Escape code block delimiters ``` to prevent Azure AI Search parser issues
    // Replace ``` with \`\`\` to display code blocks as literal text
    processed = processed.replace(/```(\w*)/g, '\\`\\`\\`$1');
    
    return processed;
}

/**
 * Convert markdown
 */
export function convertMarkdown(content: string): { filename: string; content: string } {
    let title = '';
    let filename = '';
    let foundTitle = false;
    let inFrontmatter = false;
    let firstContentLine = true;
    
    // Preprocess content to fix Azure AI Search parser issues
    content = preprocessContent(content);
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
        filename,
        content: contentLines.join('\n')
    };
}

/**
 * Extract sections and downgrade headers (kept for release notes processing)
 */
export function extractSections(content: string): string {
    // Remove frontmatter
    const contentWithoutFrontmatter = content.replace(/^---\s*\n[\s\S]*?\n---\s*\n/, '');
    
    // Remove caution blocks
    const contentWithoutCaution = contentWithoutFrontmatter.replace(/:::caution[\s\S]*?:::\s*/g, '');
    
    // Downgrade headers (add one more # to each header)
    const downgradedContent = contentWithoutCaution.replace(/^(#+)\s+(.+)$/gm, '#$1 $2');
    
    return downgradedContent.trim();
}

/**
 * Upload changed files to blob storage using the ProcessedMarkdownFile information
 */
async function uploadFilesToBlobStorage(
    changedFiles: ProcessedMarkdownFile[]
) {
    try {
        const blobService = new BlobService();
        let uploadedCount = 0;
        // Upload only changed files
        for (const file of changedFiles) {
            if (file.isValid) {
                // Prepare blob metadata if it exists
                let blobMetadata: Record<string, string> | undefined;
                
                if (file.metadata) {
                    // Convert metadata to string key-value pairs for blob metadata
                    // Only store scope and service_type as blob metadata
                    blobMetadata = {
                        scope: file.metadata.scope
                    };
                    
                    // Only add service_type if it exists
                    if (file.metadata.service_type) {
                        blobMetadata.service_type = file.metadata.service_type;
                    }
                }
                
                await blobService.putBlob(file.blobPath, file.content, blobMetadata);
                uploadedCount++;
            }
        }

        console.log(`Successfully uploaded ${uploadedCount} changed files to blob storage`);
        return;
    } catch (error) {
        console.error('Error uploading changed files to blob storage:', error);
        throw error;
    }
}

/**
 * Clean up expired blobs
 */
async function cleanupExpiredBlobs(currentFiles: ProcessedMarkdownFile[]): Promise<void> {
    try {
        const blobService = new BlobService();

        console.log('Cleaning up expired blobs...');
        
        // Get all existing blobs
        const blobs = await blobService.listBlobs();
        
        // Create a set of current file blob paths for efficient lookup
        const currentFileBlobPaths = new Set(
            currentFiles
                .filter(file => file.isValid)
                .map(file => file.blobPath)
        );
        
        let deletedCount = 0;

        for (const blob of blobs) {
            const blobPath = blob[0];
            // Skip static files
            if (blobPath.startsWith('static_')) {
                continue;
            }
            
            // Delete if not in current files
            if (!currentFileBlobPaths.has(blobPath)) {
                try {
                    await blobService.deleteBlob(blobPath);
                    deletedCount++;
                } catch (error) {
                    console.warn(`Failed to delete blob ${blobPath}: ${error instanceof Error ? error.message : 'Unknown error'}`);
                }
            }
        }

        console.log(`Cleaned up ${deletedCount} expired blobs`);
    } catch (error) {
        console.error('Error cleaning up expired blobs:', error);
        throw error;
    }
}

/**
 * Preprocess spector cases in the documentation directories
 */
async function preprocessSpectorCases(docsDir: string): Promise<void> {
    console.log('Starting spector case processing...');
    
    try {
        await SpectorCaseProcessor.processSpectorCases(docsDir);
        console.log('Spector case processing completed successfully');
    } catch (error) {
        console.error('Error processing spector cases:', error);
        throw error;
    }
}

/**
 * Process TypeSpec library
 */
function processTypeSpec(docsDir: string, relativeLibDir: string) : void {
    try {
        new TypeSpecProcessor(docsDir, relativeLibDir).processTypeSpecLibraries();
    } catch (error) {
        console.error(`Error processing typespec library: ${relativeLibDir}`, error);
    }
}