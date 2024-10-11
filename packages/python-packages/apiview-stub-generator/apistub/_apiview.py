from json import JSONEncoder
import logging
import re
import os
import platform
from typing import Optional

from ._node_index import NodeIndex
from ._token import Token
from ._token_kind import TokenKind
from ._version import VERSION
from ._diagnostic import Diagnostic
from ._metadata_map import MetadataMap

JSON_FIELDS = ["Name", "Version", "VersionString", "Navigation", "Tokens", "Diagnostics", "PackageName", "Language", "PackageVersion", "CrossLanguagePackageId"]

HEADER_TEXT = "# Package is parsed using apiview-stub-generator(version:{0}), Python version: {1}".format(VERSION, platform.python_version())
TYPE_NAME_REGEX = re.compile(r"(~?[a-zA-Z\d._]+)")
TYPE_OR_SEPARATOR = " or "


class ApiView:
    """Entity class that holds API view for all namespaces within a package
    :param str pkg_name: The package name.
    :param str namespace: The package namespace.
    :param MetadataMap metadata_map: A metadata mapping object.
    :param str source_url: An optional source URL to display in the preamble.
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
        self.name = pkg_name
        self.version = 0
        self.version_string = VERSION
        self.language = "Python"
        self.tokens = []
        self.navigation = []
        self.diagnostics = []
        self.indent = 0    
        self.namespace = namespace
        self.node_index = NodeIndex()
        self.package_name = pkg_name
        self.package_version = pkg_version
        self.metadata_map = metadata_map or MetadataMap("")
        self.cross_language_package_id = self.metadata_map.cross_language_package_id
        self.add_token(Token("", TokenKind.SkipDiffRangeStart))
        self.add_literal(HEADER_TEXT)
        self.add_line_marker("GLOBAL")
        if source_url:
            self.set_blank_lines(1)
            self.add_literal("# Source URL: ")
            self.add_link(source_url)
        self.add_token(Token("", TokenKind.SkipDiffRangeEnd))
        self.set_blank_lines(2)

    def add_token(self, token):
        self.tokens.append(token)

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

    def set_blank_lines(self, count):
        """ Ensures a specific number of blank lines.
            Will add or remove newline tokens as needed
            to ensure the exact number of blank lines.
        """
        # count the number of trailing newlines
        newline_count = 0
        for token in self.tokens[::-1]:
            if token.kind == TokenKind.Newline:
                newline_count += 1
            else:
                break
        
        if newline_count < (count + 1):
            # if there are not enough newlines, add some
            for n in range((count + 1) - newline_count):
                self.add_token(Token("", TokenKind.Newline))
        elif newline_count > (count + 1):
            # if there are too many newlines, remove some
            excess = newline_count - (count + 1)
            for _ in range(excess):
                self.tokens.pop()

    def add_punctuation(self, value, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(value, TokenKind.Punctuation))
        if postfix_space:
            self.add_space()

    import re

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
            # Check if one of the types is None
            has_none = False
            if "None" in types:
                has_none = True
                types.remove("None")
            # Make a Union of types if multiple non-None types are present, otherwise use the single type
            if len(types) > 1:
                type_name = "Union[{}]".format(", ".join(types))
            else:
                type_name = types[0]
            # If one of the types is None, wrap the Union type in Optional
            if has_none:
                type_name = "Optional[{}]".format(type_name)

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


    def add_diagnostic(self, *, obj, target_id):
        self.diagnostics.append(Diagnostic(obj=obj, target_id=target_id))


    def add_member(self, name, id):
        token = Token(name, TokenKind.MemberName)
        token.definition_id = id
        self.add_token(token)


    def add_string_literal(self, value):
        self.add_token(Token("\u0022{}\u0022".format(value), TokenKind.StringLiteral))


    def add_literal(self, value):
        self.add_token(Token(value, TokenKind.Literal))


    def add_navigation(self, navigation):
        self.navigation.append(navigation)

class APIViewEncoder(JSONEncoder):
    """Encoder to generate json for APIview object
    """

    def _snake_to_pascal(self, text: str) -> str:
        return text.replace("_", " ").title().replace(" ", "")

    def _pascal_to_snake(self, text: str) -> str:
        results = "_".join([x.lower() for x in re.findall('[A-Z][^A-Z]*', text)])
        return results

    def default(self, obj):
        obj_dict = {}
        if isinstance(obj, (ApiView, Token, Navigation, NavigationTag, Diagnostic)):            
            # Remove fields in APIview that are not required in json
            if isinstance(obj, ApiView):
                for key in JSON_FIELDS:
                    snake_key = self._pascal_to_snake(key)
                    if snake_key in obj.__dict__:
                        obj_dict[key] = obj.__dict__[snake_key]
            elif isinstance(obj, Token):
                obj_dict = {self._snake_to_pascal(k):v for k, v in obj.__dict__.items()}
                # Remove properties from serialization to reduce size if property is not set
                if not obj.definition_id:
                    del obj_dict["DefinitionId"]
                if not obj.navigate_to_id:
                    del obj_dict["NavigateToId"]
                if not obj.cross_language_definition_id:
                    del obj_dict["CrossLanguageDefinitionId"]
            elif isinstance(obj, Diagnostic):
                obj_dict = {self._snake_to_pascal(k):v for k, v in obj.__dict__.items()}
                if not obj.help_link_uri:
                    del obj_dict["HelpLinkUri"]
            else:
                obj_dict = {self._snake_to_pascal(k):v for k, v in obj.__dict__.items()}

            return obj_dict
        elif isinstance(obj, TokenKind) or isinstance(obj, Kind):
            return obj.value  # {"__enum__": obj.value}
        else:
            try:
                JSONEncoder.default(self, obj)
            except:
                logging.error("Failed to serialize using default serialization for {}. Serializing using object dict.".format(obj))
                return obj_dict


class NavigationTag:
    def __init__(self, kind):
        self.type_kind = kind


class Kind:
    type_class = "class"
    type_enum = "enum"
    type_method = "method"
    type_module = "namespace"
    type_package = "assembly"


class Navigation:
    """Navigation model to be added into tokens files. List of Navigation object represents the tree panel in tool"""

    def __init__(self, text, nav_id):
        self.text = text
        self.navigation_id = nav_id
        self.child_items = []
        self.tags = None

    def add_child(self, child):
        self.child_items.append(child)
