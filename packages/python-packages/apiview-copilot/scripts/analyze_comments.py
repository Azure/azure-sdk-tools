import argparse
import os
import sys

import prompty
import prompty.azure
from dotenv import load_dotenv

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from src._apiview import get_comments_in_date_range
from src._models import APIViewComment
from src._utils import get_prompt_path

load_dotenv(override=True)


def main():
    parser = argparse.ArgumentParser(description="Analyze APIView comments by language and date window.")
    parser.add_argument("--language", required=True, help="Language to filter comments (e.g., python)")
    parser.add_argument("--start-date", required=True, help="Start date (YYYY-MM-DD)")
    parser.add_argument("--end-date", required=True, help="End date (YYYY-MM-DD)")
    parser.add_argument("--environment", default="production", help="APIView environment (default: production)")
    args = parser.parse_args()

    # Retrieve all comments in date range
    raw_comments = get_comments_in_date_range(args.start_date, args.end_date, environment=args.environment)
    filtered = [c for c in raw_comments if c.get("CommentSource") != "Diagnostic" and c.get("IsDeleted") != True]

    # Build ReviewId -> Language mapping
    from src._apiview import get_apiview_cosmos_client

    allowed_commenters = {"christothes", "bterlson", "glorialimicrosoft", "helen229", "maorleger", "tg-msft"}
    # allowed_commenters = None

    reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=args.environment)
    review_ids = set(c.get("ReviewId") for c in filtered if c.get("ReviewId"))
    if review_ids:
        params = []
        clauses = []
        for i, rid in enumerate(review_ids):
            param_name = f"@id_{i}"
            clauses.append(f"c.id = {param_name}")
            params.append({"name": param_name, "value": rid})
        query = f"SELECT c.id, c.Language FROM c WHERE ({' OR '.join(clauses)})"
        review_results = list(
            reviews_container.query_items(query=query, parameters=params, enable_cross_partition_query=True)
        )
        review_lang_map = {r["id"]: r.get("Language", "").lower() for r in review_results}
    else:
        review_lang_map = {}

    language = args.language.lower()
    comments = [APIViewComment(**c) for c in filtered if review_lang_map.get(c.get("ReviewId", ""), "") == language]

    # Filter by allowed commenters if provided
    if allowed_commenters:
        comments = [c for c in comments if c.created_by in allowed_commenters]

    comment_texts = [comment.comment_text for comment in comments if comment.comment_text]

    # Analyze themes using prompty
    prompt_path = get_prompt_path(folder="other", filename="analyze_comment_themes")
    inputs = {"comments": comment_texts}
    print("\n---\nTHEME ANALYSIS:\n---")
    theme_output = prompty.execute(prompt_path, inputs=inputs)
    print(theme_output)

    # Output comment count
    print(f"Comment count: {len(comment_texts)}")

    # Output unique CreatedBy values
    created_by_set = {comment.created_by for comment in comments if comment.created_by}
    print(f"Unique CreatedBy values ({len(created_by_set)}): {sorted(created_by_set)}")


if __name__ == "__main__":
    main()
