import { Octokit } from "@octokit/rest";
import { components } from "@octokit/openapi-types";

type RPCheckRunConclusion = components["schemas"]["check-run"]["conclusion"];

export interface RPCheckResult {
    name: string;
    conclusion: RPCheckRunConclusion;
}

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
    checks: RPCheckResult[];
    labels: string[];
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

export async function getPullRequestDetails(
    prUrl: string,
    authToken?: string
): Promise<PRDetails> {
    const octokit = new Octokit({ auth: authToken });

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
    const headSha = pr.head.sha;
    const labels = pr.labels.map((lbl) => lbl.name);

    // 3. Fetch issue comments (general comments)
    const issueCommentsResponse = await octokit.issues.listComments({
        owner,
        repo,
        issue_number: pull_number,
        per_page: 100,
    });
    const issueComments = issueCommentsResponse.data.map((d) =>
        getCommentWithUser(d.user, d.body)
    );
    // 4. Fetch review comments (inline/code comments)
    const reviewCommentsResponse = await octokit.pulls.listReviewComments({
        owner,
        repo,
        pull_number,
        per_page: 100,
    });
    const reviewComments = reviewCommentsResponse.data.map((d) =>
        getCommentWithUser(d.user, d.body)
    );

    // 5. Fetch check runs for the head SHA
    const checkRuns = await octokit.checks.listForRef({
        owner,
        repo,
        ref: headSha,
        per_page: 100,
    });
    const checks = checkRuns.data.check_runs.map((run) => ({
        name: run.name,
        conclusion: run.conclusion,
    }));

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

    return {
        comments: {
            review: reviewComments,
            issue: issueComments,
        },
        checks,
        labels,
        reviews,
    };
}
