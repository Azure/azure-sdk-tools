from ._base import MentionWorkflow
from ._github_issue_handler import GitHubIssueHandler


class OpenParserIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"

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
        handler = GitHubIssueHandler(
            repo_owner="Azure",
            repo_name="azure-sdk-tools",
            workflow_tag="parser-issue",
            source_tag="APIView Copilot",
            deduplication_prompt_file="deduplicate_parser_issue.prompty",
            base_labels=["APIView"],
            language_labels=self.LANGUAGE_LABELS,
        )
        return handler.execute_workflow(
            plan=plan,
            language=self.language,
            package_name=self.package_name,
            code=self.code,
        )


