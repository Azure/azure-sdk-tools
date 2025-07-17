import os
import pytest
from src._utils import get_prompt_path

# You may need to adjust PACKAGE_ROOT and prompts folder for your test environment
PACKAGE_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PROMPTS_FOLDER = os.path.join(PACKAGE_ROOT, "prompts")


def test_get_prompt_path_valid_no_suffix():
    result = get_prompt_path(folder="summarize", filename="summarize_api")
    assert os.path.exists(result)


def test_get_prompt_path_valid_with_suffix():
    result = get_prompt_path(folder="summarize", filename="summarize_api.prompty")
    assert os.path.exists(result)


def test_get_prompt_path_missing():
    folder = ""
    filename = "nonexistent.prompty"
    with pytest.raises(FileNotFoundError):
        get_prompt_path(folder=folder, filename=filename)
