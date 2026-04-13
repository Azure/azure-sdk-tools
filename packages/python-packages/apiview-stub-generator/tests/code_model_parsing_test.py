# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
"""Unit tests for CodeModelParser and _type_annotation."""

import pytest
from apistub.nodes._code_model_parser import CodeModelParser, _type_annotation


# ---------------------------------------------------------------------------
# _type_annotation tests
# ---------------------------------------------------------------------------


def _ann(type_dict, optional=False):
    return _type_annotation(type_dict, is_optional=optional)


class TestTypeAnnotation:
    def test_string(self):
        assert _ann({"type": "string"}) == "str"

    def test_integer(self):
        assert _ann({"type": "integer"}) == "int"

    def test_float(self):
        assert _ann({"type": "float"}) == "float"

    def test_boolean(self):
        assert _ann({"type": "boolean"}) == "bool"

    def test_binary(self):
        assert _ann({"type": "binary"}) == "IO[bytes]"

    def test_any(self):
        assert _ann({"type": "any"}) == "Any"

    def test_any_object(self):
        assert _ann({"type": "any-object"}) == "JSON"

    def test_datetime(self):
        assert _ann({"type": "utcDateTime"}) == "datetime"
        assert _ann({"type": "offsetDateTime"}) == "datetime"

    def test_bytes(self):
        assert _ann({"type": "bytes"}) == "bytes"

    def test_duration(self):
        assert _ann({"type": "duration"}) == "timedelta"

    def test_optional_wraps(self):
        assert _ann({"type": "string"}, optional=True) == "Optional[str]"

    def test_model(self):
        assert _ann({"type": "model", "name": "Widget"}) == "_models.Widget"

    def test_model_optional(self):
        assert _ann({"type": "model", "name": "Widget"}, optional=True) == "Optional[_models.Widget]"

    def test_enum(self):
        # enum name should be capitalised
        assert _ann({"type": "enum", "name": "myStatus"}) == "Union[str, _models.MyStatus]"

    def test_enum_already_capitalised(self):
        assert _ann({"type": "enum", "name": "Status"}) == "Union[str, _models.Status]"

    def test_list_of_string(self):
        assert _ann({"type": "list", "elementType": {"type": "string"}}) == "List[str]"

    def test_list_of_model(self):
        assert _ann({"type": "list", "elementType": {"type": "model", "name": "Widget"}}) == "List[_models.Widget]"

    def test_dict_of_int(self):
        assert _ann({"type": "dict", "valueType": {"type": "integer"}}) == "Dict[str, int]"

    def test_constant_string(self):
        assert _ann({"type": "constant", "value": "xml", "valueType": {"type": "string"}}) == 'Literal["xml"]'

    def test_constant_int(self):
        assert _ann({"type": "constant", "value": 5, "valueType": {"type": "integer"}}) == "Literal[5]"

    def test_combined(self):
        ann = _ann({"type": "combined", "types": [{"type": "string"}, {"type": "integer"}]})
        assert ann == "Union[str, int]"

    def test_combined_single(self):
        assert _ann({"type": "combined", "types": [{"type": "boolean"}]}) == "bool"

    def test_unknown_falls_back_to_any(self):
        assert _ann({"type": "unknownFuture"}) == "Any"

    def test_empty_dict(self):
        assert _ann({}) == "Any"


# ---------------------------------------------------------------------------
# Minimal YAML fixtures
# ---------------------------------------------------------------------------

MINIMAL_YAML = {
    "namespace": "azure.foo",
    "packageName": "azure-foo",
    "packageVersion": "1.0.0b1",
    "crossLanguagePackageId": "Azure.Foo",
    "types": [],
    "clients": [],
}


def _make_enum(**kwargs):
    base = {
        "type": "enum",
        "name": "MyStatus",
        "crossLanguageDefinitionId": "Azure.Foo.MyStatus",
        "values": [
            {"name": "ACTIVE", "value": "active"},
            {"name": "INACTIVE", "value": "inactive"},
        ],
    }
    base.update(kwargs)
    return base


def _make_model(**kwargs):
    base = {
        "type": "model",
        "name": "Widget",
        "usage": 3,
        "crossLanguageDefinitionId": "Azure.Foo.Widget",
        "properties": [
            {"clientName": "name", "type": {"type": "string"}, "optional": False},
        ],
        "parents": [],
    }
    base.update(kwargs)
    return base


def _make_client(operations=None):
    ops = operations or [
        {
            "name": "get_widget",
            "crossLanguageDefinitionId": "Azure.Foo.Widgets.getWidget",
            "parameters": [
                {"clientName": "widget_id", "type": {"type": "string"}, "optional": False, "location": "path"},
            ],
            "responses": [{"type": {"type": "model", "name": "Widget"}}],
            "overloads": [],
        }
    ]
    return {
        "name": "FooClient",
        "parameters": [
            {"clientName": "endpoint", "type": {"type": "string"}, "optional": False, "location": "path"},
            {"clientName": "credential", "type": {"type": "credential"}, "optional": False, "location": "other"},
        ],
        "operationGroups": [
            {
                "identifyName": "widgets",
                "propertyName": "widgets",
                "className": "WidgetsOperations",
                "operations": ops,
                "operationGroups": [],
            }
        ],
    }


def _parse(yaml_data):
    parser = CodeModelParser(yaml_data)
    return parser.generate_tokens()


def _rendered(apiview):
    return "".join(apiview.review_lines.render())


# ---------------------------------------------------------------------------
# CodeModelParser tests
# ---------------------------------------------------------------------------


class TestCodeModelParserEnum:
    def test_enum_class_header_present(self):
        yaml_data = {**MINIMAL_YAML, "types": [_make_enum()]}
        text = _rendered(_parse(yaml_data))
        assert "class MyStatus" in text
        assert "str" in text
        assert "Enum" in text

    def test_enum_values_present(self):
        yaml_data = {**MINIMAL_YAML, "types": [_make_enum()]}
        text = _rendered(_parse(yaml_data))
        assert "ACTIVE" in text
        assert "INACTIVE" in text

    def test_enum_cross_language_id_set(self):
        yaml_data = {**MINIMAL_YAML, "types": [_make_enum()]}
        apiview = _parse(yaml_data)
        line = _find_line(apiview, "azure.foo.MyStatus")
        assert line is not None
        assert line.cross_language_id == "Azure.Foo.MyStatus"

    def test_integer_enum_values(self):
        enum = _make_enum(name="Priority", values=[{"name": "HIGH", "value": 1}, {"name": "LOW", "value": 0}])
        yaml_data = {**MINIMAL_YAML, "types": [enum]}
        text = _rendered(_parse(yaml_data))
        assert "HIGH" in text
        assert "1" in text


class TestCodeModelParserModel:
    def test_model_class_header_present(self):
        yaml_data = {**MINIMAL_YAML, "types": [_make_model()]}
        text = _rendered(_parse(yaml_data))
        assert "class Widget" in text

    def test_model_property_present(self):
        yaml_data = {**MINIMAL_YAML, "types": [_make_model()]}
        text = _rendered(_parse(yaml_data))
        assert "name" in text
        assert "str" in text

    def test_model_usage_zero_excluded(self):
        yaml_data = {**MINIMAL_YAML, "types": [_make_model(usage=0)]}
        text = _rendered(_parse(yaml_data))
        assert "class Widget" not in text

    def test_model_with_parent(self):
        parent = {"type": "model", "name": "WidgetBase", "usage": 3, "properties": [], "parents": []}
        child = _make_model(parents=[{"name": "WidgetBase"}])
        yaml_data = {**MINIMAL_YAML, "types": [child, parent]}
        text = _rendered(_parse(yaml_data))
        assert "WidgetBase" in text

    def test_optional_property_has_default_none(self):
        model = _make_model(properties=[
            {"clientName": "tag", "type": {"type": "string"}, "optional": True}
        ])
        yaml_data = {**MINIMAL_YAML, "types": [model]}
        text = _rendered(_parse(yaml_data))
        assert "= None" in text


class TestCodeModelParserClient:
    def test_client_class_present(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "class FooClient" in text

    def test_client_init_present(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "__init__" in text

    def test_operation_group_property_present(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "widgets" in text
        assert "WidgetsOperations" in text

    def test_operation_group_class_present(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "class WidgetsOperations" in text

    def test_operation_present(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "def get_widget" in text

    def test_async_operation_present(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "async def get_widget" in text

    def test_operation_return_type(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        assert "Widget" in text

    def test_void_return_type(self):
        ops = [{"name": "delete_widget", "parameters": [], "responses": [], "overloads": []}]
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client(operations=ops)]}
        text = _rendered(_parse(yaml_data))
        assert "None" in text

    def test_paging_return_type(self):
        ops = [
            {
                "name": "list_widgets",
                "discriminator": "paging",
                "parameters": [],
                "responses": [{"type": {"type": "model", "name": "Widget"}}],
                "overloads": [],
            }
        ]
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client(operations=ops)]}
        text = _rendered(_parse(yaml_data))
        assert "ItemPaged" in text

    def test_lro_return_type(self):
        ops = [
            {
                "name": "create_widget",
                "discriminator": "lro",
                "parameters": [],
                "responses": [{"type": {"type": "model", "name": "Widget"}}],
                "overloads": [],
            }
        ]
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client(operations=ops)]}
        text = _rendered(_parse(yaml_data))
        assert "LROPoller" in text

    def test_overload_decorators_present(self):
        ops = [
            {
                "name": "create_widget",
                "parameters": [],
                "responses": [],
                "overloads": [
                    {"name": "create_widget", "parameters": [{"clientName": "body", "type": {"type": "model", "name": "Widget"}, "optional": False, "location": "body"}], "responses": [], "overloads": []},
                    {"name": "create_widget", "parameters": [{"clientName": "body", "type": {"type": "dict", "valueType": {"type": "any"}}, "optional": False, "location": "body"}], "responses": [], "overloads": []},
                ],
            }
        ]
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client(operations=ops)]}
        text = _rendered(_parse(yaml_data))
        assert "@overload" in text


class TestCodeModelParserMetadata:
    def test_package_name(self):
        apiview = _parse(MINIMAL_YAML)
        assert apiview.package_name == "azure-foo"

    def test_package_version(self):
        apiview = _parse(MINIMAL_YAML)
        assert apiview.package_version == "1.0.0b1"

    def test_cross_language_package_id(self):
        apiview = _parse({**MINIMAL_YAML, "types": [_make_model()]})
        # cross_language_metadata is populated from the YAML
        assert apiview.cross_language_metadata is not None
        assert apiview.cross_language_metadata.cross_language_package_id == "Azure.Foo"

    def test_aio_namespace_in_async_section(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        text = _rendered(_parse(yaml_data))
        # Async client classes are rendered under namespace.aio
        # The class is the same name but appears twice (sync + async)
        assert text.count("class FooClient") == 2

    def test_operation_cross_language_id(self):
        yaml_data = {**MINIMAL_YAML, "clients": [_make_client()]}
        apiview = _parse(yaml_data)
        line = _find_line(apiview, "azure.foo.WidgetsOperations.get_widget")
        assert line is not None
        assert line.cross_language_id == "Azure.Foo.Widgets.getWidget"


# ---------------------------------------------------------------------------
# Helper
# ---------------------------------------------------------------------------


def _find_line(apiview, line_id):
    """Recursively search for a ReviewLine with the given line_id."""

    def _search(lines):
        for line in lines:
            if getattr(line, "line_id", None) == line_id:
                return line
            children = getattr(line, "children", None)
            if children:
                result = _search(children)
                if result:
                    return result
        return None

    return _search(apiview.review_lines)
