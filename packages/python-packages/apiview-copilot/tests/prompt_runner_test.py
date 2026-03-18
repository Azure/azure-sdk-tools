# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Tests for _render_template in _prompt_runner.py.
"""

from src._prompt_runner import _render_template


class TestRenderTemplate:
    def test_simple_variable_substitution(self):
        result = _render_template("Hello {{ name }}!", {"name": "World"})
        assert result == "Hello World!"

    def test_for_loop(self):
        template = "{% for item in items %}- {{ item }}\n{% endfor %}"
        result = _render_template(template, {"items": ["a", "b", "c"]})
        assert result == "- a\n- b\n- c\n"

    def test_for_loop_matches_resolve_package_pattern(self):
        template = (
            "Available packages:\n"
            "{% for pkg in available_packages %}\n"
            "- {{pkg}}\n"
            "{% endfor %}"
        )
        result = _render_template(template, {"available_packages": ["azure-storage-blob", "azure-keyvault-secrets"]})
        assert "- azure-storage-blob" in result
        assert "- azure-keyvault-secrets" in result

    def test_if_block(self):
        template = "{% if show %}visible{% endif %}"
        assert _render_template(template, {"show": True}) == "visible"
        assert _render_template(template, {"show": False}) == ""

    def test_missing_variable_renders_empty(self):
        result = _render_template("Hello {{ name }}!", {})
        assert result == "Hello !"

    def test_none_value(self):
        result = _render_template("Value: {{ val }}", {"val": None})
        assert result == "Value: None"

    def test_dict_value(self):
        result = _render_template("Data: {{ data }}", {"data": {"key": "value"}})
        assert "key" in result
        assert "value" in result
