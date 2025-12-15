import json

from src._github_manager import GithubManager
from src._utils import run_prompty


def create_issue(
    client: GithubManager,
    *,
    owner: str,
    repo: str,
    title: str,
    body: str,
    workflow_tag: str,
    source_tag: str = "APIView Copilot",
    labels: list[str] | None = None,
) -> dict:
    """
    Create a GitHub issue with workflow metadata.

    Args:
        client: GithubManager instance
        owner: Repository owner
        repo: Repository name
        title: Issue title
        body: Issue body content
        workflow_tag: Tag identifying workflow type (e.g., "parser-issue", "guidelines-issue")
        source_tag: Tag identifying the source (default: "APIView Copilot")
        labels: Optional list of labels to apply

    Returns:
        Created issue dict from GitHub API
    """
    body_with_metadata = _inject_metadata(body, workflow_tag, source_tag)
    return client.create_issue(
        owner=owner,
        repo=repo,
        title=title,
        body=body_with_metadata,
        labels=labels,
    )


def execute_workflow(
    client: GithubManager,
    *,
    plan: dict,
    owner: str,
    repo: str,
    workflow_tag: str,
    source_tag: str = "APIView Copilot",
    dedup_prompt_file: str,
    dedup_inputs: dict,
    base_labels: list[str] | None = None,
    language: str | None = None,
    language_labels: dict[str, str] | None = None,
) -> dict:
    """
    Execute complete GitHub issue workflow: fetch, deduplicate, and create if needed.

    Args:
        client: GithubManager instance
        plan: Dict with 'title' and 'body' for the planned issue
        owner: Repository owner
        repo: Repository name
        workflow_tag: Tag identifying workflow type (e.g., "parser-issue")
        source_tag: Tag identifying the source (default: "APIView Copilot")
        dedup_prompt_file: Filename of deduplication prompty file (in mention folder)
        dedup_inputs: Dict of inputs for the deduplication prompt (without issue_context)
        base_labels: Optional base labels to apply
        language: Optional language for language-specific labeling
        language_labels: Optional mapping of language names to GitHub labels

    Returns:
        Dict with keys:
        - action: "create" or "no-op"
        - url: Issue URL
        - title: Issue title
        - body: Issue body
        - created_at: ISO timestamp
    """
    # Build issue context for deduplication
    issue_context = f"{plan.get('title')}\n\n{plan.get('body')}"
    full_dedup_inputs = {
        **dedup_inputs,
        "issue_context": issue_context,
    }

    # Build labels with language support if provided
    labels = _build_labels_with_language(
        base_labels or [],
        language,
        language_labels or {},
    )

    # Fetch recent issues
    recent_issues = _fetch_recent_issues(client, owner, repo, workflow_tag, source_tag)

    # Check for duplicates
    dedup_result = _check_for_duplicate(plan, recent_issues, dedup_prompt_file, full_dedup_inputs)

    # Handle no-op case
    if dedup_result["action"] == "no-op":
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

    # Create new issue
    issue = create_issue(
        client,
        owner=owner,
        repo=repo,
        title=plan.get("title"),
        body=plan.get("body"),
        workflow_tag=workflow_tag,
        source_tag=source_tag,
        labels=labels,
    )
    return {
        "action": "create",
        "url": issue["html_url"],
        "title": issue["title"],
        "body": issue["body"],
        "created_at": issue["created_at"],
    }


# Internal helpers


def _fetch_recent_issues(
    client: GithubManager, owner: str, repo: str, workflow_tag: str, source_tag: str
) -> list[dict]:
    """Fetch recent open issues from GitHub matching workflow metadata."""
    metadata_query = [f"workflow: {workflow_tag}", f"source: {source_tag}"]
    query = f'{" ".join(metadata_query)} in:body is:issue state:open'
    return client.search_issues(owner=owner, repo=repo, query=query)


def _check_for_duplicate(plan: dict, recent_issues: list[dict], dedup_prompt_file: str, dedup_inputs: dict) -> dict:
    """
    Check if issue already exists using deduplication prompt.

    Args:
        plan: Dict with 'title' and 'body'
        recent_issues: List of recent issue dicts
        dedup_prompt_file: Filename of deduplication prompty
        dedup_inputs: Context-specific inputs for dedup prompt

    Returns:
        Dict with 'action' ("create" or "no-op") and optional 'issue' number
    """
    if not recent_issues:
        return {"action": "create"}

    # Merge plan context and custom inputs
    full_inputs = {
        **dedup_inputs,
        "existing_issues": _format_issues_for_dedup(recent_issues),
    }
    raw_dedup = run_prompty(folder="mention", filename=dedup_prompt_file, inputs=full_inputs)
    try:
        return json.loads(raw_dedup)
    except json.JSONDecodeError as e:
        raise ValueError(f"Deduplication prompt returned invalid JSON: {raw_dedup}") from e


def _format_issues_for_dedup(issues: list[dict], max_body_length: int = 500) -> str:
    """Format issues for deduplication prompt input."""
    formatted_issues = [
        {
            "number": issue["number"],
            "title": issue["title"],
            "body": issue.get("body", "")[:max_body_length],
            "created_at": issue["created_at"],
        }
        for issue in issues
    ]
    return json.dumps(formatted_issues)


def _inject_metadata(body: str, workflow_tag: str, source_tag: str) -> str:
    """Inject workflow metadata into issue body."""
    return f"""<!-- workflow: {workflow_tag} source: {source_tag} -->\n\n{body}"""


def _build_labels_with_language(
    base_labels: list[str], language: str | None, language_labels: dict[str, str]
) -> list[str]:
    """
    Build labels including language-specific label when available.

    Args:
        base_labels: Base labels to include
        language: Programming language name (optional)
        language_labels: Mapping of normalized language names to GitHub labels

    Returns:
        List of labels including language label if found
    """
    labels = base_labels.copy()
    if language:
        normalized_language = language.strip().lower()
        language_label = language_labels.get(normalized_language)
        if language_label:
            labels.append(language_label)
    return labels
