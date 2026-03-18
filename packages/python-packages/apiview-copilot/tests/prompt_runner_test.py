# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Tests for _render_template and _parse_prompty in _prompt_runner.py.
"""

import json

import pytest

from src._prompt_runner import _render_template, _parse_prompty, _resolve_env_vars, _load_file_reference


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


class TestResolveEnvVars:
    def test_single_env_var(self, monkeypatch):
        monkeypatch.setenv("MY_VAR", "hello")
        assert _resolve_env_vars("${env:MY_VAR}") == "hello"

    def test_multiple_env_vars(self, monkeypatch):
        monkeypatch.setenv("HOST", "localhost")
        monkeypatch.setenv("PORT", "8080")
        assert _resolve_env_vars("${env:HOST}:${env:PORT}") == "localhost:8080"

    def test_missing_env_var_returns_empty(self, monkeypatch):
        monkeypatch.delenv("NONEXISTENT_VAR", raising=False)
        assert _resolve_env_vars("${env:NONEXISTENT_VAR}") == ""

    def test_no_env_pattern_returns_unchanged(self):
        assert _resolve_env_vars("plain text") == "plain text"

    def test_mixed_text_and_env_vars(self, monkeypatch):
        monkeypatch.setenv("NAME", "world")
        assert _resolve_env_vars("hello ${env:NAME}!") == "hello world!"


class TestLoadFileReference:
    def test_loads_json_file(self, tmp_path):
        schema = {"type": "object", "properties": {"name": {"type": "string"}}}
        json_file = tmp_path / "schema.json"
        json_file.write_text(json.dumps(schema), encoding="utf-8")
        result = _load_file_reference(tmp_path, "${file:schema.json}")
        assert result == schema

    def test_loads_text_file(self, tmp_path):
        txt_file = tmp_path / "content.txt"
        txt_file.write_text("some content", encoding="utf-8")
        result = _load_file_reference(tmp_path, "${file:content.txt}")
        assert result == "some content"

    def test_missing_file_returns_original(self, tmp_path):
        result = _load_file_reference(tmp_path, "${file:missing.json}")
        assert result == "${file:missing.json}"

    def test_non_file_pattern_returns_unchanged(self, tmp_path):
        result = _load_file_reference(tmp_path, "plain string")
        assert result == "plain string"


class TestParsePrompty:
    def test_parses_basic_prompty(self, tmp_path):
        content = (
            "---\n"
            "name: Test Prompt\n"
            "description: A test prompt\n"
            "model:\n"
            "  api: chat\n"
            "  configuration:\n"
            "    azure_deployment: gpt-4\n"
            "  parameters:\n"
            "    frequency_penalty: 0\n"
            "---\n"
            "system:\n"
            "You are a helpful assistant.\n"
            "\n"
            "user:\n"
            "Hello {{ name }}\n"
        )
        prompty_file = tmp_path / "test.prompty"
        prompty_file.write_text(content, encoding="utf-8")
        config = _parse_prompty(prompty_file)
        assert config.name == "Test Prompt"
        assert config.description == "A test prompt"
        assert config.azure_deployment == "gpt-4"
        assert config.parameters == {"frequency_penalty": 0}
        assert "helpful assistant" in config.system_template
        assert "{{ name }}" in config.user_template

    def test_parses_sample_inputs(self, tmp_path):
        content = (
            "---\n"
            "name: Sample Test\n"
            "model:\n"
            "  api: chat\n"
            "  configuration:\n"
            "    azure_deployment: gpt-4\n"
            "sample:\n"
            "  language: python\n"
            "  query: test query\n"
            "---\n"
            "system:\n"
            "Review {{ language }} code.\n"
            "\n"
            "user:\n"
            "{{ query }}\n"
        )
        prompty_file = tmp_path / "test.prompty"
        prompty_file.write_text(content, encoding="utf-8")
        config = _parse_prompty(prompty_file)
        assert config.sample == {"language": "python", "query": "test query"}

    def test_resolves_env_var_in_endpoint(self, tmp_path, monkeypatch):
        monkeypatch.setenv("TEST_ENDPOINT", "https://example.azure.com")
        content = (
            "---\n"
            "name: Env Test\n"
            "model:\n"
            "  api: chat\n"
            "  configuration:\n"
            "    azure_endpoint: ${env:TEST_ENDPOINT}\n"
            "    azure_deployment: gpt-4\n"
            "---\n"
            "system:\n"
            "Hello\n"
            "\n"
            "user:\n"
            "World\n"
        )
        prompty_file = tmp_path / "test.prompty"
        prompty_file.write_text(content, encoding="utf-8")
        config = _parse_prompty(prompty_file)
        assert config.azure_endpoint == "https://example.azure.com"

    def test_loads_file_reference_for_response_format(self, tmp_path):
        schema = {"type": "json_schema", "json_schema": {"name": "result", "schema": {"type": "object"}}}
        schema_file = tmp_path / "schema.json"
        schema_file.write_text(json.dumps(schema), encoding="utf-8")
        content = (
            "---\n"
            "name: Schema Test\n"
            "model:\n"
            "  api: chat\n"
            "  configuration:\n"
            "    azure_deployment: gpt-4\n"
            "  parameters:\n"
            "    response_format: ${file:schema.json}\n"
            "---\n"
            "system:\n"
            "Hello\n"
            "\n"
            "user:\n"
            "World\n"
        )
        prompty_file = tmp_path / "test.prompty"
        prompty_file.write_text(content, encoding="utf-8")
        config = _parse_prompty(prompty_file)
        assert config.response_format == schema
        assert "response_format" not in config.parameters

    def test_raises_on_missing_file(self):
        with pytest.raises(FileNotFoundError, match="Prompty file not found"):
            _parse_prompty("/nonexistent/path/test.prompty")

    def test_raises_on_invalid_format(self, tmp_path):
        prompty_file = tmp_path / "bad.prompty"
        prompty_file.write_text("no front matter here", encoding="utf-8")
        with pytest.raises(ValueError, match="missing YAML front matter"):
            _parse_prompty(prompty_file)

    def test_filters_response_format_from_parameters(self, tmp_path):
        content = (
            "---\n"
            "name: Param Test\n"
            "model:\n"
            "  api: chat\n"
            "  configuration:\n"
            "    azure_deployment: gpt-4\n"
            "  parameters:\n"
            "    frequency_penalty: 0\n"
            "    max_completion_tokens: 8000\n"
            "    response_format:\n"
            "      type: json_object\n"
            "---\n"
            "system:\n"
            "Hello\n"
            "\n"
            "user:\n"
            "World\n"
        )
        prompty_file = tmp_path / "test.prompty"
        prompty_file.write_text(content, encoding="utf-8")
        config = _parse_prompty(prompty_file)
        assert "response_format" not in config.parameters
        assert config.parameters == {"frequency_penalty": 0, "max_completion_tokens": 8000}
        assert config.response_format == {"type": "json_object"}
