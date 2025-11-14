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
        github_handler = GitHubIssueHandler(
            repo_owner="Azure",
            repo_name="azure-sdk-tools",
            workflow_tag="parser-issue",
            source_tag="APIView Copilot",
            deduplication_prompt_file="deduplicate_parser_issue.prompty",
            base_labels=["APIView"],
            language_labels=self.LANGUAGE_LABELS,
        )

        recent_issues = github_handler.fetch_recent_issues()
        dedup_result = github_handler.check_for_duplicate_issue(
            plan=plan,
            recent_issues=recent_issues,
            language=self.language,
            package_name=self.package_name,
            code=self.code,
        )

        if dedup_result["action"] == "no-op":
            # in recent_issues, find the issue with the matching number
            issue_number = dedup_result["issue"]
            for issue in recent_issues:
                if issue["number"] == issue_number:
                    return {
                        "action": "no-op",
                        "url": issue["html_url"],
                        "title": issue["title"],
                        "body": issue.get("body", ""),
                        "created_at": issue["created_at"],
                    }
            raise ValueError(f"Issue number {issue_number} from deduplication prompt not found in recent issues.")

        issue = github_handler.create_issue(plan=plan, language=self.language)
        return {
            "action": "create",
            "url": issue["html_url"],
            "title": issue["title"],
            "body": issue["body"],
            "created_at": issue["created_at"],
        }


