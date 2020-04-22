import json
from json import JSONEncoder
import logging
import re
import importlib
import inspect

from ._token import Token
from ._token_kind import TokenKind
from ._version import VERSION
from ._diagnostic import Diagnostic

JSON_FIELDS = ["Name", "Version", "VersionString", "Navigation", "Tokens", "Diagnostics"]

HEADER_TEXT = "# Package is parsed using api-stub-generator(version:{})".format(VERSION)
COMPLEX_DATA_TYPE_PATTERN = re.compile("([a-zA-Z]+)(\[|\()[^\]\)]+(\]|\))")

# Lint warnings
SOURCE_LINK_NOT_AVAILABLE = "Source definition link is not available for [{0}]. Please check and ensure type is fully qualified name in docstring"
RETURN_TYPE_MISMATCH = "Return type in type hint is not matching return type in docstring"


class ApiView:
    """Entity class that holds API view for all namespaces within a package
    :param NodeIndex: nodeindex
    :param str: pkg_name
    :param str: pkg_version
    :param str: ver_string
    """

    def __init__(self, nodeindex, pkg_name="", pkg_version=0, ver_string="", namespace = ""):
        self.Name = pkg_name
        self.Version = pkg_version
        self.VersionString = ver_string
        self.Language = "Python"
        self.Tokens = []
        self.Navigation = []
        self.Diagnostics = []
        self.indent = 0
        self.namespace = namespace
        self.nodeindex = nodeindex
        self.add_literal(HEADER_TEXT)
        self.add_new_line(2)

    def add_token(self, token):
        self.Tokens.append(token)

    def begin_group(self, group_name=""):
        """Begin a new group in API view by shifting to right
        """
        self.indent += 1

    def end_group(self):
        """End current group by moving indent to left
        """
        if not self.indent:
            raise ValueError("Invalid intendation")
        self.indent -= 1

    def add_whitespace(self):
        if self.indent:
            self.add_token(Token(" " * (self.indent * 4)))

    def add_space(self):
        self.add_token(Token(" ", TokenKind.Whitespace))

    def add_new_line(self, additional_line_count=0):
        self.add_token(Token("", TokenKind.Newline))
        for n in range(additional_line_count):
            self.add_space()
            self.add_token(Token("", TokenKind.Newline))

    def add_punctuation(self, value, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(value, TokenKind.Punctuation))
        if postfix_space:
            self.add_space()

    def add_line_marker(self, text):
        token = Token("", TokenKind.LineIdMarker)
        token.set_definition_id(text)
        self.add_token(token)

    def add_text(self, id, text):
        token = Token(text, TokenKind.Text)
        token.DefinitionId = id
        self.add_token(token)

    def add_keyword(self, keyword, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(keyword, TokenKind.Keyword))
        if postfix_space:
            self.add_space()

    def _generate_type_tokens(self, type_name, prefix_type, line_id = None):
        # Generate tokens for multiple data types
        # for e.g. Union[type1, type2,] or dict(type1, type2)
        # For some args, type is given as just "dict"
        # We should not process further if type name is same as prefix(In above e.g. dict)
        logging.debug("Generating tokens for type: {}".format(type_name))
        if prefix_type == type_name:
            self.add_keyword(prefix_type)
            return

        prefix_len = len(prefix_type)
        type_names = type_name[prefix_len + 1 : -1]
        if type_names:
            self.add_keyword(prefix_type)
            self.add_punctuation(type_name[prefix_len])
            # Split types and add individual types
            types = type_names.split(",")
            type_count = len(types)
            for index in range(type_count):
                self.add_type(types[index].strip(), line_id)
                # Add separator between types
                if index < type_count - 1:
                    self.add_punctuation(",", False, True)
            self.add_punctuation(type_name[-1])


    def add_type(self, type_name, line_id=None):
        # This method replace full qualified internal types to short name and generate tokens
        if not type_name:
            return

        # Check if type is multi value types like Union, Dict, list etc
        # Those types needs to be recursively processed
        multi_types = re.search(COMPLEX_DATA_TYPE_PATTERN,type_name)
        if multi_types:
            self._generate_type_tokens(type_name, multi_types.groups()[0], line_id)
        else:
            # Encode mutliple types with or separator into Union
            types = [t for t in type_name.split() if t != 'or']
            if len(types) > 1:
                # Make a Union of types if multiple types are present
                self._generate_type_tokens("Union[{}]".format(", ".join(types)), "Union", line_id)
            else:
                self._add_type_token(types[0], line_id)


    def _add_type_token(self, type_name, line_id = None):
        token = Token(type_name, TokenKind.TypeName)
        type_full_name = type_name[1:] if type_name.startswith("~") else type_name
        token.set_value(type_full_name.split(".")[-1])
        navigate_to_id = self.nodeindex.get_id(type_full_name)
        if navigate_to_id:
            token.NavigateToId = navigate_to_id
        elif type_name.startswith("~") and line_id:
            # Check if type name is importable. If type name is incorrect in docstring then it wont be importable
            # If type name is importable then it's a valid type name. Source link wont be available if type is from 
            # different package
            if not is_valid_type_name(type_full_name):
                # Navigation ID is missing for internal type, add diagnostic error
                self.add_diagnostic(SOURCE_LINK_NOT_AVAILABLE.format(token.Value), line_id)            
        self.add_token(token)


    def add_diagnostic(self, text, line_id):
        self.Diagnostics.append(Diagnostic(line_id, text))


    def add_member(self, name, id):
        token = Token(name, TokenKind.MemberName)
        token.DefinitionId = id
        self.add_token(token)


    def add_stringliteral(self, value):
        self.add_token(Token("\u0022{}\u0022".format(value), TokenKind.StringLiteral))


    def add_literal(self, value):
        self.add_token(Token(value, TokenKind.Literal))


    def add_navigation(self, navigation):
        self.Navigation.append(navigation)


class APIViewEncoder(JSONEncoder):
    """Encoder to generate json for APIview object
    """

    def default(self, obj):
        obj_dict = {}
        if (
            isinstance(obj, ApiView)
            or isinstance(obj, Token)
            or isinstance(obj, Navigation)
            or isinstance(obj, NavigationTag)
            or isinstance(obj, Diagnostic)
        ):            
            # Remove fields in APIview that are not required in json
            if isinstance(obj, ApiView):
                for key in JSON_FIELDS:
                    if key in obj.__dict__:
                        obj_dict[key] = obj.__dict__[key]
            elif isinstance(obj, Token):
                obj_dict = obj.__dict__
                # Remove properties from serialization to reduce size if property is not set
                if not obj.DefinitionId:
                    del obj_dict["DefinitionId"]
                if not obj.NavigateToId:
                    del obj_dict["NavigateToId"]
            elif isinstance(obj, Diagnostic):
                obj_dict = obj.__dict__
                if not obj.HelpLinkUri:
                    del obj_dict["HelpLinkUri"]
            else:
                obj_dict = obj.__dict__

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
        self.TypeKind = kind


class Kind:
    type_class = "class"
    type_enum = "enum"
    type_method = "method"
    type_module = "namespace"
    type_package = "assembly"


class Navigation:
    """Navigation model to be added into tokens files. List of Navigation object represents the tree panel in tool"""

    def __init__(self, text, nav_id):
        self.Text = text
        self.NavigationId = nav_id
        self.ChildItems = []
        self.Tags = None

    def set_tag(self, tag):
        self.Tags = tag

    def add_child(self, child):
        self.ChildItems.append(child)


def is_valid_type_name(type_name):
    try:
        module_end_index = type_name.rfind(".")
        if module_end_index > 0:
            module_name = type_name[:module_end_index]
            class_name = type_name[module_end_index+1:]
            mod = importlib.import_module(module_name)
            return class_name in [x[0] for x in inspect.getmembers(mod)]
    except:
        logging.error("Failed to import {}".format(type_name))    
    return False
