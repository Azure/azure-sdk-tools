import os

from dotenv import load_dotenv
from src._github_manager import GithubManager

from ._base import MentionWorkflow
from ._github_issue_helpers import execute_workflow

load_dotenv(override=True)


class OpenParserIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"
    deduplication_prompt_file = "deduplicate_parser_issue.prompty"

    LANGUAGE_LABELS = {
        "python": "Python",
        "py": "Python",
        "c#": ".NET",
        "csharp": ".NET",
        "dotnet": ".NET",
        ".net": ".NET",
        "c++": "C++",
        "cpp": "C++",
        "c99": "C99",
        "java": "Java",
        "android": "Java",
        "swift": "Swift",
        "javascript": "javascript",
        "js": "javascript",
        "go": "Go",
        "golang": "Go",
        "rust": "Rust",
    }

    def execute_plan(self, plan: dict):
        """Execute the parser issue workflow"""
        client = GithubManager.get_instance()
        environment = os.getenv("ENVIRONMENT_NAME")
        owner = "Azure" if environment == "production" else "tjprescott"

        return execute_workflow(
            client=client,
            plan=plan,
            owner=owner,
            repo="azure-sdk-tools",
            workflow_tag="parser-issue",
            source_tag="APIView Copilot",
            dedup_prompt_file=self.deduplication_prompt_file,
            dedup_inputs={
                "language": self.language,
                "package_name": self.package_name,
                "code": self.code,
            },
            base_labels=["APIView"],
            language=self.language,
            language_labels=self.LANGUAGE_LABELS,
        )
