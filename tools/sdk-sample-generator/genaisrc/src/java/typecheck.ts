import path from "node:path";
import { getUniqueDirName } from "../utils.ts";
import type { TypeCheckParameters, TypeCheckResult } from "../types.ts";

// Grab the public class name from a Java file 
function extractClassName(code: string): string {
    const classMatch = code.match(/public\s+class\s+(\w+)/);
    return classMatch ? classMatch[1] : "Temp";
}

let container: ContainerHost | undefined = undefined;
const importRegex = /^import\s+([a-zA-Z0-9_.]+);/gm;

// Cache Maven API responses to avoid repeated HTTP calls
const mavenApiCache = new Map<string, MavenCoordinates>();

interface MavenCoordinates {
    groupId: string;
    artifactId: string;
    version: string;
}

// Get the package name from a fully-qualified Java class
function extractPackageName(javaClass: string): string {
    const parts = javaClass.split('.');
    if (parts.length <= 1) return javaClass;
    return parts.slice(0, -1).join('.');
}


// Convert Azure package name to Maven artifact name
function packageToArtifactName(packageName: string): string {
    const parts = packageName.split('.');
    if (parts.length >= 3 && parts[0] === 'com' && parts[1] === 'azure') {
        const serviceParts = parts.slice(2);
        return 'azure-' + serviceParts.join('-');
    }
    return packageName;
}

export async function getMavenCoordinatesFromAPI(
    javaClass: string,
): Promise<MavenCoordinates | undefined> {
    const packageName = extractPackageName(javaClass);
    
    // Skip JDK classes early
    if (packageName.startsWith('java.') || packageName.startsWith('javax.')) {
        return undefined;
    }
    
    // For Azure packages, query Maven repo dynamically
    if (packageName.startsWith('com.azure.')) {
        const artifactId = packageToArtifactName(packageName);
        const cacheKey = `com.azure:${artifactId}`;
        
        // Check cache first
        if (mavenApiCache.has(cacheKey)) {
            return mavenApiCache.get(cacheKey);
        }
        
        try {
            const metadataUrl = `https://repo1.maven.org/maven2/com/azure/${artifactId}/maven-metadata.xml`;
            
            const response = await fetch(metadataUrl);
            if (!response.ok) {
                return undefined;
            }
            
            const xmlText = await response.text();
            const latestMatch = xmlText.match(/<latest>([^<]+)<\/latest>/);
            const releaseMatch = xmlText.match(/<release>([^<]+)<\/release>/);
            
            const version = latestMatch?.[1] || releaseMatch?.[1];
            if (version) {
                const coordinates = {
                    groupId: 'com.azure',
                    artifactId,
                    version
                };
                
                // Cache the result for future use
                mavenApiCache.set(cacheKey, coordinates);
                return coordinates;
            }
        } catch (error) {
            console.warn(`Failed to fetch Maven metadata for ${packageName}:`, error);
        }
    }
    
    return undefined;
}

async function parseMavenDependencies(
    code: string,
    pkgName?: string,
): Promise<MavenCoordinates[]> {
    const deps = new Map<string, MavenCoordinates>();
    let match;
    while ((match = importRegex.exec(code)) !== null) {
        const dep = await getMavenCoordinatesFromAPI(match[1]);
        if (dep && (!pkgName || dep.artifactId !== pkgName)) {
            const key = `${dep.groupId}:${dep.artifactId}`;
            if (!deps.has(key)) {
                deps.set(key, dep);
            }
        }
    }
    return Array.from(deps.values());
}

// Make javac errors a bit friendlier with extra hints
function enhanceJavacErrors(javacOutput: string): string[] {
    const enhancements: string[] = [];
    const lines = javacOutput.split('\n');
    
    for (const line of lines) {
        if (line.includes('error: cannot find symbol')) {
            // Extract the symbol from the error line
            const symbolMatch = line.match(/symbol:\s+(\w+)\s+(\w+)/);
            
            if (symbolMatch) {
                const symbolType = symbolMatch[1]; // class, method, variable
                const symbolName = symbolMatch[2];
                
                if (symbolType === 'class') {
                    enhancements.push(`• Missing class '${symbolName}': Check if you need to add an import statement. Common issues:
                                        - Missing import for the class
                                        - Incorrect package name in import
                                        - Missing Maven dependency for the library
                                        - Class name typo or wrong capitalization`);
                } else if (symbolType === 'method') {
                    enhancements.push(`• Missing method '${symbolName}': This method may not exist in the current library version. Check:
                                        - Method name spelling and capitalization
                                        - If the method was renamed or moved to a different class
                                        - Library documentation for the correct method signature`);
                } else {
                    enhancements.push(`• Missing symbol '${symbolName}': Verify the symbol name and ensure proper imports are included.`);
                }
            }
        } else if (line.includes('error: package') && line.includes('does not exist')) {
            const packageMatch = line.match(/package\s+([a-zA-Z0-9_.]+)\s+does not exist/);
            if (packageMatch) {
                const packageName = packageMatch[1];
                enhancements.push(`• Missing package '${packageName}': This package could not be resolved. Check:
                                    - Package name spelling and case sensitivity
                                    - If the package exists in Maven Central
                                    - If you need to add the corresponding Maven dependency`);
            }
        } else if (line.includes('error:') && line.includes('incompatible types')) {
            enhancements.push(`• Type incompatibility: Check that you're using compatible types. Ensure proper casting or use the correct method signatures.`);
        }
    }
    
    
    return enhancements;
}

export async function typecheckJava({
    code,
    clientDist,
    pkgName,
}: TypeCheckParameters): Promise<TypeCheckResult> {
    const className = extractClassName(code);
    const fileName = `${className}.java`;
    const projectDir = path.join("tmp", getUniqueDirName());
    const filePath = `${projectDir}/${fileName}`;

    if (!container) {
        container = await host.container({
            image: "eclipse-temurin:21-jdk-alpine", // Java 21 with Alpine
            networkEnabled: true,
            persistent: true,
        });
        
        // Install Maven once per container lifecycle (cached in persistent container)
        await container.exec("apk", ["add", "--no-cache", "maven"]);
    }

    try {
        await container.writeText(filePath, code);

        // Add client jar to classpath if passed in
        let classpath = ".";
        if (clientDist) {
            await container.copyTo(clientDist, projectDir);
            const distName = path.basename(clientDist);
            classpath += `:${distName}`;
        }

         // Look for dependencies in imports and build a quick pom.xml
        const deps = await parseMavenDependencies(code, pkgName);
        let installRes: ShellOutput | undefined;
        if (deps.length > 0) {
            const pomXml = `
              <project xmlns="http://maven.apache.org/POM/4.0.0" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
              xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 http://www.maven.apache.org/xsd/maven-4.0.0.xsd">
              <modelVersion>4.0.0</modelVersion>
              <groupId>tempmod</groupId>
              <artifactId>tempmod</artifactId>
              <version>1.0-SNAPSHOT</version>
              <repositories>
                <repository>
                  <id>azure-maven</id>
                  <url>https://repo1.maven.org/maven2/</url>
                </repository>
              </repositories>
              <dependencies>
              ${deps.map((d) => `
                <dependency>
                  <groupId>${d.groupId}</groupId>
                  <artifactId>${d.artifactId}</artifactId>
                  <version>${d.version}</version>
                </dependency>
              `).join("\n")}
            </dependencies>
          </project>
                `.trim();
            await container.writeText(path.join(projectDir, "pom.xml"), pomXml);

            // Use single mvn compile command (Maven pre-installed, no need to install)
            installRes = await container.exec(
                "mvn",
                ["compile", "-q", "--batch-mode"], // Quiet, non-interactive, handles deps + compilation
                { cwd: projectDir },
            );
        } else {
            // No dependencies, just compile with javac
            installRes = await container.exec(
                "javac",
                [fileName],
                { cwd: projectDir },
            );
        }

        const compileSucceeded = installRes?.exitCode === 0 && !installRes.failed;

        // Get compile output (Maven or javac)
        const compileOutput = (installRes?.stdout ?? "") + (installRes?.stderr ?? "");
        
        // Filter output to show only essential information
        const filteredOutput = compileOutput
            .split('\n')
            .filter(line => 
                line.includes('BUILD SUCCESS') || 
                line.includes('BUILD FAILURE') ||
                line.includes('ERROR') ||
                line.includes('WARN') ||
                line.includes('Total time:') ||
                line.includes('error:') ||
                line.includes('FAILURE:')
            )
            .join('\n');

        let output = `compile output:\n${filteredOutput || compileOutput || (compileSucceeded ? 'Compilation successful' : 'Compilation failed')}`;

        // If failed, add explicit error summary with helpful guidance
        if (!compileSucceeded) {
            const errors = [`Java compilation failed (exit code: ${installRes?.exitCode})`];
            
            // Parse and enhance compilation errors with helpful context
            const enhancedErrors = enhanceJavacErrors(compileOutput);
            if (enhancedErrors.length > 0) {
                errors.push(`\nDETAILED ERROR ANALYSIS:\n${enhancedErrors.join('\n')}`);
            }
            output += `\n\nCOMPILATION ERRORS:\n${errors.join('\n')}`;
        }

        return {
            succeeded: compileSucceeded,
            output,
        };
    } finally {
        try {
            await container.exec("rm", ["-rf", projectDir]);
        } catch (cleanupErr) {
            console.warn("Cleanup failed:", cleanupErr);
        }
    }
}