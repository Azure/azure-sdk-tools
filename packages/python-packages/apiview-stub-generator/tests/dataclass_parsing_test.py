# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

from apistub.nodes import DataClassNode
from apistubgentest.models import (
    DataClassSimple,
    DataClassWithFields,
    DataClassDynamic,
    DataClassWithKeywordOnly,
    DataClassWithPostInit,
)

from ._test_util import _check, _tokenize, _merge_lines, _render_lines, MockApiView


class TestDataClassParsing:

    pkg_namespace = "apistubgentest.models._dataclasses"

    def test_dataclass_simple(self):
        obj = DataClassSimple
        class_node = DataClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        lines = _render_lines(_tokenize(class_node))
        assert lines[0].startswith("@dataclass")

        ivars = lines[2:5]
        _check(
            ivars,
            [
                "ivar name: str",
                'ivar quantity_on_hand: int = field(compare = True, default = 0, hash = None, init = True, kw_only = False, metadata = {}, name = "quantity_on_hand", repr = True, type = int)',
                "ivar unit_price: float",
            ],
            obj,
        )

        actual = lines[8:13]
        expected = [
            "def __init__(",
            "    name: str, ",
            "    unit_price: float, ",
            "    quantity_on_hand: int",
            ")",
        ]
        # TODO: quantity_on_hand actually has a default value that should be displayed
        # assert init_string == "def __init__(name: str, unit_price: float, quantity_on_hand: int = 0)"
        _check(actual, expected, obj)

    def test_dataclass_fields(self):
        obj = DataClassWithFields
        class_node = DataClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        lines = _render_lines(_tokenize(class_node))
        assert lines[0].startswith("@dataclass")

        ivars = lines[2:6]
        # TODO: Display missing field assignments
        _check(
            ivars,
            [
                # "ivar myint_field: int = field(repr = False)",
                "ivar myint_field: int",
                'ivar myint_field_default: int = field(compare = True, default = 10, hash = None, init = True, kw_only = False, metadata = {}, name = "myint_field_default", repr = False, type = int)',
                "ivar myint_plain: int",
                # "mylist: list[int] = field(default_factor = list)"
                "ivar mylist: list[int]",
            ],
            obj,
        )

        actual = lines[9:15]
        # TODO: init should display defaults
        # assert init_string == "def __init__(myint_plain: int, myint_field: int, myint_field_default: int = 10, mylist: list[int] = list)"
        expected = [
            "def __init__(",
            "    myint_plain: int, ",
            "    myint_field: int, ",
            "    myint_field_default: int, ",
            "    mylist: list[int]",
            ")"
        ]
        _check(actual, expected, obj)

    def test_dataclass_dynamic(self):
        obj = DataClassDynamic
        # TODO: Support make_dataclass
        try:
            class_node = DataClassNode(
                name=obj.__name__,
                namespace=obj.__name__,
                parent_node=None,
                obj=obj,
                pkg_root_namespace=self.pkg_namespace,
                apiview=MockApiView,
            )
            lines = _render_lines(_tokenize(class_node))
            assert lines[0].startswith("@dataclass")
            # TODO: Flesh this out
        except AttributeError:
            pass

    def test_dataclass_with_kw_only(self):
        obj = DataClassWithKeywordOnly
        class_node = DataClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        lines = _render_lines(_tokenize(class_node))
        assert lines[0].startswith("@dataclass")
        ivars = lines[2:5]
        _check(ivars, ["ivar x: float", "ivar y: float", "ivar z: float"], obj)

        actual = lines[8:13]
        expected = [
            "def __init__(",
            "    x: float, ",
            "    y: float, ",
            "    z: float",
            ")",
        ]
        # TODO: init should display keyword only marker '*'
        # assert init_string == "def __init__(x: float, *, y: float, z: float)"
        _check(actual, expected, obj)

    def test_dataclass_with_post_init(self):
        obj = DataClassWithPostInit
        class_node = DataClassNode(
            name=obj.__name__,
            namespace=obj.__name__,
            parent_node=None,
            obj=obj,
            pkg_root_namespace=self.pkg_namespace,
            apiview=MockApiView,
        )
        lines = _render_lines(_tokenize(class_node))
        assert lines[0].startswith("@dataclass")
        ivars = lines[2:5]
        # TODO: should display field assignment
        _check(
            ivars,
            [
                "ivar a: float",
                "ivar b: float",
                # "ivar c: float = field(init = False)"
                "ivar c: float",
            ],
            obj,
        )

        init_string = lines[8].lstrip()
        assert init_string == "def __init__(a: float, b: float)"
