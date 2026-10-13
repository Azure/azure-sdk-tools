import hashlib
import io
import json
import sys
from unittest.mock import Mock

import pytest

from research import run_holdout, server
from research.profiles import PROFILES


def snapshot():
    revision = "a" * 40
    paths = [
        "eng/pipelines/mirror-repos.yml",
        "eng/pipelines/templates/steps/sync-repo-merge-branch.yml",
        "eng/pipelines/templates/steps/sync-repo-branch.yml",
        "eng/scripts/Merge-Branch.ps1",
        "eng/scripts/Git-Repo-Sync.ps1",
    ]
    return {
        "source_revision": revision,
        "contract": {
            "source_revision": revision,
            "source_files": [
                {"path": path, "sha256": hashlib.sha256(b"source").hexdigest()}
                for path in paths
            ],
        },
    }


def test_source_loader_verifies_every_pinned_file(monkeypatch):
    opener = Mock(side_effect=lambda *args, **kwargs: io.BytesIO(b"source"))
    monkeypatch.setattr(server, "urlopen", opener)
    result = server.load_snapshot(json.dumps(snapshot()))
    assert len(result["texts"]) == opener.call_count == 5
    assert all(
        call.args[0].startswith("https://raw.githubusercontent.com/Azure/azure-sdk-tools/" + "a" * 40)
        for call in opener.call_args_list
    )


def test_source_loader_rejects_checksum_mismatch(monkeypatch):
    monkeypatch.setattr(server, "urlopen", lambda *args, **kwargs: io.BytesIO(b"changed"))
    with pytest.raises(ValueError, match="checksum mismatch"):
        server.load_snapshot(json.dumps(snapshot()))


def test_source_loader_rejects_arbitrary_paths_before_fetch(monkeypatch):
    opener = Mock()
    monkeypatch.setattr(server, "urlopen", opener)
    value = snapshot()
    value["contract"]["source_files"][0]["path"] = "../credentials"
    with pytest.raises(ValueError, match="Unexpected research source paths"):
        server.load_snapshot(json.dumps(value))
    opener.assert_not_called()


def test_source_loader_requires_full_sha():
    value = snapshot()
    value["source_revision"] = "main"
    with pytest.raises(ValueError, match="full commit SHA"):
        server.load_snapshot(json.dumps(value))


def test_no_additional_candidate_added():
    assert len(PROFILES) == 6
    assert PROFILES["baseline"] == ""


def test_empty_profile_runs_original_server(monkeypatch):
    monkeypatch.setattr(sys, "argv", ["research/server.py"])
    monkeypatch.setattr(server.os, "chdir", Mock())
    runner = Mock()
    monkeypatch.setattr(server.runpy, "run_path", runner)
    server.main()
    runner.assert_called_once_with(str(server.AGENT / "server.py"), run_name="__main__")


@pytest.mark.parametrize("value", ["", "[]", "{}"])
def test_holdout_requires_nonempty_case_array(monkeypatch, value):
    monkeypatch.setenv("QA_RESEARCH_DATASET", value)
    with pytest.raises(ValueError):
        run_holdout.main()
