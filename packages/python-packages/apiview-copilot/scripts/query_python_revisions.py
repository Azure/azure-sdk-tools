# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Query APIView Cosmos DB for Python revisions and report upgrade eligibility.

Prints:
  1. Total Python revision count
  2. Revisions where VersionString != the specified value (upgrade candidates)
  3. Of those, how many are excluded by the other criteria:
     - FileName is null
     - HasOriginal is false
     - FileName is not a .whl file

Usage:
  python scripts/query_python_revisions.py --version "0.3.28"
  python scripts/query_python_revisions.py --version "0.3.28" --environment staging
"""

import argparse
import os
import sys

_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
_ROOT = os.path.dirname(_SCRIPT_DIR)
sys.path.insert(0, _ROOT)

from src._apiview import get_apiview_cosmos_client
from src._credential import get_credential


def main():
    parser = argparse.ArgumentParser(description="Query Python revisions for upgrade eligibility.")
    parser.add_argument(
        "--version",
        type=str,
        default="0.3.28",
        help="The current parser VersionString. Revisions != this value are upgrade candidates.",
    )
    parser.add_argument(
        "--environment",
        type=str,
        default="production",
        choices=["production", "staging"],
        help="APIView environment (default: production).",
    )
    args = parser.parse_args()

    container = get_apiview_cosmos_client(container_name="APIRevisions", environment=args.environment)
    reviews_container = get_apiview_cosmos_client(container_name="Reviews", environment=args.environment)

    # Get open (not closed) Python review IDs — matches pipeline behavior
    print("Fetching open Python reviews...")
    open_reviews_query = "SELECT VALUE c.id FROM c WHERE c.Language = 'Python' AND c.IsClosed = false"
    open_review_ids = list(
        reviews_container.query_items(open_reviews_query, enable_cross_partition_query=True)
    )
    open_review_id_set = set(open_review_ids)
    print(f"Open Python reviews: {len(open_review_id_set)}")

    # 1. Total Python revision count (open reviews)
    total_query = """
        SELECT VALUE COUNT(1) FROM c
        WHERE c.Language = 'Python'
          AND ARRAY_CONTAINS(@review_ids, c.ReviewId)
    """
    review_id_params = [{"name": "@review_ids", "value": open_review_ids}]
    total_count = list(container.query_items(total_query, parameters=review_id_params, enable_cross_partition_query=True))[0]
    print(f"Total Python revisions (open reviews): {total_count}")

    # 2. Revisions where at least one file has VersionString != target
    candidates_query = """
        SELECT DISTINCT VALUE c.id
        FROM c
        JOIN f IN c.Files
        WHERE c.Language = 'Python'
          AND ARRAY_CONTAINS(@review_ids, c.ReviewId)
          AND f.VersionString != @version
    """
    params = [{"name": "@version", "value": args.version}, {"name": "@review_ids", "value": open_review_ids}]
    candidate_ids = list(
        container.query_items(candidates_query, parameters=params, enable_cross_partition_query=True)
    )
    candidate_count = len(candidate_ids)
    print(f"Revisions with VersionString != '{args.version}': {candidate_count}")

    # 3. Of those candidates, find ones that DON'T meet the other criteria
    exclusion_query = """
        SELECT c.id, c.ReviewId, c.APIRevisionType, c.IsApproved, c.IsReleased, c.IsDeleted,
               f.FileId, f.FileName, f.HasOriginal, f.VersionString
        FROM c
        JOIN f IN c.Files
        WHERE c.Language = 'Python'
          AND ARRAY_CONTAINS(@review_ids, c.ReviewId)
          AND f.VersionString != @version
    """
    results = list(
        container.query_items(exclusion_query, parameters=params, enable_cross_partition_query=True)
    )

    no_filename = 0
    no_original = 0
    not_whl = 0
    excluded_revision_ids = set()
    excluded_revision_types = {}  # APIRevisionType -> count
    excluded_approved = 0
    excluded_released = 0
    no_filename_ids = []
    no_original_ids = []
    not_whl_ids = []
    not_whl_extensions = {}  # extension -> count
    upgradable_file_ids = []  # FileIds that pass all criteria
    upgradable_file_meta = {}  # FileId -> {IsApproved, IsReleased}
    upgradable_deleted = 0  # upgradable but IsDeleted=true

    for row in results:
        filename = row.get("FileName")
        has_original = row.get("HasOriginal", False)
        reasons = []

        if not filename:
            no_filename += 1
            if len(no_filename_ids) < 5:
                no_filename_ids.append(row["id"])
            reasons.append("FileName=null")
        elif not filename.endswith(".whl"):
            not_whl += 1
            if len(not_whl_ids) < 5:
                not_whl_ids.append(row["id"])
            not_whl_extensions[os.path.splitext(filename)[1] or f"(no ext: {filename})"] = \
                not_whl_extensions.get(os.path.splitext(filename)[1] or f"(no ext: {filename})", 0) + 1
            reasons.append(f"not .whl ({filename})")

        if not has_original:
            no_original += 1
            if len(no_original_ids) < 5:
                no_original_ids.append(row["id"])
            reasons.append("HasOriginal=false")

        if reasons:
            excluded_revision_ids.add(row["id"])
            rev_type = row.get("APIRevisionType", "Unknown")
            excluded_revision_types[rev_type] = excluded_revision_types.get(rev_type, 0) + 1
            if row.get("IsApproved"):
                excluded_approved += 1
            if row.get("IsReleased"):
                excluded_released += 1
        else:
            fid = row["FileId"]
            upgradable_file_ids.append(fid)
            upgradable_file_meta[fid] = {
                "IsApproved": bool(row.get("IsApproved")),
                "IsReleased": bool(row.get("IsReleased")),
            }
            if row.get("IsDeleted"):
                upgradable_deleted += 1

    print(f"\n--- Exclusion Breakdown (file-level) ---")
    print(f"  Files with FileName = null:      {no_filename}")
    if no_filename_ids:
        print(f"    Examples: {no_filename_ids}")
    print(f"  Files with HasOriginal = false:   {no_original}")
    if no_original_ids:
        print(f"    Examples: {no_original_ids}")
    print(f"  Files not ending in .whl:         {not_whl}")
    if not_whl_ids:
        print(f"    Examples: {not_whl_ids}")
    if not_whl_extensions:
        print(f"    Extensions breakdown: {dict(sorted(not_whl_extensions.items(), key=lambda x: -x[1]))}")
    if excluded_revision_types:
        print(f"\n  Not-upgradable by revision type:")
        for rtype, count in sorted(excluded_revision_types.items(), key=lambda x: -x[1]):
            print(f"    {rtype}: {count}")
    if excluded_revision_ids:
        n = len(excluded_revision_ids)
        print(f"\n  Not-upgradable approval/release status:")
        print(f"    Approved: {excluded_approved}  ({excluded_approved / n * 100:.1f}%)")
        print(f"    Released: {excluded_released}  ({excluded_released / n * 100:.1f}%)")

    # Summary
    on_current_version = total_count - candidate_count
    upgradable = candidate_count - len(excluded_revision_ids)
    not_upgradable = len(excluded_revision_ids)

    print(f"\n--- Summary ---")
    print(f"  On current version ({args.version}): {on_current_version:>6}  ({on_current_version / total_count * 100:.1f}%)")
    print(f"  Upgradable (not yet upgraded):     {upgradable:>6}  ({upgradable / total_count * 100:.1f}%)")
    print(f"    of which IsDeleted=true:         {upgradable_deleted:>6}  ({upgradable_deleted / upgradable * 100:.1f}%)" if upgradable else "")
    print(f"  Not upgradable (missing criteria): {not_upgradable:>6}  ({not_upgradable / total_count * 100:.1f}%)")

    # 4. Check blob storage for corrupt (0 byte) files among upgradable revisions
    print(f"\n--- Blob Storage Integrity Check ---")
    print(f"  Checking {len(upgradable_file_ids)} upgradable files in blob storage...")

    from azure.storage.blob import BlobServiceClient

    storage_account_names = {
        "production": "apiview",
        "staging": "apiviewstagingstorage",
    }
    storage_acc = storage_account_names.get(args.environment)
    if not storage_acc:
        print(f"  ERROR: No storage account name for environment '{args.environment}'.")
    else:
        storage_url = f"https://{storage_acc}.blob.core.windows.net/"
        blob_service = BlobServiceClient(account_url=storage_url, credential=get_credential())
        originals_container = blob_service.get_container_client("originals")

        zero_byte_count = 0
        missing_count = 0
        zero_byte_ids = []
        missing_ids = []
        corrupt_file_ids = set()  # all corrupt/missing file IDs

        for i, file_id in enumerate(upgradable_file_ids):
            if (i + 1) % 500 == 0:
                print(f"    Checked {i + 1}/{len(upgradable_file_ids)}...", flush=True)
            try:
                blob_client = originals_container.get_blob_client(file_id)
                props = blob_client.get_blob_properties()
                if props.size == 0:
                    zero_byte_count += 1
                    corrupt_file_ids.add(file_id)
                    if len(zero_byte_ids) < 5:
                        zero_byte_ids.append(file_id)
            except Exception:
                missing_count += 1
                corrupt_file_ids.add(file_id)
                if len(missing_ids) < 5:
                    missing_ids.append(file_id)

        valid_count = len(upgradable_file_ids) - zero_byte_count - missing_count
        print(f"\n  Results:")
        print(f"    Valid (non-zero size):  {valid_count}")
        print(f"    Corrupt (0 bytes):      {zero_byte_count}")
        if zero_byte_ids:
            print(f"      Examples: {zero_byte_ids}")
        print(f"    Missing (not in blob):  {missing_count}")
        if missing_ids:
            print(f"      Examples: {missing_ids}")
        if upgradable_file_ids:
            print(f"    Corrupt/missing rate:   {(zero_byte_count + missing_count) / len(upgradable_file_ids) * 100:.1f}%")

        # Approval/release status for corrupt blobs
        if corrupt_file_ids:
            corrupt_approved = sum(1 for fid in corrupt_file_ids if upgradable_file_meta[fid]["IsApproved"])
            corrupt_released = sum(1 for fid in corrupt_file_ids if upgradable_file_meta[fid]["IsReleased"])
            corrupt_total = len(corrupt_file_ids)
            print(f"\n    Corrupt/missing approval status:")
            print(f"      Approved: {corrupt_approved}  ({corrupt_approved / corrupt_total * 100:.1f}%)")
            print(f"      Released: {corrupt_released}  ({corrupt_released / corrupt_total * 100:.1f}%)")


if __name__ == "__main__":
    main()
