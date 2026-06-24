import { createNodeMiddleware, type EmitterWebhookEvent } from "@octokit/webhooks";
import { trace } from "@opentelemetry/api";
import { SeverityNumber } from "@opentelemetry/api-logs";
import { createServer } from "http";
import { App, type Octokit } from "octokit";
import { loadConfig } from "./config.ts";
import { logger } from "./logger.ts";

// TODO: Add unit tests

const host = process.env.NODE_ENV === "production" ? "0.0.0.0" : "localhost";
const port = process.env.PORT ? parseInt(process.env.PORT) : 3000;
const { githubAppId, webhookSecret, privateKey } = await loadConfig();

const app = new App({
    appId: githubAppId,
    privateKey: privateKey,
    webhooks: {
        secret: webhookSecret,
    },
});

const messageForNewPRs = "test comment from github app";

async function handlePullRequestOpened({
    octokit,
    payload,
}: {
    octokit: Octokit;
    payload: EmitterWebhookEvent<"pull_request.opened">["payload"];
}) {
    console.log(`Received a pull request event for #${payload.pull_request.number}`);

    try {
        await octokit.request("POST /repos/{owner}/{repo}/issues/{issue_number}/comments", {
            owner: payload.repository.owner.login,
            repo: payload.repository.name,
            issue_number: payload.pull_request.number,
            body: messageForNewPRs,
            headers: {
                "x-github-api-version": "2026-03-10",
            },
        });
    } catch (error) {
        if (error instanceof Error && "response" in error) {
            const err = error as { response: { status: number; data: { message: string } } };
            console.error(
                `Error! Status: ${err.response.status}. Message: ${err.response.data.message}`,
            );
        }
        console.error(error);
    }
}

async function handleIssueCommentCreated({
    octokit,
    payload,
}: {
    octokit: Octokit;
    payload: EmitterWebhookEvent<"issue_comment.created">["payload"];
}) {
    const span = trace.getActiveSpan();
    span?.setAttributes({
        issue_number: payload.issue?.number,
    });

    logger.emit({
        severityNumber: SeverityNumber.INFO,
        body: JSON.stringify(payload),
        attributes: {
            "issue-number": payload.issue?.number,
            "comment-body": payload.comment.body,
            "github-event": "issue_comment.created",
        },
    });

    if (!payload.issue.pull_request) return;
    if (payload.comment.body !== "trigger") return;

    console.log(`Received a "trigger" PR comment on #${payload.issue.number}`);

    const triggerTime = new Date(payload.comment.created_at);
    const currentTime = new Date();
    const gapSeconds = (currentTime.getTime() - triggerTime.getTime()) / 1000;
    const body = [
        `Triggering comment time: ${triggerTime.toISOString()}`,
        `Current time: ${currentTime.toISOString()}`,
        `Gap (seconds): ${gapSeconds}`,
    ].join("\n");

    try {
        await octokit.request("POST /repos/{owner}/{repo}/issues/{issue_number}/comments", {
            owner: payload.repository.owner.login,
            repo: payload.repository.name,
            issue_number: payload.issue.number,
            body: body,
            headers: {
                "x-github-api-version": "2026-03-10",
            },
        });
    } catch (error) {
        if (error instanceof Error && "response" in error) {
            const err = error as { response: { status: number; data: { message: string } } };
            console.error(
                `Error! Status: ${err.response.status}. Message: ${err.response.data.message}`,
            );
        }
        console.error(error);
    }
}

app.webhooks.on("pull_request.opened", handlePullRequestOpened);
app.webhooks.on("issue_comment.created", handleIssueCommentCreated);
app.webhooks.onError((error) => {
    if (error.name === "AggregateError") {
        console.error(`Error processing request: ${JSON.stringify(error.event)}`);
    } else {
        console.error(error);
    }
});

const path = "/api/webhook";
const localWebhookUrl = `http://${host}:${port}${path}`;

const middleware = createNodeMiddleware(app.webhooks, { path });

createServer((req, res) => {
    console.log(
        `${new Date().toISOString()} ${req.method} ${req.url} from ${req.socket.remoteAddress}`,
    );

    if (!req.url?.startsWith(path)) {
        res.writeHead(200);
        res.end("OK");
        return;
    }

    middleware(req, res).catch((error) => {
        console.error(error);
        res.writeHead(500);
        res.end();
    });
}).listen(port, host, () => {
    console.log(`Server is listening for events at: ${localWebhookUrl}`);
    console.log("Press Ctrl + C to quit.");
});
