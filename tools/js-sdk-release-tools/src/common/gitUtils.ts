import { runCommand, runCommandOptions } from "./utils.js";
import * as os from "os";
import * as path from "path";
import * as fs from "fs";
import { Octokit } from "@octokit/rest";

const GITHUB_OWNER = 'Azure';
const GITHUB_REPO = 'azure-sdk-for-js';

export async function getFile(packageName: string, version: string, sdkFilePath: string): Promise<string> {
    const result = await runCommand(`git`, [`--no-pager`, `show`, `${packageName}_${version}:${sdkFilePath}`], runCommandOptions, false);
    return result.stdout;
}

export function getTag(packageName: string, version: string): string {
    return `${packageName}_${version}`;
}

export async function cloneTmpSdkRepo(tag: string): Promise<string> {
    // Create a unique temporary directory
    const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'azure-sdk-'));
    
    try {
        const octokit = new Octokit({
            auth: process.env.GITHUB_TOKEN, // Optional: for higher rate limits
        });
        
        // Get repository information to verify the tag exists
        await octokit.rest.git.getRef({
            owner: GITHUB_OWNER,
            repo: GITHUB_REPO,
            ref: `tags/${tag}`,
        });
        
        // Download the repository archive at the specific tag
        const { data: archiveData } = await octokit.rest.repos.downloadZipballArchive({
            owner: GITHUB_OWNER,
            repo: GITHUB_REPO,
            ref: tag,
        });
        
        // Write the archive to a temporary file
        const archivePath = path.join(tmpDir, 'repo.zip');
        fs.writeFileSync(archivePath, Buffer.from(archiveData as ArrayBuffer));
        
        // Extract the archive using Node.js built-in modules
        const AdmZip = require('adm-zip');
        const zip = new AdmZip(archivePath);
        const extractPath = path.join(tmpDir, 'extracted');
        zip.extractAllTo(extractPath, true);
        
        // The extracted folder will have a name like "repo-name-commit-hash", find and rename it
        const extractedContents = fs.readdirSync(extractPath);
        const extractedFolderPath = path.join(extractPath, extractedContents[0]);
        const finalPath = path.join(tmpDir, 'repo');
        fs.renameSync(extractedFolderPath, finalPath);
        
        // Clean up
        fs.unlinkSync(archivePath);
        fs.rmSync(extractPath, { recursive: true });
        
        return finalPath;
    } catch (error) {
        // Clean up on error
        if (fs.existsSync(tmpDir)) {
            fs.rmSync(tmpDir, { recursive: true, force: true });
        }
        throw error;
    }
}