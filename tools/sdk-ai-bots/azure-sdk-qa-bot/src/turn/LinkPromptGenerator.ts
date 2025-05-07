import { TurnContext } from "botbuilder";
import { GithubClient } from "../input/GithubClient.js";

export class LinkPromptGenerator {
    private readonly githubClient = new GithubClient();
    private readonly urlRegex = /https?:\/\/[^\s"'<>]+/g;
    private readonly urls: URL[] = [];
    private readonly context: TurnContext;

    constructor(context: TurnContext) {
        this.context = context;
        const text = this.context.activity.text;
        const links = text.match(this.urlRegex);
        this.urls = links?.map((link) => new URL(link)) || [];
    }

    public async generateGithubPullRequestPrompts() {
        const prompts: string[] = [];

        for (const url of this.urls) {
            if (!this.isGithubPullRequestUrl(url)) continue;
            const prUrl = url.href;
            const prDetails = await this.githubClient.getPullRequestDetails(
                prUrl
            );
            let prompt = `## Additionl Information for the pull request: ${prUrl}\n`;
            for (const key in prDetails) {
                const detail = JSON.stringify(prDetails[key], null, 2);
                prompt += `### ${key}\n${detail}\n`;
            }
            prompts.push(prompt);
        }

        return prompts;
    }

    private isGithubPullRequestUrl(url: URL): boolean {
        return url.hostname === "github.com" && url.pathname.includes("/pull/");
    }
}
