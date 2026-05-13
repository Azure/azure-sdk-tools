# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
"""CodeModelParser: generates APIView token files from a preprocessed code-model.yaml.

The code-model.yaml is written by the TypeSpec http-client-python emitter (pygen)
*after* TCGC preprocessing, so all Python names (clientName, snakeCaseName,
className, propertyName) are already set and all client.tsp customisations
(@client, @operationGroup, @override, @access, @usage) have been applied.

The emitter invokes apistub with --code-model-path (and --skip-pylint) to generate
api.md directly, without installing or inspecting the generated Python SDK.
Users who need the full package-inspection path (e.g. to pick up handwritten
customisations from _patch.py) opt out at the emitter level; this parser is not
involved in that decision.
"""

from __future__ import annotations

import logging
from typing import Any, Dict, List, Optional

from apistub._generated.treestyle.parser.models import ApiView, ReviewLines

logger = logging.getLogger(__name__)

# ---------------------------------------------------------------------------
# Type annotation rendering
# ---------------------------------------------------------------------------

# YAML "type" discriminator → Python annotation string (no Optional wrapper).
_PRIMITIVE_MAP: Dict[str, str] = {
    "string": "str",
    "integer": "int",
    "float": "float",
    "decimal": "decimal.Decimal",
    "boolean": "bool",
    "binary": "IO[bytes]",
    "bytes": "bytes",
    "any": "Any",
    "any-object": "JSON",
    "utcDateTime": "datetime",
    "offsetDateTime": "datetime",
    "plainDate": "date",
    "plainTime": "time",
    "duration": "timedelta",
    "unixtime": "datetime",
    "credential": "str",
}

# Credential / policy types that appear in parameters but are not user-visible types.
_CREDENTIAL_TYPES = {"OAuth2", "Key", "ARMChallengeAuthenticationPolicy",
                     "BearerTokenCredentialPolicy", "KeyCredentialPolicy"}


def _type_annotation(type_dict: Dict[str, Any], *, is_optional: bool = False) -> str:
    """Recursively convert a YAML type descriptor to a Python annotation string."""
    if not type_dict:
        return "Any"

    t = type_dict.get("type", "")

    # Credential types
    if t in _CREDENTIAL_TYPES:
        ann = "TokenCredential"
        return f"Optional[{ann}]" if is_optional else ann

    # Primitive
    if t in _PRIMITIVE_MAP:
        ann = _PRIMITIVE_MAP[t]
        return f"Optional[{ann}]" if is_optional else ann

    # model → "_models.ClassName"
    if t == "model":
        name = type_dict.get("name", "Unknown")
        ann = f"_models.{name}"
        return f"Optional[{ann}]" if is_optional else ann

    # enum → "Union[str, _models.EnumName]"
    if t == "enum":
        name = type_dict.get("name", "Unknown")
        # Capitalise first letter as pygen does
        name = name[0].upper() + name[1:] if name else name
        ann = f"Union[str, _models.{name}]"
        return f"Optional[{ann}]" if is_optional else ann

    # constant → use valueType
    if t == "constant":
        val = type_dict.get("value")
        value_type = type_dict.get("valueType", {})
        if val is not None:
            base = _PRIMITIVE_MAP.get(value_type.get("type", ""), "str")
            if isinstance(val, str):
                ann = f'Literal["{val}"]'
            else:
                ann = f"Literal[{val}]"
        else:
            ann = _type_annotation(value_type)
        return f"Optional[{ann}]" if is_optional else ann

    # list → "List[ElementType]"
    if t == "list":
        elem = type_dict.get("elementType", {})
        inner = _type_annotation(elem)
        ann = f"List[{inner}]"
        return f"Optional[{ann}]" if is_optional else ann

    # dict → "Dict[str, ValueType]"
    if t == "dict":
        val = type_dict.get("valueType", {})
        inner = _type_annotation(val)
        ann = f"Dict[str, {inner}]"
        return f"Optional[{ann}]" if is_optional else ann

    # combined / union → "Union[A, B]"
    if t == "combined":
        types = type_dict.get("types", [])
        parts = [_type_annotation(sub) for sub in types]
        if len(parts) == 1:
            ann = parts[0]
        else:
            ann = f"Union[{', '.join(parts)}]"
        return f"Optional[{ann}]" if is_optional else ann

    # sdkcore / multipartfile / external — render as their name or generic
    if t in ("sdkcore", "multipartfile", "external"):
        name = type_dict.get("name") or type_dict.get("xmlName") or t
        ann = name
        return f"Optional[{ann}]" if is_optional else ann

    # enumvalue → Literal
    if t == "enumvalue":
        parent = type_dict.get("enumType", {}).get("name", "")
        name = type_dict.get("name", "")
        if parent:
            ann = f"Literal[_models.{parent}.{name}]"
        else:
            ann = f'Literal["{name}"]'
        return f"Optional[{ann}]" if is_optional else ann

    # Fallback
    logger.debug("Unknown YAML type %r, falling back to Any", t)
    return "Optional[Any]" if is_optional else "Any"


# ---------------------------------------------------------------------------
# Helper: build method signature string from a list of parameter dicts
# ---------------------------------------------------------------------------

def _build_method_signature(params: List[Dict[str, Any]], *, is_async: bool = False) -> str:
    """Render a comma-separated parameter string for a method, grouped as
    positional → ``*`` → keyword-only → ``**kwargs``.

    Returns the inner signature (no ``self``, no ``def name(``).
    """
    # Separate by location
    POSITIONAL_LOCATIONS = {"path", "query", "header", "body", "positional"}
    KWARG_LOCATION = "kwarg"
    KW_ONLY_LOCATION = "keywordOnly"

    positional: List[str] = []
    kw_only: List[str] = []
    has_kwargs = False

    for p in params:
        if p.get("inOverload") or p.get("hideInMethod"):
            continue
        client_name = p.get("clientName", p.get("name", ""))
        location = p.get("location", "")
        optional = p.get("optional", False)
        type_dict = p.get("type", {})
        annotation = _type_annotation(type_dict, is_optional=optional)

        default_val = p.get("clientDefaultValue")
        # **kwargs placeholder
        if client_name == "kwargs" or location == KWARG_LOCATION:
            has_kwargs = True
            continue

        if default_val is not None:
            if isinstance(default_val, str):
                default_str = f' = "{default_val}"'
            elif isinstance(default_val, bool):
                default_str = f" = {default_val}"
            elif default_val is None:
                default_str = " = None"
            else:
                default_str = f" = {default_val}"
        elif optional:
            default_str = " = None"
        else:
            default_str = ""

        rendered = f"{client_name}: {annotation}{default_str}"

        if location in POSITIONAL_LOCATIONS:
            positional.append(rendered)
        else:
            kw_only.append(rendered)

    parts: List[str] = ["self"]
    parts.extend(positional)
    if kw_only:
        parts.append("*")
        parts.extend(kw_only)
    if has_kwargs:
        parts.append("**kwargs: Any")

    return ", ".join(parts)


# ---------------------------------------------------------------------------
# Main parser class
# ---------------------------------------------------------------------------

class CodeModelParser:
    """Generates an ApiView token file from a preprocessed code-model.yaml dict."""

    def __init__(
        self,
        yaml_data: Dict[str, Any],
        *,
        mapping_path: Optional[str] = None,
        source_url: Optional[str] = None,
    ):
        self._yaml = yaml_data
        self._mapping_path = mapping_path
        self._source_url = source_url

    # ------------------------------------------------------------------
    # Public entry point
    # ------------------------------------------------------------------

    def generate_tokens(self) -> ApiView:
        """Walk the YAML and produce a fully populated ApiView."""
        namespace = self._yaml.get("namespace", "")
        pkg_name = self._yaml.get("packageName") or namespace
        pkg_version = self._yaml.get("packageVersion") or ""
        cross_language_package_id = self._yaml.get("crossLanguagePackageId")

        # Build cross-language map from all types and operations
        cross_language_map = self._build_cross_language_map()
        cross_language_version = None  # not stored in YAML directly

        # Create MetadataMap-like wrapper
        metadata_map = _YamlMetadataMap(
            cross_language_package_id=cross_language_package_id,
            cross_language_map=cross_language_map,
            cross_language_version=cross_language_version,
        )

        apiview = ApiView(
            pkg_name=pkg_name,
            namespace=namespace,
            metadata_map=metadata_map,
            source_url=self._source_url,
            pkg_version=pkg_version,
        )
        apiview.generate_tokens()  # writes header line

        review_lines: ReviewLines = apiview.review_lines

        # Render enums, models, clients, operation groups
        self._render_enums(review_lines, apiview)
        self._render_models(review_lines, apiview)
        self._render_clients(review_lines, apiview, namespace, is_async=False)
        self._render_clients(review_lines, apiview, namespace, is_async=True)

        return apiview

    # ------------------------------------------------------------------
    # Cross-language map builder
    # ------------------------------------------------------------------

    def _build_cross_language_map(self) -> Dict[str, str]:
        """Build {python_qualified_name: crossLanguageDefinitionId} map."""
        result: Dict[str, str] = {}
        namespace = self._yaml.get("namespace", "")

        for t in self._yaml.get("types", []):
            cid = t.get("crossLanguageDefinitionId")
            if not cid:
                continue
            if t["type"] == "model":
                key = f"{namespace}.{t['name']}"
            elif t["type"] == "enum":
                name = t["name"][0].upper() + t["name"][1:]
                key = f"{namespace}.{name}"
            else:
                continue
            result[key] = cid

        for client in self._yaml.get("clients", []):
            for og in _all_operation_groups(client.get("operationGroups", [])):
                class_name = og.get("className", "")
                for op in og.get("operations", []):
                    cid = op.get("crossLanguageDefinitionId")
                    if cid:
                        op_name = op.get("name", "")
                        key = f"{namespace}.{class_name}.{op_name}"
                        result[key] = cid
                        # async variant
                        key_async = f"{namespace}.aio.{class_name}.{op_name}"
                        result[key_async] = cid

        return result

    # ------------------------------------------------------------------
    # Enums
    # ------------------------------------------------------------------

    def _render_enums(self, review_lines: ReviewLines, apiview: ApiView):
        namespace = self._yaml.get("namespace", "")
        for type_dict in self._yaml.get("types", []):
            if type_dict.get("type") != "enum":
                continue
            self._render_enum(review_lines, apiview, type_dict, namespace)

    def _render_enum(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        type_dict: Dict[str, Any],
        namespace: str,
    ):
        raw_name = type_dict.get("name", "Unknown")
        name = raw_name[0].upper() + raw_name[1:] if raw_name else raw_name
        line_id = f"{namespace}.{name}"
        cid = type_dict.get("crossLanguageDefinitionId")

        line = review_lines.create_review_line(line_id=line_id)
        if cid:
            line.cross_language_id = cid
        line.add_keyword("class")
        line.add_text(name, navigation_display_name=name)
        line.add_punctuation("(", has_suffix_space=False)
        line.add_type("str", apiview)
        line.add_punctuation(",", has_suffix_space=True)
        line.add_type("Enum", apiview)
        line.add_punctuation(")", has_suffix_space=False)
        line.add_punctuation(":")

        children = ReviewLines()
        for val in type_dict.get("values", []):
            val_name = val.get("name", "")
            value = val.get("value", "")
            val_line_id = f"{line_id}.{val_name}"
            val_line = children.create_review_line(line_id=val_line_id)
            val_line.add_text(val_name)
            val_line.add_punctuation("=")
            if isinstance(value, str):
                val_line.add_string_literal(value, has_suffix_space=False)
            else:
                val_line.add_literal(str(value), has_suffix_space=False)
            children.append(val_line)

        children.set_blank_lines(last_is_context_end_line=True)
        line.add_children(children)
        review_lines.append(line)
        review_lines.set_blank_lines(2)

    # ------------------------------------------------------------------
    # Models
    # ------------------------------------------------------------------

    def _render_models(self, review_lines: ReviewLines, apiview: ApiView):
        namespace = self._yaml.get("namespace", "")
        for type_dict in self._yaml.get("types", []):
            if type_dict.get("type") not in ("model",):
                continue
            # Skip internal / error-only models
            usage = type_dict.get("usage", 0)
            if usage == 0:
                continue
            self._render_model(review_lines, apiview, type_dict, namespace)

    def _render_model(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        type_dict: Dict[str, Any],
        namespace: str,
    ):
        name = type_dict.get("name", "Unknown")
        line_id = f"{namespace}.{name}"
        cid = type_dict.get("crossLanguageDefinitionId")

        parents = type_dict.get("parents", [])
        parent_names = [p.get("name", "") for p in parents if p.get("name")]

        line = review_lines.create_review_line(line_id=line_id)
        if cid:
            line.cross_language_id = cid
        line.add_keyword("class")
        line.add_text(name, navigation_display_name=name)
        if parent_names:
            line.add_punctuation("(", has_suffix_space=False)
            for i, pname in enumerate(parent_names):
                line.add_type(pname, apiview, has_suffix_space=False)
                if i < len(parent_names) - 1:
                    line.add_punctuation(",", has_suffix_space=True)
            line.add_punctuation(")", has_suffix_space=False)
        line.add_punctuation(":")

        children = ReviewLines()
        props = type_dict.get("properties", [])
        if not props:
            # Empty class body
            pass_line = children.create_review_line()
            pass_line.add_punctuation("...", has_suffix_space=False)
            children.append(pass_line)
        else:
            for prop in props:
                self._render_property(children, apiview, prop, line_id)

        children.set_blank_lines(last_is_context_end_line=True)
        line.add_children(children)
        review_lines.append(line)
        review_lines.set_blank_lines(2)

    def _render_property(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        prop: Dict[str, Any],
        parent_line_id: str,
    ):
        client_name = prop.get("clientName", prop.get("name", ""))
        line_id = f"{parent_line_id}.{client_name}"
        optional = prop.get("optional", False)
        readonly = prop.get("readOnly", False)
        type_dict = prop.get("type", {})
        annotation = _type_annotation(type_dict, is_optional=optional)

        line = review_lines.create_review_line(line_id=line_id)
        line.add_text(client_name)
        line.add_punctuation(":", has_suffix_space=True)
        line.add_type(annotation, apiview, has_suffix_space=False)
        if optional:
            line.add_text(" = None", has_prefix_space=False, has_suffix_space=False)
        review_lines.append(line)

    # ------------------------------------------------------------------
    # Clients
    # ------------------------------------------------------------------

    def _render_clients(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        namespace: str,
        is_async: bool,
    ):
        ns = f"{namespace}.aio" if is_async else namespace
        for client in self._yaml.get("clients", []):
            self._render_client(review_lines, apiview, client, ns, is_async)

    def _render_client(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        client: Dict[str, Any],
        namespace: str,
        is_async: bool,
    ):
        name = client.get("name", "UnknownClient")
        line_id = f"{namespace}.{name}"

        line = review_lines.create_review_line(line_id=line_id)
        line.add_keyword("class")
        line.add_text(name, navigation_display_name=name)
        line.add_punctuation(":")

        children = ReviewLines()

        # __init__
        self._render_init(children, apiview, client, line_id, is_async)

        # Operation group properties
        for og in client.get("operationGroups", []):
            og_prop = og.get("propertyName", "")
            og_class = og.get("className", "")
            if og_prop and og_class:
                og_line_id = f"{line_id}.{og_prop}"
                og_line = children.create_review_line(line_id=og_line_id)
                og_line.add_text(og_prop)
                og_line.add_punctuation(":", has_suffix_space=True)
                og_line.add_type(og_class, apiview, has_suffix_space=False)
                children.append(og_line)

        children.set_blank_lines(last_is_context_end_line=True)
        line.add_children(children)
        review_lines.append(line)
        review_lines.set_blank_lines(2)

        # Render each operation group class
        for og in _all_operation_groups(client.get("operationGroups", [])):
            self._render_operation_group(review_lines, apiview, og, namespace, is_async)

    def _render_init(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        client: Dict[str, Any],
        parent_line_id: str,
        is_async: bool,
    ):
        line_id = f"{parent_line_id}.__init__"
        params = client.get("parameters", [])
        sig = _build_method_signature(params, is_async=is_async)

        line = review_lines.create_review_line(line_id=line_id)
        line.add_keyword("def")
        line.add_text("__init__")
        line.add_punctuation("(", has_suffix_space=False)
        line.add_text(sig, has_prefix_space=False, has_suffix_space=False)
        line.add_punctuation(")", has_suffix_space=False)
        line.add_punctuation(":")

        body = ReviewLines()
        dots = body.create_review_line()
        dots.add_punctuation("...", has_suffix_space=False)
        body.append(dots)
        line.add_children(body)
        review_lines.append(line)
        review_lines.set_blank_lines(1)

    # ------------------------------------------------------------------
    # Operation groups
    # ------------------------------------------------------------------

    def _render_operation_group(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        og: Dict[str, Any],
        namespace: str,
        is_async: bool,
    ):
        class_name = og.get("className", "UnknownOperations")
        # mixin groups have an empty identify_name and a leading underscore
        is_mixin = not og.get("identifyName", "")
        if is_mixin:
            class_name = f"_{class_name}" if not class_name.startswith("_") else class_name
        line_id = f"{namespace}.{class_name}"

        line = review_lines.create_review_line(line_id=line_id)
        line.add_keyword("class")
        line.add_text(class_name, navigation_display_name=class_name)
        line.add_punctuation(":")

        children = ReviewLines()

        for op in og.get("operations", []):
            # Render @overload stubs first
            for overload in op.get("overloads", []):
                self._render_operation(children, apiview, overload, line_id, is_async, is_overload=True)
            # Render main method
            self._render_operation(children, apiview, op, line_id, is_async, is_overload=False)

        children.set_blank_lines(last_is_context_end_line=True)
        line.add_children(children)
        review_lines.append(line)
        review_lines.set_blank_lines(2)

    def _render_operation(
        self,
        review_lines: ReviewLines,
        apiview: ApiView,
        op: Dict[str, Any],
        parent_line_id: str,
        is_async: bool,
        is_overload: bool,
    ):
        op_name = op.get("name", "unknown")
        line_id = f"{parent_line_id}.{op_name}"
        cid = op.get("crossLanguageDefinitionId")
        params = op.get("parameters", [])
        sig = _build_method_signature(params, is_async=is_async)
        return_type = self._build_return_type(op)

        if is_overload:
            dec_line = review_lines.create_review_line()
            dec_line.add_punctuation("@", has_suffix_space=False)
            dec_line.add_text("overload", has_prefix_space=False)
            review_lines.append(dec_line)

        line = review_lines.create_review_line(line_id=line_id if not is_overload else None)
        if not is_overload and cid:
            line.cross_language_id = cid
        if is_async:
            line.add_keyword("async")
        line.add_keyword("def")
        line.add_text(op_name, navigation_display_name=op_name if not is_overload else None)
        line.add_punctuation("(", has_suffix_space=False)
        line.add_text(sig, has_prefix_space=False, has_suffix_space=False)
        line.add_punctuation(")", has_suffix_space=False)
        if return_type:
            line.add_text("->", has_prefix_space=True, has_suffix_space=True)
            line.add_type(return_type, apiview, has_suffix_space=False)
        line.add_punctuation(":")

        body = ReviewLines()
        dots = body.create_review_line()
        dots.add_punctuation("...", has_suffix_space=False)
        body.append(dots)
        line.add_children(body)
        review_lines.append(line)
        review_lines.set_blank_lines(1)

    def _build_return_type(self, op: Dict[str, Any]) -> Optional[str]:
        """Derive the Python return type annotation from an operation dict."""
        discriminator = op.get("discriminator", "")
        responses = op.get("responses", [])

        # Collect all non-None body types from responses
        return_types = []
        for resp in responses:
            body_type = resp.get("type")
            if body_type:
                ann = _type_annotation(body_type)
                if ann and ann not in return_types:
                    return_types.append(ann)

        if discriminator == "paging":
            inner = return_types[0] if return_types else "Any"
            return f"ItemPaged[{inner}]"
        if discriminator in ("lro", "lropaging"):
            inner = return_types[0] if return_types else "Any"
            return f"LROPoller[{inner}]"

        if not return_types:
            return "None"
        if len(return_types) == 1:
            return return_types[0]
        return f"Union[{', '.join(return_types)}]"


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _all_operation_groups(groups: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """Flatten nested operation groups (BFS)."""
    result = []
    queue = list(groups)
    while queue:
        og = queue.pop(0)
        result.append(og)
        queue.extend(og.get("operationGroups", []))
    return result


class _YamlMetadataMap:
    """Minimal MetadataMap-like object populated from YAML data."""

    def __init__(
        self,
        *,
        cross_language_package_id: Optional[str],
        cross_language_map: Dict[str, str],
        cross_language_version: Optional[str],
    ):
        self.cross_language_package_id = cross_language_package_id or ""
        self.cross_language_map = cross_language_map
        self.cross_language_version = cross_language_version
