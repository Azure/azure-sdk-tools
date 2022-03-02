from json import JSONEncoder
import logging
import re
import importlib
import inspect
import platform

from ._token import Token
from ._token_kind import TokenKind
from ._version import VERSION
from ._diagnostic import Diagnostic
from ._metadata_map import MetadataMap

JSON_FIELDS = ["Name", "Version", "VersionString", "Navigation", "Tokens", "Diagnostics", "PackageName", "Language"]

HEADER_TEXT = "# Package is parsed using api-stub-generator(version:{0}), Python version: {1}".format(VERSION, platform.python_version())
TYPE_NAME_REGEX = re.compile("(~?[a-zA-Z\d._]+)")
TYPE_OR_SEPERATOR = " or "

# Lint warnings
SOURCE_LINK_NOT_AVAILABLE = "Source definition link is not available for [{0}]. Please check and ensure type is fully qualified name in docstring"


class ApiView:
    """Entity class that holds API view for all namespaces within a package
    :param NodeIndex: nodeindex
    :param str: pkg_name
    :param str: ver_string
    """

    def __init__(self, nodeindex, pkg_name="", namespace = "", metadata_map=None):
        self.name = pkg_name
        self.version = 0
        self.version_string = ""
        self.language = "Python"
        self.tokens = []
        self.navigation = []
        self.diagnostics = []
        self.indent = 0    
        self.namespace = namespace
        self.nodeindex = nodeindex
        self.package_name = pkg_name
        self.metadata_map = metadata_map or MetadataMap("")
        self.add_token(Token("", TokenKind.SkipDiffRangeStart))
        self.add_literal(HEADER_TEXT)
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
            raise ValueError("Invalid intendation")
        self.indent -= 1

    def add_whitespace(self):
        if self.indent:
            self.add_token(Token(" " * (self.indent * 4)))

    def add_space(self):
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

    def add_line_marker(self, text):
        token = Token("", TokenKind.LineIdMarker)
        token.definition_id = text
        self.add_token(token)

    def add_text(self, id, text, add_cross_language_id=False):
        token = Token(text, TokenKind.Text)
        token.definition_id = id
        if add_cross_language_id:
            token.cross_language_definition_id = self.metadata_map.cross_language_map.get(id, None)
        self.add_token(token)

    def add_keyword(self, keyword, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(keyword, TokenKind.Keyword))
        if postfix_space:
            self.add_space()


    def add_type(self, type_name, line_id=None):
        # This method replace full qualified internal types to short name and generate tokens
        if not type_name:
            return

        type_name = type_name.replace(":class:", "")
        logging.debug("Processing type {}".format(type_name))
        # Check if multiple types are listed with 'or' seperator
        # Encode multiple types with or separator into Union
        if TYPE_OR_SEPERATOR in type_name:
            types = [t.strip() for t in type_name.split(TYPE_OR_SEPERATOR) if t != TYPE_OR_SEPERATOR]
            # Make a Union of types if multiple types are present
            type_name = "Union[{}]".format(", ".join(types))

        self._add_type_token(type_name, line_id)


    def _add_token_for_type_name(self, type_name, line_id = None):
        logging.debug("Generating tokens for type name {}".format(type_name))
        token = Token(type_name, TokenKind.TypeName)
        type_full_name = type_name[1:] if type_name.startswith("~") else type_name
        token.value = type_full_name.split(".")[-1]
        navigate_to_id = self.nodeindex.get_id(type_full_name)
        if navigate_to_id:
            token.navigate_to_id = navigate_to_id
        elif type_name.startswith("~") and line_id:
            # Check if type name is importable. If type name is incorrect in docstring then it wont be importable
            # If type name is importable then it's a valid type name. Source link wont be available if type is from 
            # different package
            if not is_valid_type_name(type_full_name):
                # Navigation ID is missing for internal type, add diagnostic error
                self.add_diagnostic(SOURCE_LINK_NOT_AVAILABLE.format(token.value), line_id)            
        self.add_token(token)


    def _add_type_token(self, type_name, line_id = None):
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
            self._add_token_for_type_name(parsed_type)
            postfix = type_name[index + len(parsed_type):]
            # process remaining string in type recursively
            self._add_type_token(postfix, line_id)
        else:
            # This is required group ending punctuations
            self.add_punctuation(type_name)        


    def add_diagnostic(self, text, line_id):
        self.diagnostics.append(Diagnostic(line_id, text))


    def add_member(self, name, id):
        token = Token(name, TokenKind.MemberName)
        token.definition_id = id
        self.add_token(token)


    def add_stringliteral(self, value):
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
