from src._github_manager import GithubManager
from ._base import MentionWorkflow
from ._github_issue_helpers import execute_workflow


class OpenGuidelinesIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"
    deduplication_prompt_file = "deduplicate_guidelines_issue.prompty"

    def execute_plan(self, plan: dict):
        """Execute the guidelines issue workflow"""
        client = GithubManager.get_instance()
        
        return execute_workflow(
            client=client,
            plan=plan,
            owner="Azure",
            repo="azure-rest-api-specs",
            workflow_tag="guidelines-issue",
            source_tag="APIView Copilot",
            dedup_prompt_file=self.deduplication_prompt_file,
            dedup_inputs={
                "language": self.language,
                "package_name": self.package_name,
                "code": self.code,
            },
        )


