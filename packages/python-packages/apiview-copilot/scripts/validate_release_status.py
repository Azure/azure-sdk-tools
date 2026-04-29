#!/usr/bin/env python3
"""
Validate and optionally fix APIView release statuses from logs.

This script:
1. Parses downloaded logs from a local file to extract package names, APIView revision IDs, and release dates
2. Queries production APIView Cosmos DB (APIViewV2/APIRevisions) to verify release status
3. Reports discrepancies between log claims and actual APIView status
4. With --fix-all, applies fixes to mark mismatched entries as released

By default, the script outputs the status of all packages without making changes.
Use --fix-all to actually apply fixes to any discrepancies.

Usage:
    python validate_release_status.py <log_file>
    python validate_release_status.py <log_file> --fix-all
    
Example:
    python validate_release_status.py logs.txt
    python validate_release_status.py logs.txt --fix-all
"""

import argparse
import logging
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Optional
import sys

# Ensure repository root is importable when running as a script path.
REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from src._apiview import get_apiview_cosmos_client

logger = logging.getLogger(__name__)


@dataclass
class PackageRelease:
    """Represents a package release entry from logs."""
    package_name: str
    review_id: str
    api_revision_id: str
    release_date: str
    http_status: int
    log_marked_released: bool
    actual_released_status: Optional[bool] = None
    actual_release_date: Optional[str] = None


class APIViewCosmosClient:
    """Client for APIView production Cosmos DB (APIViewV2/APIRevisions)."""

    def __init__(self, environment: str = "production"):
        self.environment = environment
        self.revisions = get_apiview_cosmos_client(
            container_name="APIRevisions",
            environment=environment,
            db_name="APIViewV2",
        )

    def get_revision_details(self, review_id: str, revision_id: str) -> Optional[dict]:
        """Get revision document from APIRevisions by id."""
        del review_id  # review_id is available for future validation if needed
        try:
            return self.revisions.read_item(item=revision_id, partition_key=revision_id)
        except Exception:
            query = "SELECT * FROM c WHERE c.id = @id"
            params = [{"name": "@id", "value": revision_id}]
            results = list(
                self.revisions.query_items(
                    query=query,
                    parameters=params,
                    enable_cross_partition_query=True,
                )
            )
            return results[0] if results else None

    def update_revision_release_status(
        self,
        review_id: str,
        revision_id: str,
        is_released: bool,
        release_date: Optional[str] = None,
    ) -> bool:
        """Update IsReleased/ReleasedOn fields in APIRevisions."""
        del review_id
        try:
            revision = self.get_revision_details(review_id="", revision_id=revision_id)
            if not revision:
                logger.error(f"Revision not found in Cosmos: {revision_id}")
                return False

            revision["IsReleased"] = is_released
            if release_date:
                revision["ReleasedOn"] = f"{release_date}T00:00:00Z"

            self.revisions.replace_item(item=revision["id"], body=revision)
            logger.info(
                "Updated revision %s: IsReleased=%s ReleasedOn=%s",
                revision_id,
                is_released,
                revision.get("ReleasedOn"),
            )
            return True
        except Exception as exc:
            logger.error("Failed updating revision %s: %s", revision_id, exc)
            return False

    def close(self):
        """No-op close for API compatibility."""
        return


class LogParser:
    """Parses APIView creation logs."""
    
    # Pattern to match package processing and review creation
    PACKAGE_PATTERN = re.compile(
        r"Processing ([\w\-]+)\n.*?"
        r"API review: https://spa\.apiview\.dev/review/([\da-f]+)\?activeApiRevisionId=([\da-f]+)",
        re.DOTALL
    )
    
    # Pattern to match release status
    RELEASE_PATTERN = re.compile(
        r"Package ([\w\-]+) is marked as released\."
    )
    
    # Pattern to extract date from log header
    DATE_PATTERN = re.compile(
        r"##\[section\]Starting: Create API Review\n.*?(\d{4})-(\d{2})-(\d{2})T",
        re.DOTALL
    )
    
    @staticmethod
    def parse(log_content: str) -> tuple[list[PackageRelease], str]:
        """
        Parse log content to extract package releases.
        
        Args:
            log_content: The full log file content
            
        Returns:
            Tuple of (list of PackageRelease, release_date)
        """
        releases = []
        
        # Extract date from log
        date_match = re.search(LogParser.DATE_PATTERN, log_content)
        release_date = "unknown"
        if date_match:
            year, month, day = date_match.groups()
            release_date = f"{year}-{month}-{day}"
        
        # Find all package processing sections
        for match in re.finditer(LogParser.PACKAGE_PATTERN, log_content):
            package_name = match.group(1)
            review_id = match.group(2)
            api_revision_id = match.group(3)
            
            # Find the HTTP status code for this section
            section_start = match.start()
            section_end = match.end()
            section_text = log_content[match.end():match.end() + 500]
            
            http_status = 200  # default
            if "HTTP Response code: 201" in section_text:
                http_status = 201
            elif "HTTP Response code: 200" in section_text:
                http_status = 200
            
            # Check if marked as released
            is_marked_released = bool(
                re.search(rf"Package {re.escape(package_name)} is marked as released\.", log_content)
            )
            
            releases.append(PackageRelease(
                package_name=package_name,
                review_id=review_id,
                api_revision_id=api_revision_id,
                release_date=release_date,
                http_status=http_status,
                log_marked_released=is_marked_released
            ))
        
        return releases, release_date


def fetch_log_content(log_source: str) -> str:
    """
    Fetch log content from a local file.
    
    Args:
        log_source: A local file path
        
    Returns:
        The log content as a string
        
    Raises:
        FileNotFoundError: If file not found
        UnicodeDecodeError: If file content is not valid text
    """
    log_path = Path(log_source)
    if not log_path.exists():
        raise FileNotFoundError(f"Log file not found: {log_path}")
    return log_path.read_text(encoding="utf-8", errors="replace")


def format_result(release: PackageRelease) -> str:
    """Format a release result for display."""
    # Use [OK] and [XX] for better Windows compatibility
    if release.actual_released_status is True:
        status = "[OK]"
    elif release.actual_released_status is False:
        status = "[NO]"
    else:
        status = "[??]"
    
    line = f"{status} {release.package_name:45} | "
    line += f"Review: {release.review_id[:8]}... | "
    line += f"Log says: {'RELEASED' if release.log_marked_released else 'NOT RELEASED'} | "
    
    if release.actual_released_status is None:
        line += "APIView: UNKNOWN"
    else:
        line += f"APIView: {'RELEASED' if release.actual_released_status else 'NOT RELEASED'}"
    
    return line


def main():
    parser = argparse.ArgumentParser(
        description="Validate APIView release statuses from logs",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python validate_release_status.py logs.txt
  python validate_release_status.py logs.txt --fix-all
  python validate_release_status.py logs.txt --fix-all --verbose
        """
    )
    parser.add_argument("log_file", help="Path to a downloaded log file")
    parser.add_argument(
        "--fix-all",
        action="store_true",
        help="Apply fixes to APIViewV2/APIRevisions (production) for mismatched entries"
    )
    parser.add_argument(
        "--verbose",
        "-v",
        action="store_true",
        help="Enable verbose logging"
    )
    
    args = parser.parse_args()
    
    # Setup logging
    log_level = logging.DEBUG if args.verbose else logging.WARNING
    logging.basicConfig(
        level=log_level,
        format="%(levelname)s: %(message)s"
    )
    if not args.verbose:
        logging.getLogger("azure").setLevel(logging.WARNING)
    
    # Fetch log content from local file
    try:
        log_content = fetch_log_content(args.log_file)
        print(f"Loaded logs from: {args.log_file}\n")
    except FileNotFoundError as e:
        print(f"Error: {e}")
        sys.exit(1)
    
    releases, release_date = LogParser.parse(log_content)
    print(f"Found {len(releases)} packages released on {release_date}\n")
    
    if not releases:
        print("No package releases found in log.")
        sys.exit(0)
    
    # Query APIView for each release
    client = APIViewCosmosClient(environment="production")
    discrepancies = []
    
    print("Checking APIViewV2/APIRevisions status (production):")
    print("-" * 130)
    
    for release in releases:
        # Query APIView
        revision_details = client.get_revision_details(
            release.review_id, 
            release.api_revision_id
        )
        
        if revision_details:
            release.actual_released_status = revision_details.get("IsReleased")
            release.actual_release_date = revision_details.get("ReleasedOn")
        else:
            release.actual_released_status = None
        
        # Print status
        print(format_result(release))
        
        # Track discrepancies only when Cosmos explicitly says not released.
        if release.log_marked_released and release.actual_released_status is False:
            discrepancies.append(release)
    
    print("-" * 130)
    
    # Report discrepancies
    if discrepancies:
        print(f"\n[WARNING] Found {len(discrepancies)} discrepancies:")
        for release in discrepancies:
            print(f"  - {release.package_name}: Log says RELEASED, but APIView shows NOT RELEASED")
        
        # Fix if requested
        if args.fix_all:
            print(f"\n[FIX] Fixing {len(discrepancies)} entries...")
            
            fixed = 0
            for release in discrepancies:
                if client.update_revision_release_status(
                    release.review_id,
                    release.api_revision_id,
                    True,
                    release.release_date
                ):
                    fixed += 1
            
            print(f"[OK] Fixed {fixed}/{len(discrepancies)} entries")
        else:
            print(f"\n[INFO] To fix these discrepancies, run: python validate_release_status.py {sys.argv[1]} --fix-all")
    else:
        print("\n[OK] All packages match their APIView release status!")
    
    client.close()


if __name__ == "__main__":
    main()
