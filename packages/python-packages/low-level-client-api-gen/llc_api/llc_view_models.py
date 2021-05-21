import re
import logging
import importlib
import inspect
from typing import Any, Dict

import yaml

from ._token import Token
from ._token_kind import TokenKind
from ._diagnostic import Diagnostic

JSON_FIELDS = ["Name", "Version", "VersionString", "Navigation", "Tokens", "Diagnostics", "PackageName"]
PARAM_FIELDS = ["name", "type", "default", "optional", "indent"]
OP_FIELDS = ["operation", "parameters", "indent"]
TOKEN_FIELDS = ["Kind","DefinitionId","NavigateToId","Value"]


TYPE_NAME_REGEX = re.compile("(~?[a-zA-Z\d._]+)")
TYPE_OR_SEPERATOR = " or "

# Lint warnings
SOURCE_LINK_NOT_AVAILABLE = "Source definition link is not available for [{0}]. Please check and ensure type is fully qualified name in docstring"
RETURN_TYPE_MISMATCH = "Return type in type hint is not matching return type in docstring"

class FormattingClass:
    def __init__(self):
        pass
 
    def add_whitespace(self):
        self.indent=1
        if self.indent:
            self.add_token(Token(" " * (self.indent * 4)))

    def add_space(self):
        self.add_token(Token(" ", TokenKind.Whitespace))

    def add_new_line(self,additional_line_count=0):
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
    
    def add_typename(self, id, text):
        token = Token(text, TokenKind.TypeName)
        token.DefinitionId = id
        self.add_token(token)

    def add_keyword(self, keyword, prefix_space=False, postfix_space=False):
        if prefix_space:
            self.add_space()
        self.add_token(Token(keyword, TokenKind.Keyword))
        if postfix_space:
            self.add_space()

    def add_navigation(self, navigation):
        self.Navigation.append(navigation)

class LLCClientView(FormattingClass):
    """Entity class that holds LLC view for all namespaces within a package"""
    def __init__(self, name="",endpoint="endpoint",endpoint_type="string",credential="credential",credential_type="Azure Credential"):
        self.Name = name
        self.Language = "LLC"
        self.Tokens = []
        self.Operations = []
        self.Navigation = []
        self.Diagnostic = []
        self.PackageName = name
        self.indent = 0 
        self.endpoint_type = endpoint_type;
        self.endpoint = endpoint;
        self.credential = credential;  
        self.credential_type = credential_type;  
        self.add_new_line(2)
        self.Client_Name = name

    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any]):
        return cls(
            name = yaml_data["info"]["title"],
            endpoint = yaml_data["globalParameters"][0]["language"]["default"]["name"],
            endpoint_type = yaml_data["globalParameters"][0]["schema"]["type"] ,
        )

    def add_token(self, token):
        self.Tokens.append(token)

    def begin_group(self, group_name=""):
        """Begin a new group in LLC view by shifting to right
        """
        self.indent += 1

    def end_group(self):
        """End current group by moving indent to left
        """
        if not self.indent:
            raise ValueError("Invalid intendation")
        self.indent -= 1

    def add_operation(self,operation):
        self.Operations.append(operation)
    
    def to_token(self): 
    #Create view 

        #Overall Name
        self.add_keyword(self.Name)
        self.add_space()
        self.add_punctuation("{")
        self.add_new_line(1)
        # self.add_new_line(1)

        #Name of client
        self.add_whitespace()
        self.add_typename(None,self.Name)
        self.add_punctuation("(")
        self.add_text(None,self.endpoint_type)
        self.add_space()
        self.add_text(None,self.endpoint)

        self.add_punctuation(",")
        self.add_space()
        self.add_text(None,self.credential_type)
        self.add_space()
        self.add_text(None,self.credential)
 
        self.add_punctuation(")")
        self.add_new_line(1)

        #Set up operations and add to token
        for operation in self.Operations:
            operation.to_token()
            my_ops = operation.get_tokens()
            for o in my_ops:
                self.add_token(o)

        self.add_punctuation("}")

        #Create heiarchy map for view, linkage stuff
        # self.module_dict = {}
       
        # navigation = Navigation(self.Name, None)
        # navigation.set_tag(NavigationTag(Kind.type_package))
        # self.add_navigation(navigation)

        # # Generate tokens
        # modules = self.module_dict.keys()
        # for m in modules:
        #     # Generate and add token to APIView
        #     logging.debug("Generating tokens for module {}".format(m))
        #     self.module_dict[m].to_json()
        #     # Add navigation info for this modules. navigation info is used to build tree panel in API tool
        #     module_nav = self.module_dict[m].get_navigation()
        #     if module_nav:
        #         navigation.add_child(module_nav)
            #Link things to each other
        return self.Tokens  

    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in JSON_FIELDS:
            if key in self.__dict__:
                obj_dict[key] = self.__dict__[key]
        for i in range(0,len(obj_dict["Tokens"])):
            obj_dict["Tokens"][i] = {"Kind": obj_dict["Tokens"][i].Kind.value, "Value" : obj_dict["Tokens"][i].Value}

        return obj_dict


class LLCOperationView(FormattingClass):
    def __init__(self, operation_name, parameters):
        self.operation=operation_name;
        self.parameters=parameters; #parameterview list
        self.Tokens =[]
        self.indent = 0 

    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],num): 
            p = []
            for i in range(0,len(yaml_data["operationGroups"][0]["operations"][num]["signatureParameters"])):
                p.append(ParameterView.from_yaml(yaml_data["operationGroups"][0]["operations"][num],i))
            return cls(
                operation_name = yaml_data["operationGroups"][0]["operations"][num]["language"]["default"]["name"],
                parameters = p,
            )

    def get_tokens(self):
        return self.Tokens

    def add_token(self, token):
        self.Tokens.append(token)
    
    #have a to_token to create the line for parameters
    def to_token(self):

        #Each operation will indent itself by 4
        self.add_whitespace()

        #Operation Name token
        self.add_typename(None,self.operation)

        #Set up operation parameters
        if len(self.parameters)==0:
            self.add_punctuation("(")
            self.add_punctuation(")")
            self.add_new_line()
        for param_num in range(0,len(self.parameters)):
            if self.parameters[param_num]:
                self.parameters[param_num].to_token()
            if param_num==0:
                self.add_punctuation("(")
            
            #If not the first, put a space after the comma
            if param_num!=0: self.add_space()

            #Add in parameter tokens
            if self.parameters[param_num]:
                for t in self.parameters[param_num].get_tokens():
                    self.add_token(t)


            #Add in comma before the next parameter
            try:
                self.parameters[param_num+1]
                self.add_punctuation(",")
            
            #Create a new line for the next operation
            except: 
                self.add_punctuation(")")
                self.add_new_line()
        #Need to consider operation groups next
    
    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict
        
    

class ParameterView(FormattingClass):
    def __init__(self, param_name, param_type, default=None, required = False):
        self.name = param_name;
        self.type = param_type;
        self.default = default;
        self.required = required
        self.Tokens = []
        self.indent = 0 
    
    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],i):
            if yaml_data["signatureParameters"][i].get("required"):
                    re=(yaml_data["signatureParameters"][i]['required'])
            else:
                re = False
            return cls(
                param_type=yaml_data["signatureParameters"][i]["schema"]['type'],
                param_name=yaml_data["signatureParameters"][i]['language']['default']['name'],
                required=re,
                #default=yaml_data["globalParameters"][0]["language"]["default"]["name"]
            )
    
    def add_token(self, token):
        self.Tokens.append(token)
        
    def get_tokens(self):
        return self.Tokens
    
    #have a to_token to create the line for parameters
    def to_token(self):

        #Create parameter type token
        self.add_text(None,self.type)

        #If parameter is optional, token for ? created
        if not self.required:
            self.add_text(None,"?")

        self.add_space()

        #Create parameter name token
        self.add_text(None,self.name)
   

        #Check if parameter has a default value or not
        if self.default is not None:
            self.add_space()
            self.add_text(None,"=")
            self.add_space()
            self.add_text(None,self.default)
        
            
    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in PARAM_FIELDS:
            obj_dict[key] = self.__dict__[key]
        
        return obj_dict


class Kind:
    type_class = "class"
    type_enum = "enum"
    type_method = "method"
    type_module = "namespace"
    type_package = "assembly"

class NavigationTag:
    def __init__(self, kind):
        self.TypeKind = kind

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
    

