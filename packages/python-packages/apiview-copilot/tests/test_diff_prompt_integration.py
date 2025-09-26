# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

"""
Integration tests to verify that diff-based prompts contain the correct targeting instructions.
"""

import os
from src._utils import get_prompt_path


def test_diff_prompts_contain_focused_targeting_instructions():
    """Test that all diff-based prompts contain the focused targeting instructions."""
    diff_prompt_files = [
        "guidelines_diff_review.prompty",
        "generic_diff_review.prompty", 
        "context_diff_review.prompty"
    ]
    
    expected_instruction = "You may use unchanged lines as context to understand the code structure, but ALL comments must target ONLY the changed lines (marked with +). Never comment on unchanged code."
    
    for prompt_file in diff_prompt_files:
        prompt_path = get_prompt_path(folder="api_review", filename=prompt_file)
        assert os.path.exists(prompt_path), f"Prompt file not found: {prompt_path}"
        
        with open(prompt_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        assert expected_instruction in content, f"Missing focused targeting instruction in {prompt_file}"
        
        # Verify the contradictory instruction is NOT present
        contradictory_instruction = "DO consider all of the code (marked with a + or not) when making comments"
        assert contradictory_instruction not in content, f"Found contradictory instruction in {prompt_file}"


def test_diff_prompts_still_mention_changed_lines_only():
    """Test that all diff-based prompts still contain the instruction to comment only on changed lines."""
    diff_prompt_files = [
        "guidelines_diff_review.prompty",
        "generic_diff_review.prompty", 
        "context_diff_review.prompty"
    ]
    
    for prompt_file in diff_prompt_files:
        prompt_path = get_prompt_path(folder="api_review", filename=prompt_file)
        assert os.path.exists(prompt_path), f"Prompt file not found: {prompt_path}"
        
        with open(prompt_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Each prompt should contain the instruction to only comment on changed lines
        assert "marked with a +" in content, f"Missing changed lines targeting in {prompt_file}"
        assert "DO NOT make comments on unchanged lines" in content, f"Missing unchanged line exclusion in {prompt_file}"