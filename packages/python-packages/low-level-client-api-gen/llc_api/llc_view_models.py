import re
import logging
import importlib
import inspect
from typing import Any, Dict
import os
import textwrap
import ast
import io


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

TOP_LEVEL_WHEEL_FILE = "top_level.txt"
INIT_PY_FILE = "__init__.py"

logging.getLogger().setLevel(logging.ERROR)

class FormattingClass:
    def __init__(self):
        pass
 
    def add_whitespace(self,indent):
        if indent:
            self.add_token(Token(" " * (indent * 4)))

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

    def add_text(self, id, text, nav):
        token = Token(text, TokenKind.Text)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)
    
    def add_typename(self, id, text, nav):
        token = Token(text, TokenKind.TypeName)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)
    
    def add_stringliteral(self, id, text):
        token = Token(text, TokenKind.StringLiteral)
        token.DefinitionId = id
        self.add_token(token)

    def add_literal(self, id, text):
        token = Token(text, TokenKind.Literal)
        token.DefinitionId = id
        self.add_token(token)

    def add_keyword(self, id, keyword, nav):
        token = Token(keyword, TokenKind.Keyword)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)
     

    def add_navigation(self, navigation):
        self.Navigation.append(navigation)

class LLCClientView(FormattingClass):
    """Entity class that holds LLC view for all namespaces within a package"""
    def __init__(self, pkg_name="",endpoint="endpoint",endpoint_type="string",credential="credential",credential_type="Azure Credential"):
        self.Name = pkg_name
        self.module_dict = {}
        self.Language = "LLC"
        self.Tokens = []
        self.Operations = []
        self.Operation_Group=[]
        self.Navigation = []
        self.Diagnostics = []
        self.PackageName = pkg_name
        self.indent = 0 
        self.endpoint_type = endpoint_type;
        self.endpoint = endpoint;
        self.credential = credential;  
        self.credential_type = credential_type;  
        self.add_new_line(2)
        self.namespace = "Azure."+pkg_name

    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any]):
        return cls(
            pkg_name = yaml_data["info"]["title"],
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

    def add_operation_group(self,operation_group):
        self.Operation_Group.append(operation_group)
    
    def to_token(self): 
    #Create view 
        #Overall Name
        self.add_keyword(None,self.namespace,None)
        self.add_space()
        self.add_punctuation("{")
        self.add_new_line(1)
        # self.add_new_line(1)

        #Name of client
        self.add_whitespace(1)
        self.add_keyword(None,self.Name,self.namespace)
        self.add_punctuation("(")
        self.add_stringliteral(None,self.endpoint_type)
        self.add_space()
        self.add_text(None,self.endpoint, None)

        self.add_punctuation(",")
        self.add_space()
        self.add_stringliteral(None,self.credential_type)
        self.add_space()
        self.add_text(None,self.credential,None)
 
        self.add_punctuation(")")
        self.add_new_line(1)

        #Set Navigation
        navigation = Navigation(self.namespace, None)
        navigation.set_tag(NavigationTag(Kind.type_package))

        for operation in self.Operation_Group:
            #Add children
            child_nav = Navigation(operation.operation_group, None)
            child_nav.set_tag(NavigationTag(Kind.type_class))
            navigation.add_child(child_nav)
            navigation1 = Navigation(operation, self.namespace+operation.operation_group)
            navigation1.set_tag(NavigationTag(Kind.type_class))

               #Set up operations and add to token
            for op in operation.operations:
                #Add operation comments
                child_nav1 = Navigation(op.operation, self.namespace+op.operation)
                child_nav1.set_tag(NavigationTag(Kind.type_method))
                child_nav.add_child(child_nav1)

            operation.to_token()
            my_ops = operation.get_tokens()
            for o in my_ops:
                self.add_token(o)
      
     
            # operation.to_token()
            # my_ops = operation.get_tokens()
            # for o in my_ops:
            #     self.add_token(o)
        self.add_new_line()
        self.add_punctuation("}")

        self.add_navigation(navigation)

        return self.Tokens  

    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in JSON_FIELDS:
            if key in self.__dict__:
                obj_dict[key] = self.__dict__[key]
        for i in range(0,len(obj_dict["Tokens"])):
            #Break down token objects into dictionary
            if obj_dict["Tokens"][i]:
                obj_dict["Tokens"][i] = {"Kind": obj_dict["Tokens"][i].Kind.value, "Value" : obj_dict["Tokens"][i].Value, 
                    "NavigateToId": obj_dict["Tokens"][i].NavigateToId, "DefinitionId": obj_dict["Tokens"][i].DefinitionId}

            #Remove Null Values from Tokens
            obj_dict["Tokens"][i] = {key:value for key,value in obj_dict["Tokens"][i].items() if value is not None}
        
        return obj_dict
    
    
class LLCOperationGroupView(FormattingClass):
    def __init__(self, operation_group_name, operations, namespace):
        self.operation_group=operation_group_name;
        self.operations=operations; #parameterview list
        self.Tokens =[]
        self.indent = 0 
        self.namespace = namespace
    
    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],op_group,name): 
            o = []
            for i in range(0,len(yaml_data["operationGroups"][op_group]["operations"])):
                o.append(LLCOperationView.from_yaml(yaml_data,op_group,i,name))
            return cls(
                operation_group_name = yaml_data["operationGroups"][op_group]["language"]["default"]["name"],
                operations = o,
                namespace=name,
            )

    def get_tokens(self):
            return self.Tokens

    def add_token(self, token):
        self.Tokens.append(token)

    #have a to_token to create the line for parameters
    def to_token(self):

        #Each operation will indent itself by 4
        self.add_new_line()
        

        if self.operation_group:
            self.add_whitespace(1)
            #Operation Name token
            self.add_keyword(self.namespace+self.operation_group,self.operation_group,self.namespace+self.operation_group)

            self.add_new_line()
    

            for operation in range(0,len(self.operations)):
                if self.operations[operation]:
                    self.operations[operation].to_token()
                    if operation==0:
                        self.add_whitespace(2)
                        self.add_punctuation("{")
                    self.add_new_line()
                    self.add_whitespace(2)
                    for t in self.operations[operation].get_tokens():
                        self.add_token(t)
            self.add_whitespace(2)
            self.add_punctuation("}")
            self.add_new_line(1)
        else:
            for operation in range(0,len(self.operations)):
                if self.operations[operation]:
                    self.operations[operation].to_token()
                    for t in self.operations[operation].get_tokens():
                        self.add_token(t)

       

    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict


class LLCOperationView(FormattingClass):
    def __init__(self, operation_name, parameters,namespace, description ="", paging = "",lro=""):
        self.operation=operation_name;
        self.parameters=parameters; #parameterview list
        self.Tokens =[]
        self.indent = 0 
        self.namespace = namespace
        self.description = description
        self.paging = paging
        self.lro = lro

    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],op_group,num,name): 
            param = []
            if len(yaml_data["operationGroups"][op_group]["operations"][num]["signatureParameters"])==0:
                param.append(LLCParameterView.from_yaml(yaml_data["operationGroups"][op_group]["operations"][num],0))
            for i in range(0,len(yaml_data["operationGroups"][op_group]["operations"][num]["signatureParameters"])):
                param.append(LLCParameterView.from_yaml(yaml_data["operationGroups"][op_group]["operations"][num],i))

            des = yaml_data["operationGroups"][op_group]["operations"][num]["language"]["default"].get("summary")
            if des is None:
                des = yaml_data["operationGroups"][op_group]["operations"][num]["language"]["default"]["description"]

            pageable = yaml_data["operationGroups"][op_group]["operations"][num]["extensions"].get("x-ms-pageable")
            if pageable:
                paging_op = "True"
            else:
                paging_op = "False"
            
            lro = yaml_data["operationGroups"][op_group]["operations"][num]["extensions"].get("x-ms-long-running-operation")
            if lro:
                lro_op = "True"
            else:
                lro_op = "False"
            
            return cls(
                operation_name = yaml_data["operationGroups"][op_group]["operations"][num]["language"]["default"]["name"],
                parameters = param,
                namespace = name,
                description = des,
                paging = paging_op,
                lro = lro_op
            )

    def get_tokens(self):
        return self.Tokens

    def add_token(self, token):
        self.Tokens.append(token)
    
    #have a to_token to create the line for parameters
    def to_token(self):

        #Each operation will indent itself by 4
        self.add_whitespace(1)

        #Operation Name token
        self.add_keyword(self.namespace+self.operation,self.operation, self.namespace+self.operation)
        self.add_space

        self.parameters = [key for key in self.parameters if key.type is not None]
        #Set up operation parameters
        if len(self.parameters)==0:
            self.add_space()
            self.add_text(None,"[",None)
            self.add_text(None,"paging",None)
            self.add_punctuation("=")
            self.add_space()
            self.add_text(None,self.paging,None)
            self.add_space()
            self.add_text(None,"lro",None)
            self.add_punctuation("=")
            self.add_space()
            self.add_text(None,self.lro,None)
            self.add_space()
            self.add_text(None,"]",None)
            self.add_space()
            self.add_punctuation("(")
            self.add_punctuation(")")
    
            self.add_new_line()
            self.add_whitespace(3)
            self.add_token(Token(kind=TokenKind.StartDocGroup))
            self.add_typename(None,self.description,None)
            self.add_token(Token(kind=TokenKind.EndDocGroup))
            self.add_new_line()
        for param_num in range(0,len(self.parameters)):
            if self.parameters[param_num]:
                self.parameters[param_num].to_token()
            if param_num==0:
                # self.add_new_line()
                # self.add_whitespace(3)
                self.add_space()
                self.add_text(None,"[",None)
                self.add_text(None,"paging",None)
                self.add_punctuation("=")
                self.add_space()
                self.add_text(None,self.paging,None)
                self.add_space()
                self.add_text(None,"lro",None)
                self.add_punctuation("=")
                self.add_space()
                self.add_text(None,self.lro,None)
                self.add_space()
                self.add_text(None,"]",None)
                self.add_space()
                self.add_punctuation("(")
                self.add_new_line()
                self.add_whitespace(3)
                self.add_token(Token(kind=TokenKind.StartDocGroup))
                self.add_typename(None,self.description,None)
                self.add_token(Token(kind=TokenKind.EndDocGroup))
            
            self.add_new_line()
            #Add in parameter tokens
            if self.parameters[param_num]:
                self.add_whitespace(4)
                for t in self.parameters[param_num].get_tokens():
                    self.add_token(t)


            #Add in comma before the next parameter
            if param_num+1 in range(0,len(self.parameters)):
                self.parameters[param_num+1]
                self.add_punctuation(",")
                 
            #Create a new line for the next operation
            else: 
                self.add_new_line()
                self.add_whitespace(3)
                self.add_punctuation(")")
                self.add_new_line(1)
        #Need to consider operation groups next
    
    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict
        

class LLCParameterView(FormattingClass):
    def __init__(self, param_name, param_type, default=None, required = False):
        self.name = param_name;
        self.type = param_type;
        self.default = default;
        self.required = required
        self.Tokens = []
        self.indent = 0 
    
    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],i):
            req=True
            if len(yaml_data["signatureParameters"])!=0:
                p_type = yaml_data["signatureParameters"][i]["schema"]['type']
                p_name = yaml_data["signatureParameters"][i]['language']['default']['name']
                if yaml_data["signatureParameters"][i].get("required"):
                    req=(yaml_data["signatureParameters"][i]['required'])
                else:
                    req = False
            else:
                p_type = None
                p_name = None
            
            if p_type is None:
                    if yaml_data['requests'][0].get('signatureParameters'):
                        if yaml_data['requests'][0]['signatureParameters'][0].get('originalParameter'):
                        # p_type = yaml_data['requests'][0]['signatureParameters'][0]['schema']['elementType']['properties'][0]['schema']['type']
                        # p_name = yaml_data['requests'][0]['signatureParameters'][0]['schema']['language']['default']['name']
                        # req = "True" if yaml_data['requests'][0]['signatureParameters'][0]['schema']['language']['default'].get('required') else "False"
                            p_type = yaml_data['requests'][0]['signatureParameters'][0]['originalParameter']['schema']['properties'][0]['schema']['elementType']['language']['default']['name']
                            p_name = yaml_data['requests'][0]['signatureParameters'][0]['originalParameter']['schema']['properties'][0]['serializedName']

            my_name = p_name
            my_type = p_type

            return cls(
                param_type=my_type,
                param_name=my_name,
                required=req,
                # default=yaml_data["globalParameters"][0]["language"]["default"]["name"]
            )
    
    def add_token(self, token):
        self.Tokens.append(token)
        
    def get_tokens(self):
        return self.Tokens
    
    #have a to_token to create the line for parameters
    def to_token(self):

        if self.type is not None:
            #Create parameter type token
            self.add_stringliteral(None,self.type)

            #If parameter is optional, token for ? created
            if not self.required:
                self.add_stringliteral(None,"?")

            self.add_space()

            #Create parameter name token
            self.add_text(None,self.name,None)
    

            #Check if parameter has a default value or not
            if self.default is not None:
                self.add_space()
                self.add_text(None,"=",None)
                self.add_space()
                self.add_text(None,self.default,None)
        
            
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

