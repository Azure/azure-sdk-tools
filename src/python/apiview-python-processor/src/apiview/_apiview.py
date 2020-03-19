import json
from json import JSONEncoder

from _token import Token
from _token_kind import TokenKind

class ApiView:
    """Entity class that holds API view for all namespaces within a package
        :param str: package_name
        :param str: pkg_version
        :param str: versionstring
    """
    def __init__(self, pkg_name ="", pkg_version = 0, ver_string = ""):
        self.Name = pkg_name
        self.Version = pkg_version
        self.VersionString = ver_string
        self.Language = "Python"
        self.Tokens = []
        self.indent = 0


    def add_token(self, token):
        self.Tokens.append(token)


    def begin_group(self, group_name = ""):
        """Begin a new group in API view by shifting to right
        """
        self.indent += 1
        

    def end_group(self):
        if not self.indent:
            raise ValueError("Invalid intendation")
        self.indent -= 1

    
    def add_whitespace(self):
        if self.indent:
            self.add_token(Token(" " *(self.indent *4)))


    def add_space(self):
        self.add_token(Token(" ", TokenKind.Whitespace))


    def add_new_line(self):
        self.add_token(Token("", TokenKind.Newline))


    def add_punctuation(self, value):
        self.add_token(Token(value, TokenKind.Punctuation))
    

    def add_line_marker(self, text):
        token = Token("", TokenKind.LineIdMarker)
        token.set_definition_id(text)
        self.add_token(token)


    def add_text(self, id, text):
        token = Token(text, TokenKind.Text)
        token.DefinitionId = id
        self.add_token(token)

    
    def add_keyword(self, keyword):
        self.add_token(Token(keyword, TokenKind.Keyword))


    def add_type(self, type_name, navigate_to = None):
        token = Token(type_name, TokenKind.TypeName)
        token.NavigationToId = navigate_to
        self.add_token(token)


    def add_member(self, name, id):
        token = Token(name, TokenKind.MemberName)
        token.DefinitionId = id
        self.add_token(token)


    def add_stringliteral(self, value):
        self.add_token(Token("\u0022{}\u0022".format(value), TokenKind.StringLiteral))


    def add_literal(self, value):
        self.add_token(Token(value, TokenKind.Literal))


class APIViewEncoder(JSONEncoder):
    def default(self, obj):
        if isinstance(obj, ApiView) or isinstance(obj, Token):
            return obj.__dict__
        elif isinstance(obj, TokenKind):
            return obj.value #{"__enum__": obj.value}
        else:
            JSONEncoder.default(self, obj)
