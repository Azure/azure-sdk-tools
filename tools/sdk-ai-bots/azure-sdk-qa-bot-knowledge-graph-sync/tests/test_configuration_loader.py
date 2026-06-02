"""Tests for ConfigurationLoader."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from src.services.configuration_loader import ConfigurationLoader


@pytest.fixture
def temp_config(tmp_path):
    """Helper to write a temp config and return its path."""

    def _write(data: dict) -> str:
        cfg_file = tmp_path / "knowledge-config.json"
        cfg_file.write_text(json.dumps(data, indent=2))
        return str(cfg_file)

    return _write


class TestConfigurationLoader:
    def setup_method(self):
        """Reset cached config between tests."""
        ConfigurationLoader._config = None

    def test_loads_config_and_exposes_sources(self, temp_config):
        config = {
            "version": "1.0.0",
            "sources": [
                {
                    "repository": {
                        "url": "https://github.com/org/repo.git",
                        "branch": "main",
                        "authType": "public",
                    },
                    "paths": [
                        {"name": "docs_a", "description": "Docs A", "path": "docs", "folder": "folder_a"},
                        {"name": "docs_b", "description": "Docs B", "folder": "folder_b", "relativeByRepoPath": True},
                    ],
                }
            ],
        }
        path = temp_config(config)
        ConfigurationLoader._config_path = Path(path)
        docs = ConfigurationLoader.get_documentation_sources()

        assert len(docs) == 2
        assert docs[0].folder == "folder_a"
        assert "docs" in docs[0].path
        assert docs[1].folder == "folder_b"

    def test_repository_configs(self, temp_config):
        config = {
            "version": "1.0.0",
            "sources": [
                {
                    "repository": {
                        "url": "https://github.com/org/another.git",
                        "branch": "dev",
                        "authType": "public",
                    },
                    "paths": [
                        {"name": "part1", "description": "Part1", "path": "docs/part1", "folder": "f1"},
                        {"name": "part2", "description": "Part2", "path": "docs/part2", "folder": "f2"},
                    ],
                }
            ],
        }
        path = temp_config(config)
        ConfigurationLoader._config_path = Path(path)
        repos = ConfigurationLoader.get_repository_configs()

        assert len(repos) == 1
        repo = repos[0]
        assert repo.url == "https://github.com/org/another.git"
        assert repo.branch == "dev"
        # Sparse checkout paths should include both paths
        assert len(repo.sparse_checkout) == 2
        assert "docs/part1" in repo.sparse_checkout
        assert "docs/part2" in repo.sparse_checkout
