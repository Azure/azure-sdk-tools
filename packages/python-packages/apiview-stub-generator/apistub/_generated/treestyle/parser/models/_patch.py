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

from ._models import CodeFile, ReviewToken as Token, ReviewLine as ReviewLineImpl, CodeDiagnostic as Diagnostic
from ._enums import TokenKind

HEADER_TEXT = "# Package is parsed using apiview-stub-generator(version:{0}), Python version: {1}".format(VERSION, platform.python_version())
TYPE_NAME_REGEX = re.compile(r"(~?[a-zA-Z\d._]+)")
TYPE_OR_SEPARATOR = " or "

class ApiView(CodeFile):
    """ReviewFile represents entire API review object. This will be processed to render review lines.

    :param str pkg_name: The package name.
    :param str namespace: The package namespace.
    :param ReviewLines review_lines: A metadata mapping object.
    :param str source_url: An optional source URL to display in the preamble.
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
    :vartype review_lines: list[~treestyle.parser.models.ReviewLine]
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
        """ Looks for the root of the apiview-stub-generator package.
        """
        path = os.path.abspath(os.path.join(os.path.dirname(__file__)))
        while os.path.split(path)[1]:
            dirname = os.path.split(path)[1]
            if dirname == "apistub":
                return path
            else:
                path = os.path.split(path)[0]
        return None

    def __init__(self, *, pkg_name="", namespace = "", metadata_map=None, source_url=None, pkg_version =""):
        #self.package_name = pkg_name
        #self.package_version = pkg_version
        #self.language = "Python"
        self.metadata_map = metadata_map or MetadataMap("")
        #self.cross_language_package_id = self.metadata_map.cross_language_package_id
        #self.review_lines = []
        #self.diagnostics = []
        #self.navigation = []
        # TODO: Version: 0 doesn't have a correpsonding value in new parser. 
        #self.version = 0
        super().__init__(
            package_name=pkg_name,
            package_version=pkg_version,
            parser_version=VERSION,
            language="Python",
            review_lines=[],
            cross_language_package_id=self.metadata_map.cross_language_package_id,
            diagnostics=[],
            #navigation=[], # TODO: Add later if needed
        )

        self.source_url = source_url
        self.indent = 0
        self.namespace = namespace
        self.node_index = NodeIndex()
        token = Token(
            kind=TokenKind.TEXT,
            skip_diff=True,
            value=HEADER_TEXT,
            has_suffix_space=False,
        )
        self.add_review_line(line_id="GLOBAL", tokens=[token])
        #if source_url:  # TODO: test source url
        #    self.set_blank_lines(1)
        #    self.add_literal("# Source URL: ")
        #    self.add_link(source_url)
        #self.add_token(Token(kind=TokenKind.TEXT, value="", skip_diff=True))
        self.set_blank_lines(2)

    def set_blank_lines(self, count):
        """ Ensures a specific number of blank lines.
            Will add or remove newline tokens as needed
            to ensure the exact number of blank lines.
        """
        for _ in range(count):
            self.add_review_line()

        # TODO: Find out why counting/removing was needed
        # count the number of trailing newlines
        #newline_count = 0
        #for token in self.tokens[::-1]:
        #    if token.kind == TokenKind.Newline:
        #        newline_count += 1
        #    else:
        #        break
        
        #if newline_count < (count + 1):
        #    # if there are not enough newlines, add some
        #    for n in range((count + 1) - newline_count):
        #        self.add_token(Token("", TokenKind.Newline))
        #elif newline_count > (count + 1):
        #    # if there are too many newlines, remove some
        #    excess = newline_count - (count + 1)
        #    for _ in range(excess):
        #        self.tokens.pop()

    def begin_group(self, group_name=""):
        """Begin a new group in API view by shifting to right
        """
        self.indent += 1

    def end_group(self):
        """End current group by moving indent to left
        """
        if not self.indent:
            raise ValueError("Invalid indentation")
        self.indent -= 1
    
    def add_review_line(
        self,
        *,
        line_id: Optional[str] = None,
        tokens: List[Token] = [],
    ):
        self.review_lines.append(
            ReviewLine(line_id=line_id, tokens=tokens)
        )

    def add_diagnostic(self, *, obj, target_id):
        self.diagnostics.append(Diagnostic(obj=obj, target_id=target_id))

    def add_navigation(self, navigation):
        self.navigation.append(navigation)

class ReviewLine(ReviewLineImpl):

    def __init__(
        self,
        *,
        tokens: List[Token],
        line_id: Optional[str] = None,
        cross_language_id: Optional[str] = None,
        children: Optional[List["ReviewLine"]] = None,
        is_context_end_line: Optional[bool] = False,
        related_to_line: Optional[str] = None,
        parent: Optional["ReviewLine"] = None
    ):
        super().__init__(
            tokens=tokens,
            line_id=line_id,
            cross_language_id=cross_language_id,
            children=children,
            is_context_end_line=is_context_end_line,
            related_to_line=related_to_line
        )
        self.parent = parent

    def add_token(self, token):
        self.tokens.append(token)

    def add_whitespace(self, count: Optional[int] = None):
        """ Inject appropriate whitespace for indentation,
            or inject a specific number of whitespace characters.
        """
        if self.indent:
            self.add_token(Token(" " * (self.indent * 4)))
        elif count:
            self.add_token(Token(" " * (count)))

    def add_space(self):
        """ Used to add a single space. Cannot add multiple spaces.
        """
        if self.tokens[-1].kind != TokenKind.Whitespace:
            self.add_token(Token(" ", TokenKind.Whitespace))

    def add_newline(self):
        """ Used to END a line and wrap to the next.
            Cannot be used to inject blank lines.
        """
        # don't add newline if it already is in place
        if self.tokens[-1].kind != TokenKind.Newline:
            self.add_token(Token("", TokenKind.Newline))

    def add_punctuation(self, value, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(value, TokenKind.Punctuation))
        if postfix_space:
            self.add_space()

    def add_line_marker(self, line_id, add_cross_language_id=False):
        token = Token("", TokenKind.LineIdMarker)
        token.definition_id = line_id
        if add_cross_language_id:
            # Check if line_id ends with an underscore and a number
            numeric_suffix = re.search(r'_(\d+)$', line_id)
            # If it does, truncate the numeric portion
            line_key = line_id[:numeric_suffix.start()] if numeric_suffix else line_id
            cross_lang_id = self.metadata_map.cross_language_map.get(line_key, None)
            token.cross_language_definition_id = cross_lang_id
        self.add_token(token)

    def add_text(self, text, *, definition_id=None):
        token = Token(text, TokenKind.Text)
        self.definition_id = definition_id
        self.add_token(token)

    def add_keyword(self, keyword, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(keyword, TokenKind.Keyword))
        if postfix_space:
            self.add_space()


    def add_type(self, type_name, line_id=None):
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

        cross_language_id = self.metadata_map.cross_language_map.get(line_id, None)
        self._add_type_token(type_name, line_id, cross_language_id)

    def add_link(self, url):
        self.add_token(Token(url, TokenKind.ExternalLinkStart))
        self.add_token(Token(url))
        self.add_token(Token(kind=TokenKind.ExternalLinkEnd))

    def _add_token_for_type_name(self, type_name, line_id = None, cross_language_id = None):
        logging.debug("Generating tokens for type name {}".format(type_name))
        token = Token(type_name, TokenKind.TypeName)
        type_full_name = type_name[1:] if type_name.startswith("~") else type_name
        token.value = type_full_name.split(".")[-1]
        navigate_to_id = self.node_index.get_id(type_full_name)
        if navigate_to_id:
            token.navigate_to_id = navigate_to_id
        if cross_language_id:
            token.cross_language_definition_id = cross_language_id
        self.add_token(token)


    def _add_type_token(self, type_name, line_id, cross_language_id):
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
                self.add_punctuation(prefix)
            # process parsed type name. internal or built in
            self._add_token_for_type_name(parsed_type, cross_language_id)
            postfix = type_name[index + len(parsed_type):]
            # process remaining string in type recursively
            self._add_type_token(postfix, line_id, cross_language_id)
        else:
            # This is required group ending punctuations
            self.add_punctuation(type_name)


    def add_member(self, name, id):
        token = Token(name, TokenKind.MemberName)
        token.definition_id = id
        self.add_token(token)


    def add_string_literal(self, value):
        self.add_token(Token("\u0022{}\u0022".format(value), TokenKind.StringLiteral))


    def add_literal(self, value):
        self.add_token(Token(value, TokenKind.Literal))
    
    def add_child_line(self, line):
        self.children.append(line)

__all__: List[str] = [
    "ApiView"
]  # Add all objects you want publicly available to users at this package level


def patch_sdk():
    """Do not remove from this file.

    `patch_sdk` is a last resort escape hatch that allows you to do customizations
    you can't accomplish using the techniques described in
    https://aka.ms/azsdk/python/dpcodegen/python/customize
    """
