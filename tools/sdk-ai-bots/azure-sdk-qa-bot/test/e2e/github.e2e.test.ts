import { it, expect } from "vitest";
import { getPullRequestDetails } from "../../src/input/GithubClient.js";

// TODO: Add more tests to cover all branches and edge cases
it("e2e test", async () => {
    const prUrl = "https://github.com/Azure/azure-rest-api-specs/pull/34201";
    const details = await getPullRequestDetails(prUrl);
    expect(details.labels).contains("TypeSpec");
    console.log("conversations", JSON.stringify(details, null, 4));
});
