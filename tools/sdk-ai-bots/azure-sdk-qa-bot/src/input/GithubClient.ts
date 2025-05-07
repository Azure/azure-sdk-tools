import { Octokit } from "@octokit/rest";
import { components } from "@octokit/openapi-types";

export interface PRDetails {
    comments: {
        review: CommentEx[];
        issue: CommentEx[];
    };
    reviews: {
        state: string;
        // TODO: check when it's null
        reviewer?: string;
    }[];
    labels: string[];
    title: string;
    diff: string;
}

type User = components["schemas"]["nullable-simple-user"];

interface CommentEx {
    name: string;
    type: string;
    comment: string;
}

function getCommentWithUser(commentUser: User, commentBody: string): CommentEx {
    return {
        name: commentUser.login,
        type: commentUser.type,
        comment: commentBody,
    };
}

export class GithubClient {
    private readonly authToken?: string;
    constructor(authToken?: string) {
        this.authToken = authToken;
    }

    // TODO: refactor
    public async getPullRequestDetails(prUrl: string): Promise<PRDetails> {
        const octokit = new Octokit({ auth: this.authToken });

        // 1. Parse owner, repo, pull_number from URL
        const match = prUrl.match(/github\.com\/([^/]+)\/([^/]+)\/pull\/(\d+)/);
        if (!match) {
            throw new Error("Invalid PR URL");
        }
        const [, owner, repo, pull_number_str] = match;
        const pull_number = Number(pull_number_str);

        // 2. Fetch basic PR to get head SHA and labels
        const { data: pr } = await octokit.pulls.get({
            owner,
            repo,
            pull_number,
        });
        const labels = pr.labels.map((lbl) => lbl.name);
        const title = pr.title;

        // 3. Fetch issue comments (general comments)
        const issueCommentsResponse = await octokit.issues.listComments({
            owner,
            repo,
            issue_number: pull_number,
            per_page: 100,
        });
        const issueComments = issueCommentsResponse.data
            .filter((d) => d.user.type !== "Bot")
            .map((d) => getCommentWithUser(d.user, d.body));
        // 4. Fetch review comments (inline/code comments)
        const reviewCommentsResponse = await octokit.pulls.listReviewComments({
            owner,
            repo,
            pull_number,
            per_page: 100,
        });
        const reviewComments = reviewCommentsResponse.data
            .filter((d) => d.user.type !== "Bot")
            .map((d) => getCommentWithUser(d.user, d.body));

        // 6. Fetch reviews to get reviewers
        const reviewsResponse = await octokit.pulls.listReviews({
            owner,
            repo,
            pull_number,
            per_page: 100,
        });

        const reviews = reviewsResponse.data.map((r) => ({
            state: r.state,
            reviewer: r.user?.login,
        }));

        // 7. Fetch PR diff
        const diff = await this.getPullRequestDiff(
            octokit,
            owner,
            repo,
            pull_number
        );

        return {
            comments: {
                review: reviewComments,
                issue: issueComments,
            },
            labels,
            reviews,
            title,
            diff,
        };
    }

    private async getPullRequestDiff(
        octokit: Octokit,
        owner: string,
        repo: string,
        pull_number: number
    ) {
        try {
            const response = await octokit.rest.pulls.get({
                owner,
                repo,
                pull_number,
                mediaType: {
                    format: "diff",
                },
            });
            return response.data as unknown as string;
        } catch (error) {
            console.error(`Failed to get diff: ${error}`);
        }
    }
}
