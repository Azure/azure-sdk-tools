import json
import prompty
from src._github_manager import GithubManager
from src._utils import get_prompt_path
from ._base import MentionWorkflow


class OpenParserIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"
    deduplication_prompt_file = "deduplicate_parser_issue.prompty"
    
    REPO_OWNER = "tjprescott"
    REPO_NAME = "azure-sdk-tools"

    def execute_plan(self, plan: dict):
        """Execute the parser issue workflow"""
        recent_issues = self._fetch_recent_parser_issues()
        dedup_result = self._check_for_duplicate_issue(plan, recent_issues)
        
        if dedup_result["action"] == "no-op":
            # in recent_issues, find the issue with the matching number
            issue_number = int(dedup_result["issue"])
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
        
        issue = self._create_parser_issue(plan)
        return {
            "action": "create",
            "url": issue["html_url"],
            "title": issue["title"],
            "body": issue["body"],
            "created_at": issue["created_at"],
        }

    def _fetch_recent_parser_issues(self):
        """Fetch recent open parser issues from GitHub."""
        client = GithubManager.get_instance()
        metadata_query = ["workflow: parser-issue", "source: APIView Copilot"]
        issues = client.search_issues(
            owner=self.REPO_OWNER,
            repo=self.REPO_NAME,
            query=f'{" ".join(metadata_query)} in:body is:issue state:open'
        )
        return issues

    def _check_for_duplicate_issue(self, plan: dict, recent_issues: list) -> dict:
        """Check if the issue already exists with deduplication prompt."""
        dedup_prompt_path = get_prompt_path(folder="mention", filename=self.deduplication_prompt_file)
        error_context = f"{plan.get('title')}\n\n{plan.get('body')}"
        dedup_inputs = {
            "language": self.language,
            "package_name": self.package_name,
            "code": self.code,
            "error_context": error_context,
            "existing_issues": self._format_issues_for_dedup(recent_issues)
        }
        raw_dedup = prompty.execute(dedup_prompt_path, inputs=dedup_inputs)
        
        try:
            return json.loads(raw_dedup)
        except json.JSONDecodeError:
            # Error out
            raise ValueError(f"Deduplication prompt returned invalid JSON: {raw_dedup}")

    def _format_issues_for_dedup(self, issues: list) -> str:
        """Format issues for deduplication prompt input."""
        formatted_issues = [
            {
                "number": issue["number"],
                "title": issue["title"],
                "body": issue.get("body", "")[:500],  # truncate
                "created_at": issue["created_at"]
            }
            for issue in issues
        ]
        return json.dumps(formatted_issues)

    def _create_parser_issue(self, plan: dict):
        """Create a new parser issue on GitHub."""
        client = GithubManager.get_instance()
        body = self._add_metadata_to_body(plan.get("body"))
        return client.create_issue(
            owner=self.REPO_OWNER,
            repo=self.REPO_NAME,
            title=plan.get("title"),
            body=body,
            labels=["parser"]
        )
    
    def _add_metadata_to_body(self, body: str) -> str:
        """Inject parser issue metadata into issue body."""
        return f"""<!-- workflow: parser-issue -->
    <!-- source: APIView Copilot -->

    {body}"""

