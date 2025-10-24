from src._github_manager import GithubManager

from ._base import MentionWorkflow


class OpenParserIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"

    def execute_plan(self, plan: dict):
        client = GithubManager.get_instance()
        issue = client.create_issue(
            owner="tjprescott", repo="azure-sdk-tools", title=plan.get("title"), body=plan.get("body")
        )
        return issue
