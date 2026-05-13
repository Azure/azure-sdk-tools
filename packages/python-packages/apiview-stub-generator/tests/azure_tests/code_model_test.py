# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
"""Integration tests: StubGenerator --code-model-path end-to-end."""

import json
import os
import sys

import pytest
import yaml

from apistub import StubGenerator
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from _test_util import render_api_view_markdown


# ---------------------------------------------------------------------------
# A self-contained code-model that exercises the main constructs.
# ---------------------------------------------------------------------------

CODE_MODEL = {
    "namespace": "azure.widgets",
    "packageName": "azure-widgets",
    "packageVersion": "1.0.0b1",
    "crossLanguagePackageId": "Azure.Widgets",
    "types": [
        {
            "type": "enum",
            "name": "widgetStatus",
            "crossLanguageDefinitionId": "Azure.Widgets.WidgetStatus",
            "values": [
                {"name": "ACTIVE", "value": "active"},
                {"name": "INACTIVE", "value": "inactive"},
            ],
        },
        {
            "type": "model",
            "name": "Widget",
            "usage": 3,
            "crossLanguageDefinitionId": "Azure.Widgets.Widget",
            "parents": [],
            "properties": [
                {"clientName": "widget_id", "type": {"type": "string"}, "optional": False},
                {"clientName": "status", "type": {"type": "enum", "name": "widgetStatus"}, "optional": True},
                {"clientName": "tags", "type": {"type": "dict", "valueType": {"type": "string"}}, "optional": True},
            ],
        },
        {
            "type": "model",
            "name": "WidgetList",
            "usage": 2,  # output only – should still render
            "crossLanguageDefinitionId": "Azure.Widgets.WidgetList",
            "parents": [],
            "properties": [
                {"clientName": "items", "type": {"type": "list", "elementType": {"type": "model", "name": "Widget"}}, "optional": False},
            ],
        },
    ],
    "clients": [
        {
            "name": "WidgetsClient",
            "parameters": [
                {
                    "clientName": "endpoint",
                    "type": {"type": "string"},
                    "optional": False,
                    "location": "path",
                },
                {
                    "clientName": "credential",
                    "type": {"type": "credential"},
                    "optional": False,
                    "location": "other",
                },
            ],
            "operationGroups": [
                {
                    "identifyName": "widgets",
                    "propertyName": "widgets",
                    "className": "WidgetsOperations",
                    "operations": [
                        {
                            "name": "get",
                            "crossLanguageDefinitionId": "Azure.Widgets.Widgets.get",
                            "parameters": [
                                {
                                    "clientName": "widget_id",
                                    "type": {"type": "string"},
                                    "optional": False,
                                    "location": "path",
                                }
                            ],
                            "responses": [{"type": {"type": "model", "name": "Widget"}}],
                            "overloads": [],
                        },
                        {
                            "name": "list",
                            "discriminator": "paging",
                            "crossLanguageDefinitionId": "Azure.Widgets.Widgets.list",
                            "parameters": [],
                            "responses": [{"type": {"type": "model", "name": "Widget"}}],
                            "overloads": [],
                        },
                        {
                            "name": "create",
                            "crossLanguageDefinitionId": "Azure.Widgets.Widgets.create",
                            "parameters": [
                                {
                                    "clientName": "body",
                                    "type": {"type": "model", "name": "Widget"},
                                    "optional": False,
                                    "location": "body",
                                }
                            ],
                            "responses": [{"type": {"type": "model", "name": "Widget"}}],
                            "overloads": [
                                {
                                    "name": "create",
                                    "parameters": [
                                        {"clientName": "body", "type": {"type": "model", "name": "Widget"}, "optional": False, "location": "body"}
                                    ],
                                    "responses": [],
                                    "overloads": [],
                                },
                                {
                                    "name": "create",
                                    "parameters": [
                                        {"clientName": "body", "type": {"type": "dict", "valueType": {"type": "any"}}, "optional": False, "location": "body"}
                                    ],
                                    "responses": [],
                                    "overloads": [],
                                },
                            ],
                        },
                    ],
                    "operationGroups": [],
                }
            ],
        }
    ],
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _write_yaml(path, data):
    with open(path, "w", encoding="utf-8") as f:
        yaml.dump(data, f)


def _run_stub_generator(code_model_path):
    """Run StubGenerator with --code-model-path and return (apiview, token_json_dict)."""
    gen = StubGenerator(
        code_model_path=code_model_path,
        source_url=None,
        filter_namespace=None,
        mapping_path=None,
        pkg_path=None,
        verbose=False,
    )
    apiview = gen.generate_tokens()
    json_str = gen.serialize(apiview)
    token_json = json.loads(json_str)
    return apiview, token_json


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------


class TestStubGeneratorCodeModelPath:
    @pytest.fixture(autouse=True)
    def _setup(self, tmp_path):
        self.yaml_path = str(tmp_path / "code-model.yaml")
        _write_yaml(self.yaml_path, CODE_MODEL)

    def test_apiview_object_returned(self):
        apiview, _ = _run_stub_generator(self.yaml_path)
        assert apiview is not None

    def test_package_name_in_token_file(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        assert token_json.get("PackageName") == "azure-widgets"

    def test_package_version_in_token_file(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        assert token_json.get("PackageVersion") == "1.0.0b1"

    def test_review_lines_not_empty(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        assert len(token_json.get("ReviewLines", [])) > 0

    def test_enum_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert "WidgetStatus" in text

    def test_model_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert "Widget" in text

    def test_client_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert "WidgetsClient" in text

    def test_operation_group_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert "WidgetsOperations" in text

    def test_operations_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert '"get"' in text
        assert '"list"' in text
        assert '"create"' in text

    def test_overload_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert "overload" in text

    def test_paging_return_type_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        text = json.dumps(token_json)
        assert "ItemPaged" in text

    def test_cross_language_metadata_in_output(self):
        _, token_json = _run_stub_generator(self.yaml_path)
        meta = token_json.get("CrossLanguageMetadata")
        assert meta is not None
        assert meta.get("CrossLanguagePackageId") == "Azure.Widgets"

    def test_apiview_package_name(self):
        apiview, _ = _run_stub_generator(self.yaml_path)
        assert apiview.package_name == "azure-widgets"

    def test_apiview_package_version(self):
        apiview, _ = _run_stub_generator(self.yaml_path)
        assert apiview.package_version == "1.0.0b1"

    def test_markdown_contains_key_symbols(self):
        """Render api.md via Export-APIViewMarkdown.ps1 and spot-check key symbols."""
        apiview, _ = _run_stub_generator(self.yaml_path)
        md = render_api_view_markdown(apiview)
        assert "WidgetStatus" in md
        assert "Widget" in md
        assert "WidgetsClient" in md
        assert "WidgetsOperations" in md
        assert "def get" in md
        assert "def list" in md
        assert "ItemPaged" in md

    def test_file_not_found_raises(self):
        with pytest.raises(SystemExit):
            StubGenerator(
                code_model_path="/nonexistent/code-model.yaml",
                source_url=None,
                filter_namespace=None,
                mapping_path=None,
                pkg_path=None,
                verbose=False,
            )
