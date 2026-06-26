import { getRepositoryInstallationToken, gitHubRequest } from "../github/github-app.js";

interface GitHubIssueResponse {
    readonly number: number;
    readonly html_url: string;
}

interface GitHubRepositoryResponse {
    readonly full_name: string;
}

export interface CreatedTestIssue {
    readonly number: number;
    readonly url: string;
}

const testIssueOwner = "tjprescott";
const testIssueRepository = "azure-sdk-for-python";
const testIssueAssignee = "tjprescott";

export async function createGitHubIssue(message: string): Promise<CreatedTestIssue> {
    const token = await getRepositoryInstallationToken(testIssueOwner, testIssueRepository);
    await verifyRepositoryAccess(token);

    const issue = await gitHubRequest<GitHubIssueResponse>(
        `https://api.github.com/repos/${testIssueOwner}/${testIssueRepository}/issues`,
        token,
        "Bearer",
        {
            method: "POST",
            body: JSON.stringify({
                title: message,
                body: message,
                assignees: [testIssueAssignee],
            }),
        },
    );

    return {
        number: issue.number,
        url: issue.html_url,
    };
}

async function verifyRepositoryAccess(token: string): Promise<void> {
    const repository = await gitHubRequest<GitHubRepositoryResponse>(
        `https://api.github.com/repos/${testIssueOwner}/${testIssueRepository}`,
        token,
        "Bearer",
    );

    console.log(`GitHub App installation token can access ${repository.full_name}`);
}