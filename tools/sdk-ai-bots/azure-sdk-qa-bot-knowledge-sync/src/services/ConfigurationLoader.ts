import * as fs from 'fs';
import * as path from 'path';
import { InvocationContext } from '@azure/functions';

/**
 * Hierarchical metadata for categorizing documentation
 */
export interface Metadata {
    scope: 'branded' | 'unbranded';
    service_type?: 'data-plane' | 'management-plane';
}

/**
 * File-specific metadata override using glob pattern matching
 */
export interface Override {
    pattern: string;
    metadata: Metadata;
}

/**
 * Configuration interfaces matching the schema
 */
export interface DocumentationPath {
    name: string;
    description: string;
    path?: string;
    folder?: string;
    fileNameLowerCase?: boolean;
    ignoredPaths?: string[];
    relativeByRepoPath?: boolean;
    isGenerated?: boolean;
    metadata?: Metadata;
    overrides?: Override[];
}

export interface Repository {
    url: string;
    path?: string;
    branch: string;
    authType: 'public' | 'ssh' | 'token';
    sshHost?: string;
    tokenEnvVar?: string;
    localPathEnv?: string;
}

export interface Source {
    repository: Repository;
    paths: DocumentationPath[];
}

export interface KnowledgeConfig {
    $schema?: string;
    description?: string;
    version: string;
    sources: Source[];
}

/**
 * Legacy interfaces for backward compatibility
 */
export interface DocumentationSource {
    path: string;
    folder: string;
    fileNameLowerCase?: boolean;
    ignoredPaths?: string[];
    isGenerated?: boolean;
    metadata?: Metadata;
    overrides?: Override[];
}

export interface RepositoryConfig {
    name: string;
    url: string;
    path: string;
    branch: string;
    sparseCheckout?: string[];
    authType?: 'public' | 'token' | 'ssh' | 'local';
    sshHost?: string;
    token?: string;
    localPath?: string;
}

/**
 * Service for loading and transforming knowledge configuration
 */
export class ConfigurationLoader {
    private static config: KnowledgeConfig | null = null;
    private static configPath: string = path.join(__dirname, '../../config/knowledge-config.json');

    /**
     * Load the configuration from the JSON file
     */
    public static loadConfig(context?: InvocationContext): KnowledgeConfig {
        if (this.config) {
            return this.config;
        }

        try {
            const configContent = fs.readFileSync(this.configPath, 'utf-8');
            this.config = JSON.parse(configContent) as KnowledgeConfig;
            
            if (context) {
                context.log(`Loaded configuration version ${this.config.version} with ${this.config.sources.length} sources`);
            }
            
            return this.config;
        } catch (error) {
            const errorMsg = `Failed to load configuration from ${this.configPath}: ${error}`;
            if (context) {
                context.error(errorMsg);
            }
            throw new Error(errorMsg);
        }
    }

    /**
     * Transform the new config format to legacy DocumentationSource format
     */
    public static getDocumentationSources(context?: InvocationContext): DocumentationSource[] {
        const config = this.loadConfig(context);
        const sources: DocumentationSource[] = [];

        for (const source of config.sources) {
            const repoPath = source.repository.path || this.getRepoPathFromUrl(source.repository.url);
            
            for (const docPath of source.paths) {
                sources.push({
                    path: (docPath.relativeByRepoPath || docPath.path === undefined) ? `docs/${repoPath}` : `docs/${repoPath}/${docPath.path}`,
                    folder: docPath.folder,
                    fileNameLowerCase: docPath.fileNameLowerCase,
                    ignoredPaths: docPath.ignoredPaths,
                    isGenerated: docPath.isGenerated,
                    metadata: docPath.metadata,
                    overrides: docPath.overrides,
                });
            }
        }

        if (context) {
            context.log(`Transformed configuration into ${sources.length} documentation sources`);
        }

        return sources;
    }

    /**
     * Transform the new config format to legacy RepositoryConfig format
     */
    public static getRepositoryConfigs(context?: InvocationContext): RepositoryConfig[] {
        const config = this.loadConfig(context);
        const repositories: RepositoryConfig[] = [];

        for (const source of config.sources) {
            const repo = source.repository;
            const repoPath = repo.path || this.getRepoPathFromUrl(repo.url);
            
            // Calculate sparse checkout from paths
            const sparseCheckout = this.calculateSparseCheckout(source.paths);
            
            repositories.push({
                name: this.getRepoNameFromUrl(repo.url),
                url: repo.url,
                path: repoPath,
                branch: repo.branch,
                sparseCheckout: sparseCheckout.length > 0 ? sparseCheckout : undefined,
                authType: repo.authType,
                sshHost: repo.sshHost,
                token: repo.tokenEnvVar ? process.env[repo.tokenEnvVar] : undefined,
                localPath: repo.localPathEnv ? process.env[repo.localPathEnv] : undefined
            });
        }

        if (context) {
            context.log(`Transformed configuration into ${repositories.length} repository configs`);
        }

        return repositories;
    }

    /**
     * Extract repository name from URL
     */
    private static getRepoNameFromUrl(url: string): string {
        // Handle various URL formats
        const patterns = [
            /\/([^\/]+)\.git$/,  // Standard .git URLs
            /\/([^\/]+)\.wiki\.git$/, // Wiki URLs  
            /\/([^\/]+)$/,       // URLs without .git
            /_git\/([^\/]+)$/    // Azure DevOps URLs
        ];

        for (const pattern of patterns) {
            const match = url.match(pattern);
            if (match) {
                return match[1];
            }
        }

        // Fallback: use the last segment of the path
        const segments = url.split('/');
        return segments[segments.length - 1] || 'unknown-repo';
    }

    /**
     * Generate repository path from URL (used when path is not specified)
     */
    private static getRepoPathFromUrl(url: string): string {
        const repoName = this.getRepoNameFromUrl(url);
        
        // Handle special cases
        if (url.includes('.wiki.git')) {
            return repoName;
        }
        
        if (url.includes('dev.azure.com')) {
            return repoName;
        }

        return repoName;
    }

    /**
     * Calculate sparse checkout paths from documentation paths
     */
    private static calculateSparseCheckout(paths: DocumentationPath[]): string[] {
        const sparseCheckout: string[] = [];
        
        for (const docPath of paths) {
            if (docPath.path) {
                sparseCheckout.push(docPath.path);
            }
        }
        
        return sparseCheckout;
    }

    /**
     * Reload configuration (for testing or dynamic updates)
     */
    public static reloadConfig(context?: InvocationContext): KnowledgeConfig {
        this.config = null;
        return this.loadConfig(context);
    }

    /**
     * Get configuration file path
     */
    public static getConfigPath(): string {
        return this.configPath;
    }

    /**
     * Set custom configuration file path (for testing)
     */
    public static setConfigPath(newPath: string): void {
        this.configPath = newPath;
        this.config = null; // Clear cached config
    }
}
