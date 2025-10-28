# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Test cases for utility functions in APIView Copilot.
"""

import os

import pytest
from src._utils import get_prompt_path


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
