from ._base import MentionWorkflow
from ._github_issue_handler import GitHubIssueHandler


class OpenGuidelinesIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"

    def execute_plan(self, plan: dict):
        """Execute the guidelines issue workflow"""
        handler = GitHubIssueHandler(
            repo_owner="Azure",
            repo_name="azure-rest-api-specs",
            workflow_tag="guidelines-issue",
            source_tag="APIView Copilot",
            deduplication_prompt_file="deduplicate_guidelines_issue.prompty",
        )
        return handler.execute_workflow_with_dedup(
            plan=plan,
            language=self.language,
            package_name=self.package_name,
            code=self.code,
        )


