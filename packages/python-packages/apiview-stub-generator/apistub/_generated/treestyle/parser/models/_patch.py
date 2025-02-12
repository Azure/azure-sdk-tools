# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------
"""Customize generated code here.

Follow our quickstart for examples: https://aka.ms/azsdk/python/dpcodegen/python/customize
"""
import os
import re
import logging
import platform
from typing import List, Optional
from apistub._version import VERSION
from apistub._node_index import NodeIndex
from apistub._metadata_map import MetadataMap

from ._models import CodeFile, ReviewToken as TokenImpl, ReviewLine as ReviewLineImpl, CodeDiagnostic as Diagnostic
from ._enums import TokenKind

HEADER_TEXT = "# Package is parsed using apiview-stub-generator(version:{0}), Python version: {1}".format(
    VERSION, platform.python_version()
)
TYPE_NAME_REGEX = re.compile(r"(~?[a-zA-Z\d._]+)")
TYPE_OR_SEPARATOR = " or "


class ApiView(CodeFile):
    """ReviewFile represents entire API review object. This will be processed to render review lines.

    :param str pkg_name: The package name.
    :param str namespace: The package namespace.
    :param ReviewLines review_lines: Required.
    :param str source_url: An optional source URL to display in the preamble.

    :ivar package_name: Required.
    :vartype package_name: str
    :ivar package_version: Required.
    :vartype package_version: str
    :ivar parser_version: version of the APIview language parser used to create token file.
     Required.
    :vartype parser_version: str
    :ivar language: Required. Is one of the following types: Literal["C"], Literal["C++"],
     Literal["C#"], Literal["Go"], Literal["Java"], Literal["JavaScript"], Literal["Kotlin"],
     Literal["Python"], Literal["Swagger"], Literal["Swift"], Literal["TypeSpec"]
    :vartype language: str or str or str or str or str or str or str or str or str or str or str
    :ivar language_variant: Language variant is applicable only for java variants. Is one of the
     following types: Literal["None"], Literal["Spring"], Literal["Android"]
    :vartype language_variant: str or str or str
    :ivar cross_language_package_id:
    :vartype cross_language_package_id: str
    :ivar review_lines: Required.
    :vartype review_lines: ReviewLines
    :ivar diagnostics: Add any system generated comments. Each comment is linked to review line ID.
    :vartype diagnostics: list[~treestyle.parser.models.CodeDiagnostic]
    :ivar navigation: Navigation items are used to create a tree view in the navigation panel. Each
     navigation item is linked to a review line ID. This is optional.
     If navigation items are not provided then navigation panel will be automatically generated
     using the review lines. Navigation items should be provided only if you want to customize the
     navigation panel.
    :vartype navigation: list[~treestyle.parser.models.NavigationItem]
    """

    @classmethod
    def get_root_path(cls):
        """Looks for the root of the apiview-stub-generator package."""
        path = os.path.abspath(os.path.join(os.path.dirname(__file__)))
        while os.path.split(path)[1]:
            dirname = os.path.split(path)[1]
            if dirname == "apistub":
                return path
            else:
                path = os.path.split(path)[0]
        return None

    def __init__(self, *, pkg_name="", namespace="", metadata_map=None, source_url=None, pkg_version=""):
        self.metadata_map = metadata_map or MetadataMap("")
        self.review_lines: ReviewLines
        super().__init__(
            package_name=pkg_name,
            package_version=pkg_version,
            parser_version=VERSION,
            language="Python",
            review_lines=ReviewLines(),
            cross_language_package_id=self.metadata_map.cross_language_package_id,
            diagnostics=[],
        )

        self.source_url = source_url
        self.indent = 0
        self.namespace = namespace
        self.node_index = NodeIndex()

    def add_diagnostic(self, *, err, target_id):
        text = f"{err.message} [{err.symbol}]"
        self.diagnostics.append(
            Diagnostic(level=err.level, text=text, help_link_uri=err.help_link, target_id=target_id)
        )

    def add_navigation(self, navigation):
        self.navigation.append(navigation)

    def generate_tokens(self):
        line = self.review_lines.create_review_line()
        line.add_line_marker("GLOBAL", apiview=self)
        line.add_text(HEADER_TEXT, has_suffix_space=False, skip_diff=True)
        self.review_lines.append(line)
        if self.source_url:
            self.review_lines.set_blank_lines()
            line = self.review_lines.create_review_line()
            line.add_literal("# Source URL: ", skip_diff=True)
            line.add_link(self.source_url, skip_diff=True)
            self.review_lines.append(line)
        self.review_lines.set_blank_lines(2)


class ReviewToken(TokenImpl):

    def __init__(self, *args, **kwargs) -> None:  # pylint: disable=useless-super-delegation
        super().__init__(*args, **kwargs)

    def render(self):
        return f"{self.has_prefix_space * ' '}{self.value}{self.has_suffix_space * ' '}"


class ReviewLines(list):
    """A list of ReviewLine objects."""

    def __init__(self, *args):
        super().__init__(*args)

    def create_review_line(
        self,
        *,
        line_id: Optional[str] = None,
        tokens: List[ReviewToken] = [],
        children: Optional[List["ReviewLine"]] = None,
        is_context_end_line: Optional[bool] = False,
        related_to_line: Optional[str] = None,
    ):
        return ReviewLine(
            line_id=line_id,
            tokens=tokens,
            children=children,
            is_context_end_line=is_context_end_line,
            related_to_line=related_to_line,
        )

    def set_blank_lines(self, count=1, last_is_context_end_line=False):
        """Ensures a specific number of blank lines.
        Will add or remove newline tokens as needed
        to ensure the exact number of blank lines.
        """
        # count the number of trailing newlines
        newline_count = 0
        for line in self[::-1]:
            if len(line.tokens) == 0:
                newline_count += 1
            else:
                break

        if newline_count < count:
            # if there are not enough newlines, add some
            for _ in range(count - newline_count):
                line = self.create_review_line()
                # if last line and is context end, specify context end line
                if newline_count + 1 == count and last_is_context_end_line:
                    line.is_context_end_line = True
                self.append(line)
                newline_count += 1
        elif newline_count > (count + 1):
            # if there are too many newlines, remove some
            excess = newline_count - count
            for _ in range(excess):
                self.pop()
            if last_is_context_end_line:
                self[-1].is_context_end_line = True

    def render(self):
        lines = []
        for line in self:
            lines.extend(line.render())
        return lines


class ReviewLine(ReviewLineImpl):

    def __init__(
        self,
        *,
        tokens: List[ReviewToken],
        line_id: Optional[str] = None,
        cross_language_id: Optional[str] = None,
        children: Optional[List["ReviewLine"]] = None,
        is_context_end_line: Optional[bool] = False,
        related_to_line: Optional[str] = None,
    ):
        super().__init__(
            tokens=tokens,
            line_id=line_id,
            cross_language_id=cross_language_id,
            children=children,
            is_context_end_line=is_context_end_line,
            related_to_line=related_to_line,
        )

    def add_children(self, children):
        self.children = children

    def add_token(self, token):
        self.tokens.append(token)

    def add_whitespace(self, count: Optional[int] = None):
        """Inject appropriate whitespace for indentation,
        or inject a specific number of whitespace characters.
        """
        if self.indent:
            self.add_token(ReviewToken(" " * (self.indent * 4)))
        elif count:
            self.add_token(ReviewToken(" " * (count)))

    def add_punctuation(self, value, has_prefix_space=False, has_suffix_space=True):
        self.add_token(
            ReviewToken(
                kind=TokenKind.PUNCTUATION,
                value=value,
                has_prefix_space=has_prefix_space,
                has_suffix_space=has_suffix_space,
            )
        )

    def add_line_marker(self, line_id, add_cross_language_id=False, *, apiview=None):
        if add_cross_language_id:
            # Check if line_id ends with an underscore and a number
            numeric_suffix = re.search(r"_(\d+)$", line_id)
            # If it does, truncate the numeric portion
            line_key = line_id[: numeric_suffix.start()] if numeric_suffix else line_id
            cross_lang_id = apiview.metadata_map.cross_language_map.get(line_key, None)
            self.cross_language_id = cross_lang_id
        self.line_id = line_id

    def add_text(
        self,
        text,
        *,
        has_prefix_space=False,
        has_suffix_space=True,
        skip_diff=False,
        navigation_display_name=None,
        render_classes=None,
    ):
        token = ReviewToken(
            kind=TokenKind.TEXT,
            value=text,
            has_prefix_space=has_prefix_space,
            has_suffix_space=has_suffix_space,
            skip_diff=skip_diff,
        )
        if navigation_display_name:
            token.navigation_display_name = navigation_display_name
        if render_classes:
            token.render_classes = render_classes
        self.add_token(token)

    def add_keyword(self, keyword, has_prefix_space=False, has_suffix_space=True):
        self.add_token(
            ReviewToken(
                kind=TokenKind.KEYWORD,
                value=keyword,
                has_prefix_space=has_prefix_space,
                has_suffix_space=has_suffix_space,
            )
        )

    def add_link(self, url, *, skip_diff=False):
        self.add_token(ReviewToken(kind=TokenKind.EXTERNAL_URL, value=url, skip_diff=skip_diff))

    def add_string_literal(self, value, *, has_prefix_space=False, has_suffix_space=True):
        self.add_token(
            ReviewToken(
                kind=TokenKind.STRING_LITERAL,
                value="\u0022{}\u0022".format(value),
                has_prefix_space=has_prefix_space,
                has_suffix_space=has_suffix_space,
            )
        )

    def add_literal(self, value, *, has_prefix_space=False, has_suffix_space=True, skip_diff=False):
        self.add_token(
            ReviewToken(
                kind=TokenKind.LITERAL,
                value=value,
                has_prefix_space=has_prefix_space,
                has_suffix_space=has_suffix_space,
                skip_diff=skip_diff,
            )
        )

    def _add_token_for_type_name(
        self,
        type_name,
        apiview,
        has_prefix_space=False,
        has_suffix_space=True,
    ):
        logging.debug("Generating tokens for type name {}".format(type_name))
        token = ReviewToken(
            kind=TokenKind.TYPE_NAME,
            value=type_name,
            has_prefix_space=has_prefix_space,
            has_suffix_space=has_suffix_space,
        )
        type_full_name = type_name[1:] if type_name.startswith("~") else type_name
        token.value = type_full_name.split(".")[-1]
        navigate_to_id = apiview.node_index.get_id(type_full_name)
        if navigate_to_id:
            token.navigate_to_id = navigate_to_id
        self.add_token(token)

    def _add_type_token(self, type_name, apiview, has_prefix_space=False, has_suffix_space=True):
        # parse to get individual type name
        logging.debug("Generating tokens for type {}".format(type_name))

        types = re.search(TYPE_NAME_REGEX, type_name)
        if types:
            # Generate token for the prefix before internal type
            # process internal type
            # process post fix of internal type recursively to find replace more internal types
            parsed_type = types.groups()[0]
            index = type_name.find(parsed_type)
            prefix = type_name[:index]
            if prefix:
                self.add_punctuation(prefix, has_suffix_space=False)
            # process parsed type name. internal or built in
            self._add_token_for_type_name(
                parsed_type, apiview=apiview, has_prefix_space=has_prefix_space, has_suffix_space=has_suffix_space
            )
            postfix = type_name[index + len(parsed_type) :]
            # process remaining string in type recursively
            self._add_type_token(
                postfix, apiview=apiview, has_prefix_space=has_prefix_space, has_suffix_space=has_suffix_space
            )
        else:
            # This is required group ending punctuations
            if type_name:  # if type name is empty, don't add punctuation
                self.add_punctuation(type_name, has_suffix_space=False)

    def add_type(self, type_name, apiview, has_prefix_space=False, has_suffix_space=True):
        # TODO: add_type should require an ArgType or similar object so we can link *all* types

        # This method replace full qualified internal types to short name and generate tokens
        if not type_name:
            return

        type_name = type_name.replace(":class:", "")
        logging.debug("Processing type {}".format(type_name))
        # Check if multiple types are listed with 'or' separator
        # Encode multiple types with or separator into Union
        if TYPE_OR_SEPARATOR in type_name:
            types = [t.strip() for t in type_name.split(TYPE_OR_SEPARATOR) if t != TYPE_OR_SEPARATOR]
            # Make a Union of types if multiple types are present
            type_name = "Union[{}]".format(", ".join(types))

        self._add_type_token(
            type_name, apiview=apiview, has_prefix_space=has_prefix_space, has_suffix_space=has_suffix_space
        )

    def render(self):
        lines = ["".join([token.render() for token in self.tokens])]
        if self.children:
            for child in self.children:
                lines.extend(child.render())
        return lines


__all__: List[str] = [
    "ApiView",
    "ReviewLines",
    "ReviewLine",
    "ReviewToken",
]  # Add all objects you want publicly available to users at this package level


def patch_sdk():
    """Do not remove from this file.

    `patch_sdk` is a last resort escape hatch that allows you to do customizations
    you can't accomplish using the techniques described in
    https://aka.ms/azsdk/python/dpcodegen/python/customize
    """
