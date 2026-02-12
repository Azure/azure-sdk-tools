# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=redefined-outer-name

"""
Shared fixtures and helpers for CLI command tests.
"""

import json
import os
import tempfile
from unittest.mock import MagicMock, patch

import pytest


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def make_temp_file(content: str, suffix: str = ".txt") -> str:
    """Create a temp file with *content* and return its path. Caller must clean up."""
    fd, path = tempfile.mkstemp(suffix=suffix)
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(content)
    return path


def make_temp_json(data: dict, suffix: str = ".json") -> str:
    """Create a temp JSON file and return its path."""
    return make_temp_file(json.dumps(data), suffix=suffix)


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------


@pytest.fixture(autouse=True)
def _reset_settings_singleton():
    """Reset the SettingsManager singleton between tests."""
    from src._settings import SettingsManager

    SettingsManager._instance = None
    yield
    SettingsManager._instance = None


@pytest.fixture(autouse=True)
def _reset_database_singleton():
    """Reset the DatabaseManager singleton between tests."""
    from src._database_manager import DatabaseManager

    DatabaseManager._instance = None
    yield
    DatabaseManager._instance = None


@pytest.fixture
def mock_settings():
    """
    Patch SettingsManager so it never connects to Azure App Configuration.
    Returns a mock whose .get(key) returns predictable fake values.
    """
    fake_settings = {
        "webapp_endpoint": "https://fake-endpoint.azurewebsites.net",
        "cosmos_endpoint": "https://fake-cosmos.documents.azure.com:443/",
        "cosmos_db_name": "fake-db",
        "search_endpoint": "https://fake-search.search.windows.net",
        "search_index_name": "fake-index",
        "app_id": "fake-app-id",
    }
    mock = MagicMock()
    mock.get.side_effect = lambda key: fake_settings.get(key.strip().lower(), f"fake-{key}")
    with patch("cli.SettingsManager", return_value=mock):
        yield mock


@pytest.fixture
def mock_credential():
    """Patch get_credential to return a fake credential."""
    fake_cred = MagicMock()
    fake_token = MagicMock()
    fake_token.token = "fake-token"
    fake_cred.get_token.return_value = fake_token
    with patch("src._credential.get_credential", return_value=fake_cred):
        yield fake_cred


@pytest.fixture
def sample_apiview_file():
    """Create a temporary file with sample APIView content."""
    content = "namespace Azure.Test {\n  public class TestClient {\n  }\n}\n"
    path = make_temp_file(content, suffix=".txt")
    yield path
    os.unlink(path)


@pytest.fixture
def sample_comments_json():
    """Create a temporary JSON file with sample comments for mention/resolve-thread tests."""
    data = {
        "language": "python",
        "package_name": "azure-test",
        "code": "class TestClient:\n    pass\n",
        "comments": [
            "This API looks good but needs documentation.",
            "@copilot please review this naming convention.",
        ],
    }
    path = make_temp_json(data, suffix=".json")
    yield path
    os.unlink(path)


@pytest.fixture
def sample_review_comments_json():
    """Create a temporary JSON file with sample existing review comments."""
    data = {
        "comments": [
            {
                "line_no": "10",
                "comment": "Consider renaming this method.",
                "badge": "warning",
            }
        ]
    }
    path = make_temp_json(data, suffix=".json")
    yield path
    os.unlink(path)
