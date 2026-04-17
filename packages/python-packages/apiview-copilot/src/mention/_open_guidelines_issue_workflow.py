# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

import os

from dotenv import load_dotenv
from src._github_manager import GithubManager

from ._base import MentionWorkflow
from ._github_issue_helpers import check_for_duplicate_issue, create_and_submit_issue

load_dotenv(override=True)


class OpenGuidelinesIssueWorkflow(MentionWorkflow):
    prompty_filename = "parse_conversation_to_github_issue.prompty"
    summarize_prompt_file = "summarize_github_actions.prompty"
    deduplication_prompt_file = "deduplicate_guidelines_issue.prompty"

    def _get_client_and_owner(self):
        client = GithubManager.get_instance()
        environment = os.getenv("ENVIRONMENT_NAME")
        owner = "Azure" if environment == "production" else "tjprescott"
        return client, owner

    def check_for_duplicates(self, plan: dict):
        """Check for duplicate guidelines issues."""
        client, owner = self._get_client_and_owner()
        return check_for_duplicate_issue(
            client,
            plan=plan,
            owner=owner,
            repo="azure-sdk",
            workflow_tag="guidelines-issue",
            source_tag="APIView Copilot",
            dedup_prompt_file=self.deduplication_prompt_file,
            dedup_inputs={
                "language": self.language,
                "package_name": self.package_name,
                "code": self.code,
            },
        )

    def execute_plan(self, plan: dict):
        """Create the guidelines issue (dedup already passed)."""
        client, owner = self._get_client_and_owner()
        return create_and_submit_issue(
            client,
            plan=plan,
            owner=owner,
            repo="azure-sdk",
            workflow_tag="guidelines-issue",
            source_tag="APIView Copilot",
        )
