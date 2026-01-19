import { minimatch } from 'minimatch';
import { Metadata, Override } from './ConfigurationLoader';

/**
 * Service for resolving hierarchical metadata with file overrides
 */
export class MetadataResolver {
    /**
     * Resolve metadata for a file using path defaults and override patterns
     * @param relativePath Relative path from repository root
     * @param pathMetadata Default metadata from DocumentationPath
     * @param overrides Override patterns from DocumentationPath
     * @returns Resolved metadata with scope and plane, or null if no metadata
     */
    static resolveMetadata(
        relativePath: string,
        pathMetadata?: Metadata,
        overrides?: Override[]
    ): Metadata | null {
        // If no metadata at all, return null (will not add metadata fields)
        if (!pathMetadata) {
            return null;
        }

        // Start with path-level defaults
        let metadata: Metadata = { ...pathMetadata };

        // Apply overrides if they exist
        if (overrides && overrides.length > 0) {
            for (const override of overrides) {
                if (this.matchPattern(relativePath, override.pattern)) {
                    // Always use inherit strategy: only update specified fields
                    metadata = {
                        ...metadata,
                        ...override.metadata
                    };
                }
            }
        }

        // Return resolved metadata (scope and plane only)
        return metadata;
    }

    /**
     * Match a file path against a glob pattern
     * @param filePath File path to test
     * @param pattern Glob pattern (e.g., '**\/Azure Data Plane Service\/**', '**\/*paging*.md')
     * @returns True if the pattern matches
     */
    private static matchPattern(filePath: string, pattern: string): boolean {
        // Normalize path separators for Windows compatibility
        const normalizedPath = filePath.replace(/\\/g, '/');
        const normalizedPattern = pattern.replace(/\\/g, '/');
        
        return minimatch(normalizedPath, normalizedPattern, {
            dot: true,           // Match dotfiles
            nocase: true,        // Case-insensitive matching
            matchBase: false     // Don't match basename only
        });
    }

    /**
     * Validate metadata structure
     * @param metadata Metadata to validate
     * @returns True if valid
     */
    static validateMetadata(metadata: Metadata): boolean {
        // Scope is required
        if (!metadata.scope || !['branded', 'unbranded'].includes(metadata.scope)) {
            return false;
        }

        // Plane is optional but must be valid if present
        if (metadata.plane && !['data-plane', 'management-plane'].includes(metadata.plane)) {
            return false;
        }

        // Plane only makes sense for branded content
        if (metadata.scope === 'unbranded' && metadata.plane) {
            console.warn('Warning: plane is set for unbranded content and will be ignored');
        }

        return true;
    }
}
