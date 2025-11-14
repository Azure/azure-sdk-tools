import json

import prompty
from src._github_manager import GithubManager
from src._utils import get_prompt_path


class GitHubIssueHandler:
    """
    Handles GitHub issue operations including fetching, deduplication, and creation.
    Encapsulates repository-specific configuration and common GitHub workflows.
    """

    def __init__(
        self,
        *,
        repo_owner: str,
        repo_name: str,
        workflow_tag: str,
        source_tag: str = "APIView Copilot",
        deduplication_prompt_file: str = "deduplicate_parser_issue.prompty",
        base_labels: list[str] | None = None,
        language_labels: dict[str, str] | None = None,
    ):
        """
        Initialize GitHub issue handler with repository and workflow configuration.

        Args:
            repo_owner: GitHub repository owner
            repo_name: GitHub repository name
            workflow_tag: Tag to identify this workflow type (e.g., "parser-issue")
            source_tag: Tag to identify the source (default: "APIView Copilot")
            deduplication_prompt_file: Prompty file for deduplication logic
            base_labels: Base labels to apply to all issues (default: ["APIView"])
            language_labels: Mapping of language names to GitHub labels
        """
        self.repo_owner = repo_owner
        self.repo_name = repo_name
        self.workflow_tag = workflow_tag
        self.source_tag = source_tag
        self.deduplication_prompt_file = deduplication_prompt_file
        self.base_labels = base_labels or ["APIView"]
        self.language_labels = language_labels or {}

    def fetch_recent_issues(self):
        """Fetch recent open issues from GitHub matching workflow metadata."""
        client = GithubManager.get_instance()
        metadata_query = [f"workflow: {self.workflow_tag}", f"source: {self.source_tag}"]
        issues = client.search_issues(
            owner=self.repo_owner,
            repo=self.repo_name,
            query=f'{" ".join(metadata_query)} in:body is:issue state:open',
        )
        return issues

    def check_for_duplicate_issue(
        self, plan: dict, recent_issues: list, language: str = None, package_name: str = None, code: str = None
    ) -> dict:
        """
        Check if the issue already exists using deduplication prompt.

        Args:
            plan: The planned issue with title and body
            recent_issues: List of recent open issues
            language: Programming language context
            package_name: Package name context
            code: Code snippet context

        Returns:
            Dict with action ("no-op" or "create") and optional issue number
        """
        if not recent_issues:
            return {"action": "create"}
        
        dedup_prompt_path = get_prompt_path(folder="mention", filename=self.deduplication_prompt_file)
        error_context = f"{plan.get('title')}\n\n{plan.get('body')}"
        dedup_inputs = {
            "language": language,
            "package_name": package_name,
            "code": code,
            "error_context": error_context,
            "existing_issues": self._format_issues_for_dedup(recent_issues),
        }
        raw_dedup = prompty.execute(dedup_prompt_path, inputs=dedup_inputs)

        try:
            return json.loads(raw_dedup)
        except json.JSONDecodeError:
            raise ValueError(f"Deduplication prompt returned invalid JSON: {raw_dedup}")

    def _format_issues_for_dedup(self, issues: list) -> str:
        """Format issues for deduplication prompt input."""
        formatted_issues = [
            {
                "number": issue["number"],
                "title": issue["title"],
                "body": issue.get("body", "")[:500],  # truncate
                "created_at": issue["created_at"],
            }
            for issue in issues
        ]
        return json.dumps(formatted_issues)

    def create_issue(self, plan: dict, language: str = None):
        """
        Create a new issue on GitHub.

        Args:
            plan: The planned issue with title and body
            language: Programming language for label selection

        Returns:
            Created issue object from GitHub
        """
        client = GithubManager.get_instance()
        body = self._add_metadata_to_body(plan.get("body"))
        labels = self._build_issue_labels(language)
        return client.create_issue(
            owner=self.repo_owner, repo=self.repo_name, title=plan.get("title"), body=body, labels=labels
        )

    def _add_metadata_to_body(self, body: str) -> str:
        """Inject workflow metadata into issue body."""
        return f"""<!-- workflow: {self.workflow_tag} -->\n <!-- source: {self.source_tag} -->\n {body}"""

    def _build_issue_labels(self, language: str = None) -> list[str]:
        """Build labels for the GitHub issue including language-specific tag when available."""
        labels = self.base_labels.copy()
        if language:
            normalized_language = language.strip().lower()
            language_label = self.language_labels.get(normalized_language)
            if language_label:
                labels.append(language_label)
        return labels
