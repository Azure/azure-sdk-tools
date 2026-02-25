# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------

# pylint: disable=missing-class-docstring,missing-function-docstring

"""
Test cases for SectionedDocument and LineData classes.
"""

from src._sectioned_document import LineData, Section, SectionedDocument


def test_line_data_properties():
    ld = LineData(line_no=5, indent=2, line="  foo", git_status="+")
    assert ld.line_no == 5
    assert ld.indent == 2
    assert ld.line == "  foo"
    assert ld.git_status == "+"


def test_section_start_line_no_and_str():
    lines = [
        LineData(line_no=10, indent=0, line="root", git_status=None),
        LineData(line_no=11, indent=2, line="  child", git_status=None),
    ]
    sec = Section(lines)
    assert sec.start_line_no() == 10
    # __str__ should join only the raw lines
    assert str(sec) == "root\n  child"


def test_section_numbered_and_idx_for_line_no():
    lines = [
        LineData(line_no=1, indent=0, line="a", git_status=None),
        LineData(line_no=2, indent=0, line="b", git_status="-"),
        LineData(line_no=None, indent=0, line="c", git_status=None),
    ]
    sec = Section(lines)
    expected = "\n".join(
        [
            "1: a",
            "2: -b",
            "c",
        ]
    )
    assert sec.numbered() == expected

    # idx_for_line_no should find existing
    assert sec.idx_for_line_no(2) == 1
    # and return None for missing
    assert sec.idx_for_line_no(999) is None


def test_sectioned_document_no_top_level_all_in_one():
    # all lines have indent > base_indent (0), so one section
    ld = [
        LineData(line_no=1, indent=2, line="  foo"),
        LineData(line_no=2, indent=2, line="  bar"),
    ]
    doc = SectionedDocument(line_data=ld, base_indent=0)
    # should produce exactly one section containing both lines
    assert len(doc) == 1
    sec = next(iter(doc))
    assert sec.lines == ld


def test_sectioned_document_basic_splitting():
    # simulate lines with explicit "1: " style input
    raw = [
        "1: root1",
        "2:   child1",
        "3: root2",
        "4:   child2",
    ]
    # build from raw lines
    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=2)
    # expect two sections: [root1, child1] and [root2, child2]
    assert len(doc) == 2
    all_sections = list(doc)
    assert [l.line.strip() for l in all_sections[0].lines] == ["root1", "child1"]
    assert [l.line.strip() for l in all_sections[1].lines] == ["root2", "child2"]

    # numbered() should join both sections with a blank line
    numbered = doc.numbered()
    parts = numbered.split("\n\n")
    assert len(parts) == 2
    # each part must start with its line_no and then the text
    assert parts[0].startswith("1: root1")
    assert "2:   child1" in parts[0]
    assert parts[1].startswith("3: root2")
    assert "4:   child2" in parts[1]


def test_sectioned_document_chunking_and_max_size():
    # build 4 simple sections of size 1 each; max_chunk_size=2 should pack them two per section
    raw = [
        "1: a",
        "2: b",
        "3: c",
        "4: d",
    ]
    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=2)
    # initial top-level sections would each be single-line sections [a], [b], [c], [d]
    # then chunked two at a time → 2 final sections
    assert len(doc) == 2
    sections = list(doc)
    assert [l.line for l in sections[0].lines] == ["a", "b"]
    assert [l.line for l in sections[1].lines] == ["c", "d"]


def test_sectioned_document_oversized_section_subdivision():
    # make one top-level section of size 3; set max_chunk_size=2 to force subdivision
    raw = [
        "1: root",
        "2:   child1",
        "3:   child2",
    ]
    # max_chunk_size=2 → section_size=3 > 2 triggers recursive subdivision
    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=2)
    # Expect two sub-sections, each containing the root + one child
    assert len(doc) == 2

    first, second = doc.sections  # pylint: disable=unbalanced-tuple-unpacking
    assert len(doc.sections[0].lines) == 2

    # both should start with the same root line
    assert first.lines[0].line == "root"
    assert second.lines[0].line == "root"
    # and then have one of the children each
    assert first.lines[1].line.strip() in ("child1", "child2")
    assert second.lines[1].line.strip() in ("child1", "child2")
    # ensure they partition the two children
    children = {first.lines[1].line.strip(), second.lines[1].line.strip()}
    assert children == {"child1", "child2"}


def test_iter_and_len_methods():
    raw = ["1: x", "2:   y"]
    doc = SectionedDocument(lines=raw)
    # __len__
    assert len(doc) == 1
    # __iter__
    sections = [sec for sec in doc]
    assert isinstance(sections[0], Section)


def test_decorator_included_in_all_chunks_when_class_is_subdivided():
    """
    Validates the hypothesis that when a class is split into multiple chunks,
    decorators above the class declaration should be included in all chunks,
    not just the first one.

    Given a class like:
        @Fluent
        public class MyFoo {
            public func bigFunc1(...)
            public func bigFunc2(...)
        }

    When split, both chunks should include the @Fluent decorator:
        Chunk 1: @Fluent, public class MyFoo { public func bigFunc1(...) }
        Chunk 2: @Fluent, public class MyFoo { public func bigFunc2(...) }

    Currently, the bug is that chunk 2 only gets the class declaration, missing @Fluent.
    """
    # Simulate a class with a decorator that needs to be split
    # Using max_chunk_size=3 to force subdivision
    raw = [
        "1: @Fluent",
        "2: public class MyFoo {",
        "3:   public func bigFunc1(...)",
        "4:   public func bigFunc2(...)",
        "5: }",
    ]

    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=3)

    # We expect multiple sections due to the size
    assert len(doc) > 1, "Expected the document to be split into multiple sections"

    # The key assertion: ALL sections that contain class content should include the decorator
    # This is the hypothesis we're testing
    for i, section in enumerate(doc.sections):
        section_text = str(section)

        # If this section contains the class declaration, it should also have the decorator
        if "public class MyFoo" in section_text:
            assert "@Fluent" in section_text, (
                f"Section {i + 1} contains 'public class MyFoo' but is missing '@Fluent' decorator.\n"
                f"Section content:\n{section_text}\n\n"
                "This validates the hypothesis that decorators are not being included "
                "when a class is split into chunks."
            )


def test_decorator_included_when_class_fits_in_single_chunk():
    """
    When a class with a decorator fits entirely in one chunk,
    the decorator should be included with the class.
    """
    raw = [
        "1: @Fluent",
        "2: public class MyFoo {",
        "3:   public func smallFunc(...)",
        "4: }",
    ]

    # max_chunk_size=10 ensures everything fits in one chunk
    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=10)

    assert len(doc) == 1, "Expected the document to fit in a single section"

    section_text = str(doc.sections[0])
    assert "@Fluent" in section_text, "Decorator should be included in the section"
    assert "public class MyFoo" in section_text, "Class declaration should be included"
    assert "smallFunc" in section_text, "Method should be included"


def test_class_without_decorator_subdivision():
    """
    When a class without decorators is split into multiple chunks,
    each chunk should correctly include the class declaration.
    This tests the baseline behavior without decorators.
    """
    raw = [
        "1: public class MyFoo {",
        "2:   public func bigFunc1(...)",
        "3:   public func bigFunc2(...)",
        "4:   public func bigFunc3(...)",
        "5: }",
    ]

    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=3)

    # Should be split into multiple sections
    assert len(doc) > 1, "Expected the document to be split into multiple sections"

    # Each section that has methods should also have the class declaration
    for i, section in enumerate(doc.sections):
        section_text = str(section)
        if "bigFunc" in section_text:
            assert "public class MyFoo" in section_text, (
                f"Section {i + 1} contains methods but is missing class declaration.\n"
                f"Section content:\n{section_text}"
            )


def test_multiple_decorators_included_when_class_is_subdivided():
    """
    When a class has multiple decorators and is split into chunks,
    ALL decorators should be included in each chunk.

    This is an extension of the single-decorator hypothesis test.
    """
    raw = [
        "1: @Fluent",
        "2: @ServiceClient",
        "3: public class MyFoo {",
        "4:   public func bigFunc1(...)",
        "5:   public func bigFunc2(...)",
        "6: }",
    ]

    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=3)

    assert len(doc) > 1, "Expected the document to be split into multiple sections"

    for i, section in enumerate(doc.sections):
        section_text = str(section)

        if "public class MyFoo" in section_text:
            assert "@Fluent" in section_text, (
                f"Section {i + 1} is missing '@Fluent' decorator.\n" f"Section content:\n{section_text}"
            )
            assert "@ServiceClient" in section_text, (
                f"Section {i + 1} is missing '@ServiceClient' decorator.\n" f"Section content:\n{section_text}"
            )


def test_multiple_classes_with_decorators():
    """
    When multiple classes each have decorators not all fitting in one chunk,
    each class's chunks should include their respective decorators.
    """
    raw = [
        "1: @Fluent",
        "2: public class Foo {",
        "3:   public func fooFunc(...)",
        "4: }",
        "5: @Builder",
        "6: public class Bar {",
        "7:   public func barFunc(...)",
        "8: }",
    ]

    # Large enough to keep each class together
    doc = SectionedDocument(lines=raw, base_indent=0, max_chunk_size=10)

    # Verify decorators are associated with their classes
    for section in doc.sections:
        section_text = str(section)

        if "public class Foo" in section_text:
            assert "@Fluent" in section_text, f"Foo class section should include @Fluent.\nSection:\n{section_text}"

        if "public class Bar" in section_text:
            assert "@Builder" in section_text, f"Bar class section should include @Builder.\nSection:\n{section_text}"
