# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from abc import ABC, abstractmethod
from enum import Enum
import logging
from typing import Any, Callable, Dict, List, Optional, Set, Tuple, Union
from ._token import Token
from ._token_kind import TokenKind

JSON_FIELDS = ["Name", "Version", "VersionString", "Navigation", "Tokens", "Diagnostics", "PackageName"]
PARAM_FIELDS = ["name", "type", "default", "optional", "indent"]
OP_FIELDS = ["operation", "parameters", "indent"]

class FormattingClass:
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
    
    def add_comment(self,id,text,nav):
        token = Token(text, TokenKind.Comment)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)

    def add_typename(self, id, text, nav):
        token = Token(text, TokenKind.TypeName)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)
    
    def add_stringliteral(self, id, text,nav):
        token = Token(text, TokenKind.StringLiteral)
        token.DefinitionId = id
        token.NavigateToId = nav
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
    def __init__(self, operation_groups, pkg_name="", endpoint="endpoint",endpoint_type="string",credential="Credential",credential_type="AzureCredential"):
        self.Name = pkg_name
        self.Language = "LLC"
        self.Tokens = []
        self.Operations = []
        self.Operation_Groups= operation_groups
        self.Navigation = []
        self.Diagnostics = []
        self.endpoint_type = endpoint_type
        self.endpoint = endpoint
        self.credential = credential 
        self.credential_type = credential_type
        self.namespace = "Azure."+pkg_name

    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any]):
        operation_groups = []
        #Iterate through Operations in OperationGroups
        for op_groups in range(0,len(yaml_data["operationGroups"])):
            operation_group_view = LLCOperationGroupView.from_yaml(yaml_data, op_groups,"Azure."+yaml_data["info"]["title"])
            operation_group = LLCOperationGroupView(operation_group_view.operation_group,operation_group_view.operations,"Azure."+yaml_data["info"]["title"])
            operation_groups.append(operation_group)

        return cls(
            operation_groups =operation_groups,
            pkg_name = yaml_data["info"]["title"],
            endpoint = yaml_data["globalParameters"][0]["language"]["default"]["name"],
            endpoint_type = yaml_data["globalParameters"][0]["schema"]["type"] ,
        )

    def add_token(self, token):
        self.Tokens.append(token)

    def add_operation_group(self,operation_group):
        self.Operation_Groups.append(operation_group)
    
    def to_token(self): 
    #Create view 
        #Namespace
        self.add_keyword(self.namespace,self.namespace,self.namespace)
        self.add_space()
        self.add_punctuation("{")
        self.add_new_line(1)

        #Name of client
        self.add_whitespace(1)
        self.add_keyword(self.namespace+self.Name,self.Name,self.namespace+self.Name)
        self.add_punctuation("(")
        self.add_stringliteral(None,self.endpoint_type,None)
        self.add_space()
        self.add_text(None,self.endpoint, None)

        self.add_punctuation(",")
        self.add_space()
        self.add_stringliteral(None,self.credential_type,None)
        self.add_space()
        self.add_text(None,self.credential,None)
 
        self.add_punctuation(")")
        self.add_new_line(1)

        #Create Overview
        for operation_group in self.Operation_Groups:
            operation_group.to_token()
            operation_tokens = operation_group.overview_tokens
            for token in operation_tokens:
                    if token:
                        self.add_token(token)
            self.add_new_line(1)

        navigation = self.to_child_tokens()

        self.add_new_line()

        self.add_punctuation("}")

        self.add_navigation(navigation)

        return self.Tokens 

    def to_child_tokens(self):
        #Set Navigation
        navigation = Navigation(self.namespace, None)
        navigation.set_tag(NavigationTag(Kind.type_package))
        self.add_new_line(1)
        for operation_group_view in self.Operation_Groups:
            #Add children
            child_nav = Navigation(operation_group_view.operation_group, self.namespace + operation_group_view.operation_group)
            child_nav.set_tag(NavigationTag(Kind.type_class))
            navigation.add_child(child_nav)
            op_group = operation_group_view.get_tokens()
            for token in op_group:
                self.add_token(token)
            #Set up operations and add to token
            
            for operation_view in operation_group_view.operations:
                #Add operation comments
                child_nav1 = Navigation(operation_view.operation, self.namespace + operation_view.operation)
                child_nav1.set_tag(NavigationTag(Kind.type_method))
                child_nav.add_child(child_nav1)

        return navigation 

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
        obj_dict['Language'] = self.Language
        return obj_dict
    
    
class LLCOperationGroupView(FormattingClass):
    def __init__(self, operation_group_name, operations, namespace):
        self.operation_group=operation_group_name
        self.operations=operations 
        self.Tokens =[]
        self.overview_tokens =[]
        self.namespace = namespace
    
    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],op_group,name): 
            operations = []
            for i in range(0,len(yaml_data["operationGroups"][op_group]["operations"])):
                operations.append(LLCOperationView.from_yaml(yaml_data,op_group,i,name))
            return cls(
                operation_group_name = yaml_data["operationGroups"][op_group]["language"]["default"]["name"],
                operations = operations,
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
            self.overview_tokens.append(Token(" "*4,TokenKind.Whitespace))
            #Operation Name token
            self.add_text(None,"OperationGroup",None)
            self.overview_tokens.append(Token("OperationGroup",TokenKind.Text))
            self.add_space()
            self.overview_tokens.append(Token(" ",TokenKind.Text))
            self.add_keyword(self.namespace+self.operation_group,self.operation_group,self.namespace+self.operation_group)
            self.overview_tokens.append(Token(self.operation_group,TokenKind.Keyword))

            self.add_new_line()
            self.overview_tokens.append(Token("",TokenKind.Newline))
    
            for operation in range(0,len(self.operations)):
                if self.operations[operation]:
                    self.operations[operation].to_token()
                    if operation==0:
                        self.add_whitespace(2)
                        self.overview_tokens.append(Token("  " * (4),TokenKind.Text))
                        self.add_punctuation("{")
                    self.add_new_line()
                    self.overview_tokens.append(Token("",TokenKind.Newline))
                    self.add_whitespace(2)
                    for i in self.operations[operation].overview_tokens:
                        self.overview_tokens.append(i)
                    for t in self.operations[operation].get_tokens():
                        self.add_token(t)
            self.add_whitespace(2)
            self.add_punctuation("}")
            self.add_new_line(1)
            self.overview_tokens.append(Token(" ",TokenKind.Whitespace))
            self.overview_tokens.append(Token("",TokenKind.Newline))

                
            
        else:
            for operation in range(0,len(self.operations)):
                if self.operations[operation]:
                    self.operations[operation].to_token()
                    for i in self.operations[operation].overview_tokens:
                        self.overview_tokens.append(i)
                    for t in self.operations[operation].get_tokens():
                        self.add_token(t)
            
        
    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict


class LLCOperationView(FormattingClass):
    def __init__(self, operation_name, return_type, parameters,namespace, json_request=None, description ="", paging = "",lro=""):
        self.operation=operation_name
        self.return_type = return_type
        self.parameters=parameters #parameterview list
        self.Tokens =[]
        self.overview_tokens =[]
        self.namespace = namespace
        self.description = description
        self.paging = paging
        self.lro = lro
        self.json_request = json_request

    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],op_group_num,op_num,namespace): 
        param = []
        pageable =None
        lro=None
        json_request={}
        for i in range(0,len(yaml_data["operationGroups"][op_group_num]["operations"][op_num]["signatureParameters"])):
            param.append(LLCParameterView.from_yaml(yaml_data["operationGroups"][op_group_num]["operations"][op_num],i,namespace))
        for j in range(0, len(yaml_data['operationGroups'][op_group_num]['operations'][op_num]['requests'])):
            for i in range(0,len(yaml_data['operationGroups'][op_group_num]['operations'][op_num]['requests'][j].get('signatureParameters',[]))):
                param.append(LLCParameterView.from_yaml(yaml_data["operationGroups"][op_group_num]["operations"][op_num]['requests'][j],i,namespace))
                # request_docstring = SchemaRequest1.from_yaml(yaml_data["operationGroups"][op_group_num]["operations"][op_num]['requests'][j],namespace) #
                # request_docstring.to_json_formatting(request_docstring.parameters)
                # json_request.update(request_docstring.json_format)
                # json_request = request_docstring.json_format
        
        return_type = get_type(yaml_data["operationGroups"][op_group_num]["operations"][op_num]['responses'][0].get('schema',[]))

        description = yaml_data["operationGroups"][op_group_num]["operations"][op_num]["language"]["default"].get("summary")
        if description is None:
            description = yaml_data["operationGroups"][op_group_num]["operations"][op_num]["language"]["default"]["description"]

        if yaml_data["operationGroups"][op_group_num]["operations"][op_num].get("extensions"):
            pageable = yaml_data["operationGroups"][op_group_num]["operations"][op_num]["extensions"].get("x-ms-pageable")
            lro = yaml_data["operationGroups"][op_group_num]["operations"][op_num]["extensions"].get("x-ms-long-running-operation")
        if pageable:
            paging_op = True
        else:
            paging_op = False  
        if lro:
            lro_op = True
        else:
            lro_op = False

        
        
        return cls(
            operation_name = yaml_data["operationGroups"][op_group_num]["operations"][op_num]["language"]["default"]["name"],
            parameters = param,
            return_type = return_type,
            namespace = namespace,
            description = description,
            paging = paging_op,
            lro = lro_op,
            json_request = json_request
        )

    def get_tokens(self):
        return self.Tokens

    def add_token(self, token):
        self.Tokens.append(token)
    
    def add_first_line(self):
        if self.paging and self.lro: 
            self.overview_tokens.append(Token("PagingLRO",TokenKind.Text))
            self.overview_tokens.append(Token("[",TokenKind.Text))
            self.add_text(None,"PagingLRO",None)
            self.add_text(None,"[",None)
 
        if self.paging:
            self.overview_tokens.append(Token("Paging",TokenKind.Text))
            self.overview_tokens.append(Token("[",TokenKind.Text))
            self.add_text(None,"Paging",None)
            self.add_text(None,"[",None)

        if self.lro:
            self.overview_tokens.append(Token("LRO",TokenKind.Text))
            self.overview_tokens.append(Token("[",TokenKind.Text))
            self.add_text(None,"LRO",None)
            self.add_text(None,"[",None)
    
        if self.return_type is None: 
            self.overview_tokens.append(Token("void",TokenKind.StringLiteral))
            self.add_stringliteral(None, "void", None)

        self.add_stringliteral(None,self.return_type,None)
        self.overview_tokens.append(Token(self.return_type,TokenKind.StringLiteral))
        if self.paging or self.lro: 
            self.add_text(None,"]",None)
            self.overview_tokens.append(Token("]",TokenKind.Text))
        
        self.add_space()
        self.overview_tokens.append(Token(" ",TokenKind.Text))
        token = Token(self.operation,TokenKind.Keyword)
        token.set_definition_id(self.namespace+self.operation)
        self.overview_tokens.append(token)
        self.add_keyword(self.namespace+self.operation,self.operation, self.namespace+self.operation)
        self.add_space
        
        self.add_new_line()
        self.add_description()
        self.add_whitespace(3)
        self.overview_tokens.append(Token("(",TokenKind.Text))
        self.add_punctuation("(")
    
    def add_description(self):
        self.add_token(Token(kind=TokenKind.StartDocGroup))
        self.add_whitespace(3)
        self.add_comment(None,self.description,None)
        self.add_new_line()
        self.add_token(Token(kind=TokenKind.EndDocGroup))

    def to_token(self):
        #Remove None Param
        self.parameters = [key for key in self.parameters if key.type]

        #Create Overview:

        #Each operation will indent itself by 4
        self.add_whitespace(1)
        self.overview_tokens.append(Token("  "*4,TokenKind.Whitespace))

        #Set up operation parameters
        if len(self.parameters)==0:
            self.add_first_line()
            self.add_new_line()
            self.add_whitespace(3)
            self.overview_tokens.append(Token(")",TokenKind.Text))
            self.add_punctuation(")")
            self.add_new_line(1)

        for param_num in range(0,len(self.parameters)):
            if self.parameters[param_num]:
                self.parameters[param_num].to_token()
            if param_num==0:
                self.add_first_line()
            self.add_new_line()

            #Add in parameter tokens
            if self.parameters[param_num]:
                self.add_whitespace(4)
                for t in self.parameters[param_num].get_tokens():
                    self.add_token(t)
                    self.overview_tokens.append(t)

            #Add in comma before the next parameter
            if param_num+1 in range(0,len(self.parameters)):
                self.parameters[param_num+1]
                self.add_punctuation(",")
                self.overview_tokens.append(Token(", ",TokenKind.Text))
                 
            #Create a new line for the next operation
            else: 
                self.add_new_line()
                self.add_whitespace(3)
                self.overview_tokens.append(Token(")",TokenKind.Text))
                self.add_punctuation(")")
                self.add_new_line(1)


                self.request_builder()

    def request_builder(self):
        if self.json_request:
            self.add_token(Token(kind=TokenKind.StartDocGroup))
            for key in self.json_request.keys():
                self.add_new_line(1)
                self.add_whitespace(4)
                self.add_typename(None,key,None)
                self.add_space()
                if isinstance(self.json_request[key],LLCParameterView):
                    self.json_request[key].to_token()
                    for t in self.json_request[key].get_tokens():
                        self.add_token(t)
                    # self.add_punctuation("{")
                    # self.add_punctuation("}")
                else:
                    for num in range(0,len(self.json_request[key])):
                        
                        if isinstance(self.json_request[key][num],list):
                            self.add_whitespace(4)
                            self.add_punctuation("{")
                            self.add_new_line()
                            for p_list in self.json_request[key][num]:
                                for p in p_list: 
                                    p.to_token()
                                # self.add_whitespace(6)

                                    for t in p.get_tokens():  
                                        self.add_token(t)
                                self.add_new_line()
                            self.add_whitespace(4)
                            self.add_punctuation("}")              
                        else: 
                            self.add_new_line()
                            self.add_whitespace(4)
                            self.add_punctuation("{")
                            self.json_request[key][num].to_token()
                            self.add_new_line()
                            self.add_whitespace(5)
                            for t in self.json_request[key][num].get_tokens():
                                self.add_token(t)
                            self.add_new_line()
                            self.add_whitespace(4)
                            self.add_punctuation("}")
                                
            self.add_new_line(1)
            self.add_token(Token(kind=TokenKind.EndDocGroup))
    
    def to_json(self):
        obj_dict={}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict
        

class LLCParameterView(FormattingClass):
    def __init__(self, param_name, param_type, namespace, json_request = None, default=None, required = False):
        self.name = param_name
        self.type = param_type
        self.default = default
        self.required = required
        self.Tokens = []
        self.overview_tokens = []
        self.json_request = json_request
        self.namespace = namespace
    
    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],i,name):
            required=True
            default = None
            json_request ={}
            if yaml_data.get("signatureParameters"):
                default = yaml_data["signatureParameters"][i]["schema"].get('defaultValue')
                param_name = yaml_data["signatureParameters"][i]['language']['default']['name']
                if yaml_data["signatureParameters"][i]["schema"]['type'] == 'object':
                    param_type = get_type(yaml_data["signatureParameters"][i]["schema"]['properties'][0]['schema'])
                else:
                    param_type = get_type(yaml_data["signatureParameters"][i]["schema"])
                if param_name == 'body':
                    try:
                        param_name = yaml_data["signatureParameters"][i]["schema"]['properties'][0]['serializedName']   
                    except:
                        param_name =param_name
                if yaml_data["signatureParameters"][i].get("required"):
                    required=yaml_data["signatureParameters"][i]['required']
                else:
                    required = False
            else:
                param_type = None
                param_name = None

            return cls(
                param_type=param_type,
                param_name=param_name,
                required=required,
                namespace = name,
                default=default,
                json_request = json_request
            )
    
    def add_token(self, token):
        self.Tokens.append(token)
        
    def get_tokens(self):
        return self.Tokens
    
    def to_token(self):

        if self.type is not None:
            #Create parameter type token
            self.add_stringliteral(self.namespace+self.type,self.type,None)
            self.overview_tokens.append(Token(self.type,TokenKind.StringLiteral))

            #If parameter is optional, token for ? created
            if not self.required:
                self.add_stringliteral(None,"?",None)
                self.overview_tokens.append(Token("?",TokenKind.StringLiteral))
            self.add_space()
            self.overview_tokens.append(Token(" ",TokenKind.Text))
            #Create parameter name token
            self.add_text(None,self.name,None)
            self.overview_tokens.append(Token(self.name,TokenKind.Text))
    

            #Check if parameter has a default value or not
            if self.default is not None:
                self.add_space()
                self.overview_tokens.append(Token(" ",TokenKind.Text))
                self.add_text(None,"=",None)
                self.overview_tokens.append(Token("=",TokenKind.Text))
                self.add_space()
                self.overview_tokens.append(Token(" ",TokenKind.Text))
                self.add_text(None,str(self.default),None)
                self.overview_tokens.append(Token(str(self.default),TokenKind.Text))

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

class SchemaRequest1():
    def __init__(self,media_types,parameters,namespace):
        self.parameters = parameters
        self.media_types = media_types
        self.namespace = namespace
        self.json_format = {}
        self.elements = []

    def to_json_formatting(self, parameters):
        elements1 = []
        for param in parameters:
            for index in param.get('schema'):
                if index=='properties':
                    for properties in param['schema']['properties']:
                        self.elements = (LLCParameterView(properties['serializedName'],get_type(properties['schema']),
                        self.namespace,required = properties.get('required')))
                        self.json_format[properties['serializedName']] = self.elements
                        if properties['schema'].get('elementType'):
                            for prop in properties['schema']['elementType'].get('properties'):
                                self.elements = LLCParameterView( prop['language']['default']['name'],get_type(prop["schema"]),
                                self.namespace,required=prop.get('required'))
                                self.json_format[prop['serializedName']] = self.elements
                                self.elements = self.to_json_formatting([prop])
                if index=='elementType':
                    for properties in param['schema']['elementType'].get('properties'):
                        self.elements = LLCParameterView( properties['language']['default']['name'],get_type(properties["schema"]),
                        self.namespace,required=properties.get('required'))
                        self.json_format[properties['serializedName']] = self.elements
                        self.elements = self.to_json_formatting([properties])
        return self.elements
        #     # this goes through the parameters
        #     if param.get('schema'):
        #         if param['schema'].get('elementType',[]):
        #             for element in param['schema']['elementType'].get('properties',[]):
        #                 elements1 =[]
        #                 for source_num in range(0,len(element['schema'].get('properties',[]))):
        #                     elements1.append(LLCParameterView(element['schema']['properties'][source_num]['language']['default']['name'],get_type(element['schema']['properties'][source_num]["schema"]),self.namespace,required=element.get('required')))
        #                     self.json_format[element['serializedName']] = elements1
        #                     elements.append(elements1)
        #         for r_property in param['schema'].get('properties',[]):
        #             self.json_format[r_property['serializedName']] = [LLCParameterView(r_property['serializedName'], get_type(r_property['schema']),self.namespace,required = r_property.get('required'))]
        #             if r_property['schema'].get('elementType'):
        #                 elements2 = []
        #                 if r_property['schema']['elementType'].get('properties'):
        #                     for element in r_property['schema']['elementType'].get('properties',[]):
        #                         elements2 = []
        #                         for source_num in range(0,len(element['schema'].get('properties',[]))):
        #                             elements2.append(LLCParameterView(element['schema']['properties'][source_num]['language']['default']['name'],get_type(element['schema']['properties'][source_num]["schema"]),self.namespace,required=element.get('required')))
        #                         if (element['schema'].get('elementType',[])):
        #                             for source_num in range(0,len(element['schema']['elementType'].get('properties',[]))):
        #                                 elements2.append(LLCParameterView( element['schema']['elementType']['properties'][source_num]['language']['default']['name'],get_type(element['schema']['elementType']['properties'][source_num]["schema"]),self.namespace,required=element.get('required')))
        #                                 elements.append(elements2)
        #                         self.json_format[element['serializedName']] = elements2
                                
        #             elif r_property['schema'].get('properties'):
        #                 elements3 = []
        #                 for obj_property in r_property['schema']['properties']:
        #                     elements3.append([LLCParameterView(obj_property['serializedName'], get_type(obj_property['schema']),self.namespace,required = obj_property.get('required'))])
        #                     elements.append(elements3)
        #                 self.json_format[r_property['serializedName']].append(elements3)
        # return elements
    
    @classmethod
    def from_yaml(cls,yaml_data: Dict[str,Any],name):
        parameters = []
        parameters = yaml_data.get("signatureParameters", [])
        # json_request={SchemaRequest.from_yaml(yaml) for yaml in yaml_data["requests"]}
        return cls(
            media_types = None,
            parameters = parameters,
            namespace =name,
            # json_format = json_request
        )


def get_type(data):

    #Get type
    try:
        return_type = data['type']
        if return_type =='choice':
                return_type = data['choiceType']['type']
        if return_type == "dictionary":
            value = data['elementType']['type']
            if value =='object'or value =='array' or value =='dictionary': value = get_type(data['elementType'])
            return_type += "[string, "+ value +"]"    
        if return_type == "object":
            return_type = data['language']['default']['name']
        if return_type =='array':
            if data['elementType']['type'] != 'object' and data['elementType']['type'] != 'choice':
                return_type = data['elementType']['type']+ "[]"
            else:
                return_type=  data['elementType']['language']['default']['name']+"[]"
        if return_type == 'number':
            if data['precision'] == 32:
                    return_type = "float32"
            if data['precision'] == 64:
                return_type = "float64"
        if return_type == 'integer':
            if data['precision'] == 32:
                return_type = "int32"
            if data['precision'] == 64:
                return_type = "int64"
        else: return_type = return_type
    except:
        return_type=None
    return return_type

# class ImportType(str, Enum):
#     STDLIB = "stdlib"
#     THIRDPARTY = "thirdparty"
#     AZURECORE = "azurecore"
#     LOCAL = "local"

# class TypingSection(str, Enum):
#     REGULAR = "regular"  # this import is always a typing import
#     CONDITIONAL = "conditional"  # is a typing import when we're dealing with files that py2 will use, else regular
#     TYPING = "typing"  # never a typing import

# class FileImport:
#     def __init__(
#         self,
#         imports: Dict[
#             TypingSection,
#             Dict[ImportType, Dict[str, Set[Optional[Union[str, Tuple[str, str]]]]]]
#         ] = None
#     ) -> None:
#         # Basic implementation
#         # First level dict: TypingSection
#         # Second level dict: ImportType
#         # Third level dict: the package name.
#         # Fourth level set: None if this import is a "import", the name to import if it's a "from"
#         self._imports: Dict[
#             TypingSection,
#             Dict[ImportType, Dict[str, Set[Optional[Union[str, Tuple[str, str]]]]]]
#         ] = imports or dict()

#     def _add_import(
#         self,
#         from_section: str,
#         import_type: ImportType,
#         name_import: Optional[Union[str, Tuple[str, str]]] = None,
#         typing_section: TypingSection = TypingSection.REGULAR
#     ) -> None:
#         self._imports.setdefault(
#                 typing_section, dict()
#             ).setdefault(
#                 import_type, dict()
#             ).setdefault(
#                 from_section, set()
#             ).add(name_import)

#     def add_from_import(
#         self,
#         from_section: str,
#         name_import: str,
#         import_type: ImportType,
#         typing_section: TypingSection = TypingSection.REGULAR,
#         alias: Optional[str] = None,
#     ) -> None:
#         """Add an import to this import block.
#         """
#         self._add_import(
#             from_section, import_type, (name_import, alias) if alias else name_import, typing_section
#         )

#     def add_import(
#         self,
#         name_import: str,
#         import_type: ImportType,
#         typing_section: TypingSection = TypingSection.REGULAR
#     ) -> None:
#         # Implementation detail: a regular import is just a "from" with no from
#         self._add_import(name_import, import_type, None, typing_section)

#     @property
#     def imports(self) -> Dict[
#             TypingSection,
#             Dict[ImportType, Dict[str, Set[Optional[Union[str, Tuple[str, str]]]]]]
#         ]:
#         return self._imports

#     def merge(self, file_import: "FileImport") -> None:
#         """Merge the given file import format."""
#         for typing_section, import_type_dict in file_import.imports.items():
#             for import_type, package_list in import_type_dict.items():
#                 for package_name, module_list in package_list.items():
#                     for module_name in module_list:
#                         self._add_import(package_name, import_type, module_name, typing_extensions)



# class BaseModel:
#     """This is the base class for model that are based on some YAML data.
#     :param yaml_data: the yaml data for this schema
#     :type yaml_data: dict[str, Any]
#     """

#     def __init__(
#         self, yaml_data: Dict[str, Any],
#     ) -> None:
#         self.yaml_data = yaml_data

#     @property
#     def id(self) -> int:
#         return id(self.yaml_data)

#     def __repr__(self):
#         return f"<{self.__class__.__name__}>"
# class SchemaRequest(BaseModel):
#     def __init__(self,yaml_data: Dict[str, Any],parameters: LLCParameterView,) -> None:
#         super().__init__(yaml_data)
#         self.parameters = parameters


#     @property
#     def body_parameter_has_schema(self) -> bool:
#         """Tell if that request has a parameter that defines a body.
#         """
#         return any([p for p in self.parameters if hasattr(p, 'schema') and p.schema])

#     @property
#     def is_stream_request(self) -> bool:
#         """Is the request expected to be streamable, like a download."""
#         if self.yaml_data['protocol']['http'].get('knownMediaType'):
#             return self.yaml_data['protocol']['http']['knownMediaType'] == 'binary' # FIXME: this might be an m4 issue
#         return self.yaml_data["protocol"]["http"].get("binary", False)

#     @classmethod
#     def from_yaml(cls, yaml_data: Dict[str, Any]) -> "SchemaRequest":

#         parameters: Optional[List[LLCParameterView]] = [
#             LLCParameterView.from_yaml(yaml,0,"")
#             for yaml in yaml_data.get("parameters", [])
#         ]

#         return cls(
#             yaml_data=yaml_data,
           
#             parameters=parameters
#         )

#     def __repr__(self) -> str:
#         return f"<{self.__class__.__name__} {self.media_types}>"
# class RawString(object):
#     def __init__(self, string: str) -> None:
#         self.string = string

#     def __repr__(self) -> str:
#         return "r'{}'".format(self.string.replace('\'', '\\\''))
# class BaseSchema(BaseModel, ABC):
#     """This is the base class for all schema models.
#     :param yaml_data: the yaml data for this schema
#     :type yaml_data: dict[str, Any]
#     """

#     def __init__(self, namespace: str, yaml_data: Dict[str, Any]) -> None:
#         super().__init__(yaml_data)
#         self.namespace = namespace
#         self.default_value = yaml_data.get("defaultValue", None)
#         self.xml_metadata = yaml_data.get("serialization", {}).get("xml", {})
#         self.api_versions = set(value_dict["version"] for value_dict in yaml_data.get("apiVersions", []))

#     @classmethod
#     def from_yaml(
#         cls, namespace: str, yaml_data: Dict[str, Any], **kwargs  # pylint: disable=unused-argument
#     ) -> "BaseSchema":
#         return cls(namespace=namespace, yaml_data=yaml_data)

#     @property
#     def has_xml_serialization_ctxt(self) -> bool:
#         return bool(self.xml_metadata)

#     def xml_serialization_ctxt(self) -> Optional[str]:
#         """Return the serialization context in case this schema is used in an operation.
#         """
#         attrs_list = []
#         if self.xml_metadata.get("name"):
#             attrs_list.append(f"'name': '{self.xml_metadata['name']}'")
#         if self.xml_metadata.get("attribute", False):
#             attrs_list.append("'attr': True")
#         if self.xml_metadata.get("prefix", False):
#             attrs_list.append(f"'prefix': '{self.xml_metadata['prefix']}'")
#         if self.xml_metadata.get("namespace", False):
#             attrs_list.append(f"'ns': '{self.xml_metadata['namespace']}'")
#         if self.xml_metadata.get("text"):
#             attrs_list.append(f"'text': True")
#         return ", ".join(attrs_list)

#     def imports(self) -> FileImport:  # pylint: disable=no-self-use
#         return FileImport()

#     def model_file_imports(self) -> FileImport:
#         return self.imports()

#     @property
#     @abstractmethod
#     def serialization_type(self) -> str:
#         """The tag recognized by 'msrest' as a serialization/deserialization.
#         'str', 'int', 'float', 'bool' or
#         https://github.com/Azure/msrest-for-python/blob/b505e3627b547bd8fdc38327e86c70bdb16df061/msrest/serialization.py#L407-L416
#         or the object schema name (e.g. DotSalmon).
#         If list: '[str]'
#         If dict: '{str}'
#         """
#         ...

#     @property
#     @abstractmethod
#     def docstring_text(self) -> str:
#         """The names used in rtype documentation
#         """
#         ...

#     @property
#     @abstractmethod
#     def docstring_type(self) -> str:
#         """The python type used for RST syntax input.
#         Special case for enum, for instance: 'str or ~namespace.EnumName'
#         """
#         ...

#     @property
#     def type_annotation(self) -> str:
#         """The python type used for type annotation
#         Special case for enum, for instance: Union[str, "EnumName"]
#         """
#         ...

#     @property
#     def operation_type_annotation(self) -> str:
#         return self.type_annotation

#     def get_declaration(self, value: Any) -> str:  # pylint: disable=no-self-use
#         """Return the current value from YAML as a Python string that represents the constant.
#         Example, if schema is "bytearray" and value is "foo",
#         should return bytearray("foo", encoding="utf-8")
#         as a string.
#         This is important for constant serialization.
#         By default, return value, since it works sometimes (integer)
#         """
#         return str(value)

#     @property
#     def default_value_declaration(self) -> str:
#         """Return the default value as string using get_declaration.
#         """
#         if self.default_value is None:
#             return "None"
#         return self.get_declaration(self.default_value)

#     @property
#     def validation_map(self) -> Optional[Dict[str, Union[bool, int, str]]]:  # pylint: disable=no-self-use
#         return None

#     @property
#     def serialization_constraints(self) -> Optional[List[str]]:  # pylint: disable=no-self-use
#         return None

#     @abstractmethod
#     def get_json_template_representation(self, **kwargs: Any) -> Any:
#         """Template of what this schema would look like as JSON input
#         """
#         ...

#     @abstractmethod
#     def get_files_template_representation(self, **kwargs: Any) -> Any:
#         """Template of what this schema would look like as files input
#         """
#         ...
# def _add_optional_and_default_value_template_representation(
#     representation: str,
#     *,
#     optional: bool = True,
#     default_value_declaration: Optional[str] = None,
#     description: Optional[str] = None,
#     **kwargs: Any
# ):
#     if optional:
#         representation += " (optional)"
#     if default_value_declaration and default_value_declaration != "None":  # not doing None bc that's assumed
#         representation += f". Default value is {default_value_declaration}"
#     if description:
#         representation += f". {description}"
#     return representation
# class PrimitiveSchema(BaseSchema):
#     _TYPE_MAPPINGS = {
#         "boolean": "bool",
#     }

#     def _to_python_type(self) -> str:
#         return self._TYPE_MAPPINGS.get(self.yaml_data["type"], "str")

#     @property
#     def serialization_type(self) -> str:
#         return self._to_python_type()

#     @property
#     def docstring_type(self) -> str:
#         return self._to_python_type()

#     @property
#     def type_annotation(self) -> str:
#         return self.docstring_type

#     @property
#     def docstring_text(self) -> str:
#         return self.docstring_type

#     def get_json_template_representation(self, **kwargs: Any) -> Any:
#         return _add_optional_and_default_value_template_representation(
#             representation=self.docstring_text,
#             **kwargs
#         )

#     def get_files_template_representation(self, **kwargs: Any) -> Any:
#         """Template of what the files input should look like
#         """
#         return _add_optional_and_default_value_template_representation(
#             representation=self.docstring_text,
#             **kwargs
#         )

# class ListSchema(BaseSchema):
#     def __init__(
#         self,
#         namespace: str,
#         yaml_data: Dict[str, Any],
#         element_type: BaseSchema,
#         *,
#         max_items: Optional[int] = None,
#         min_items: Optional[int] = None,
#         unique_items: Optional[int] = None,
#     ) -> None:
#         super(ListSchema, self).__init__(namespace=namespace, yaml_data=yaml_data)
#         self.element_type = element_type
#         self.max_items = max_items
#         self.min_items = min_items
#         self.unique_items = unique_items

#     @property
#     def serialization_type(self) -> str:
#         return f"[{self.element_type.serialization_type}]"

#     @property
#     def type_annotation(self) -> str:
#         return f"List[{self.element_type.type_annotation}]"

#     @property
#     def operation_type_annotation(self) -> str:
#         return f"List[{self.element_type.operation_type_annotation}]"

#     @property
#     def docstring_type(self) -> str:
#         return f"list[{self.element_type.docstring_type}]"

#     @property
#     def docstring_text(self) -> str:
#         return f"list of {self.element_type.docstring_text}"

#     @property
#     def validation_map(self) -> Optional[Dict[str, Union[bool, int, str]]]:
#         validation_map: Dict[str, Union[bool, int, str]] = {}
#         if self.max_items:
#             validation_map["max_items"] = self.max_items
#             validation_map["min_items"] = self.min_items or 0
#         if self.min_items:
#             validation_map["min_items"] = self.min_items
#         if self.unique_items:
#             validation_map["unique"] = True
#         return validation_map or None

#     @property
#     def has_xml_serialization_ctxt(self) -> bool:
#         return super().has_xml_serialization_ctxt or self.element_type.has_xml_serialization_ctxt

#     def _get_template_representation(
#         self,
#         callable: Callable,
#         **kwargs: Any
#     ) -> Any:
#         try:
#             if self.element_type.name == kwargs.pop("object_schema_name", ""):
#                 return ["..."]
#         except AttributeError:
#             pass
#         return [callable(**kwargs)]

#     def get_json_template_representation(self, **kwargs: Any) -> Any:
#         return self._get_template_representation(
#             callable=self.element_type.get_json_template_representation,
#             **kwargs
#         )

#     def get_files_template_representation(self, **kwargs: Any) -> Any:
#         return self._get_template_representation(
#             callable=self.element_type.get_files_template_representation,
#             **kwargs
#         )

#     def xml_serialization_ctxt(self) -> Optional[str]:
#         attrs_list = []
#         base_xml_map = super().xml_serialization_ctxt()
#         if base_xml_map:
#             attrs_list.append(base_xml_map)

#         # Attribute at the list level
#         if self.xml_metadata.get("wrapped", False):
#             attrs_list.append("'wrapped': True")

#         # Attributes of the items
#         item_xml_metadata = self.element_type.xml_metadata
#         if item_xml_metadata.get("name"):
#             attrs_list.append(f"'itemsName': '{item_xml_metadata['name']}'")
#         if item_xml_metadata.get("prefix", False):
#             attrs_list.append(f"'itemsPrefix': '{item_xml_metadata['prefix']}'")
#         if item_xml_metadata.get("namespace", False):
#             attrs_list.append(f"'itemsNs': '{item_xml_metadata['namespace']}'")

#         return ", ".join(attrs_list)

#     @classmethod
#     def from_yaml(cls, namespace: str, yaml_data: Dict[str, Any], **kwargs) -> "ListSchema":
#         # TODO: for items, if the type is a primitive is it listed in type instead of $ref?
#         element_schema = yaml_data["elementType"]

#         from . import build_schema  # pylint: disable=import-outside-toplevel

#         element_type = build_schema(yaml_data=element_schema, **kwargs)

#         return cls(
#             namespace=namespace,
#             yaml_data=yaml_data,
#             element_type=element_type,
#             max_items=yaml_data.get("maxItems"),
#             min_items=yaml_data.get("minItems"),
#             unique_items=yaml_data.get("uniqueItems"),
#         )

#     def imports(self) -> FileImport:
#         file_import = FileImport()
#         file_import.add_from_import("typing", "List", ImportType.STDLIB, TypingSection.CONDITIONAL)
#         file_import.merge(self.element_type.imports())
#         return file_import

# class ConstantSchema(BaseSchema):
#     """Schema for constants that will be serialized.
#     :param yaml_data: the yaml data for this schema
#     :type yaml_data: dict[str, Any]
#     :param str value: The actual value of this constant.
#     :param schema: The schema for the value of this constant.
#     :type schema: ~autorest.models.PrimitiveSchema
#     """

#     def __init__(
#         self, namespace: str, yaml_data: Dict[str, Any], schema: PrimitiveSchema, value: Optional[str],
#     ) -> None:
#         super(ConstantSchema, self).__init__(namespace=namespace, yaml_data=yaml_data)
#         self.value = value
#         self.schema = schema

#     def get_declaration(self, value: Any):
#         if value != self.value:
#             _LOGGER.warning(
#                 "Passed in value of %s differs from constant value of %s. Choosing constant value",
#                 str(value), str(self.value)
#             )
#         if self.value is None:
#             return "None"
#         return self.schema.get_declaration(self.value)

#     @property
#     def serialization_type(self) -> str:
#         """Returns the serialization value for msrest.
#         :return: The serialization value for msrest
#         :rtype: str
#         """
#         return self.schema.serialization_type

#     @property
#     def docstring_text(self) -> str:
#         return "constant"

#     @property
#     def docstring_type(self) -> str:
#         """The python type used for RST syntax input and type annotation.
#         :param str namespace: Optional. The namespace for the models.
#         """
#         return self.schema.docstring_type

#     @property
#     def type_annotation(self) -> str:
#         return self.schema.type_annotation

#     @classmethod
#     def from_yaml(cls, namespace: str, yaml_data: Dict[str, Any], **kwargs) -> "ConstantSchema":
#         """Constructs a ConstantSchema from yaml data.
#         :param yaml_data: the yaml data from which we will construct this schema
#         :type yaml_data: dict[str, Any]
#         :return: A created ConstantSchema
#         :rtype: ~autorest.models.ConstantSchema
#         """
#         name = yaml_data["language"]["python"]["name"] if yaml_data["language"]["python"].get("name") else ""
#         logging.Logger.debug("Parsing %s constant", name)
#         return cls(
#             namespace=namespace,
#             yaml_data=yaml_data,
#             schema=get_primitive_schema(namespace=namespace, yaml_data=yaml_data["valueType"]),
#             value=yaml_data.get("value", {}).get("value", None),
#         )

#     def get_json_template_representation(self, **kwargs: Any) -> Any:
#         return self.schema.get_json_template_representation(**kwargs)

#     def get_files_template_representation(self, **kwargs: Any) -> Any:
#         return self.schema.get_files_template_representation(**kwargs)

#     def imports(self) -> FileImport:
#         file_import = FileImport()
#         file_import.merge(self.schema.imports())
#         return file_import
        
# class ParameterLocation(Enum):
#     Path = "path"
#     Body = "body"
#     Query = "query"
#     Header = "header"
#     Uri = "uri"
#     Other = "other"

# class ParameterStyle(Enum):
#     simple = "simple"
#     label = "label"
#     matrix = "matrix"
#     form = "form"
#     spaceDelimited = "spaceDelimited"
#     pipeDelimited = "pipeDelimited"
#     deepObject = "deepObject"
#     tabDelimited = "tabDelimited"
#     json = "json"
#     binary = "binary"
#     xml = "xml"
#     multipart = "multipart"
# class Parameter(BaseModel):  # pylint: disable=too-many-instance-attributes
#     def __init__(
#         self,
#         yaml_data: Dict[str, Any],
#         schema: BaseSchema,
#         rest_api_name: str,
#         serialized_name: str,
#         description: str,
#         implementation: str,
#         required: bool,
#         location: ParameterLocation,
#         skip_url_encoding: bool,
#         constraints: List[Any],
#         target_property_name: Optional[Union[int, str]] = None,  # first uses id as placeholder
#         style: Optional[ParameterStyle] = None,
#         explode: Optional[bool] = False,
#         *,
#         flattened: bool = False,
#         grouped_by: Optional["Parameter"] = None,
#         original_parameter: Optional["Parameter"] = None,
#         client_default_value: Optional[Any] = None,
#     ) -> None:
#         super().__init__(yaml_data)
#         self.schema = schema
#         self.rest_api_name = rest_api_name
#         self.serialized_name = serialized_name
#         self.description = description
#         self._implementation = implementation
#         self.required = required
#         self.location = location
#         self.skip_url_encoding = skip_url_encoding
#         self.constraints = constraints
#         self.target_property_name = target_property_name
#         self.style = style
#         self.explode = explode
#         self.flattened = flattened
#         self.grouped_by = grouped_by
#         self.original_parameter = original_parameter
#         self._client_default_value = client_default_value
#         self.is_hidden_kwarg: bool = False
#         self.has_multiple_media_types: bool = False
#         self.multiple_media_types_type_annot: Optional[str] = None
#         self.multiple_media_types_docstring_type: Optional[str] = None
#         self.is_partial_body = yaml_data.get("isPartialBody", False)

#     def __eq__(self, o: "Parameter") -> bool:
#         try:
#             return self.serialized_name == o.serialized_name
#         except AttributeError:
#             return False

#     def __hash__(self) -> int:
#         return hash(self.serialized_name)

#     @staticmethod
#     def serialize_line(function_name: str, parameters_line: str):
#         return f'self._serialize.{function_name}({parameters_line})'

#     def build_serialize_data_call(self, function_name: str) -> str:

#         optional_parameters = []

#         if self.skip_url_encoding:
#             optional_parameters.append("skip_quote=True")

#         if self.style and not self.explode:
#             if self.style in [ParameterStyle.simple, ParameterStyle.form]:
#                 div_char = ","
#             elif self.style in [ParameterStyle.spaceDelimited]:
#                 div_char = " "
#             elif self.style in [ParameterStyle.pipeDelimited]:
#                 div_char = "|"
#             elif self.style in [ParameterStyle.tabDelimited]:
#                 div_char = "\t"
#             else:
#                 raise ValueError(f"Do not support {self.style} yet")
#             optional_parameters.append(f"div='{div_char}'")

#         if self.explode:
#             if not isinstance(self.schema, ListSchema):
#                 raise ValueError("Got a explode boolean on a non-array schema")
#             serialization_schema = self.schema.element_type
#         else:
#             serialization_schema = self.schema

#         serialization_constraints = serialization_schema.serialization_constraints
#         if serialization_constraints:
#             optional_parameters += serialization_constraints

#         origin_name = self.full_serialized_name

#         parameters = [
#             f'"{origin_name.lstrip("_")}"',
#             "q" if self.explode else origin_name,
#             f"'{serialization_schema.serialization_type}'",
#             *optional_parameters
#         ]
#         parameters_line = ', '.join(parameters)

#         serialize_line = self.serialize_line(function_name, parameters_line)

#         if self.explode:
#             return f"[{serialize_line} if q is not None else '' for q in {origin_name}]"
#         return serialize_line

#     @property
#     def constant(self) -> bool:
#         """Returns whether a parameter is a constant or not.
#         Checking to see if it's required, because if not, we don't consider it
#         a constant because it can have a value of None.
#         """
#         if not isinstance(self.schema, ConstantSchema):
#             return False
#         return self.required

#     @property
#     def is_multipart(self) -> bool:
#         return self.yaml_data["language"]["python"].get("multipart", False)

#     @property
#     def constant_declaration(self) -> str:
#         if self.schema:
#             if isinstance(self.schema, ConstantSchema):
#                 return self.schema.get_declaration(self.schema.value)
#             raise ValueError(
#                 "Trying to get constant declaration for a schema that is not ConstantSchema"
#                 )
#         raise ValueError("Trying to get a declaration for a schema that doesn't exist")

#     @property
#     def xml_serialization_ctxt(self) -> str:
#         return self.schema.xml_serialization_ctxt() or ""

#     @property
#     def is_body(self) -> bool:
#         return self.location == ParameterLocation.Body

#     @property
#     def in_method_signature(self) -> bool:
#         return not(
#             # If I only have one value, I can't be set, so no point being in signature
#             self.constant
#             # If i'm not in the method code, no point in being in signature
#             or not self.in_method_code
#             # If I'm grouped, my grouper will be on signature, not me
#             or self.grouped_by
#             # If I'm body and it's flattened, I'm not either
#             or (self.is_body and self.flattened)
#             # If I'm a kwarg, don't include in the signature
#             or self.is_hidden_kwarg
#         )


#     @classmethod
#     def from_yaml(cls, yaml_data: Dict[str, Any]) -> "Parameter":
#         http_protocol = yaml_data["protocol"].get("http", {"in": ParameterLocation.Other})
#         return cls(
#             yaml_data=yaml_data,
#             schema=yaml_data.get("schema", None),  # FIXME replace by operation model
#             # See also https://github.com/Azure/autorest.modelerfour/issues/80
#             rest_api_name=yaml_data["language"]["default"].get(
#                 "serializedName", yaml_data["language"]["default"]["name"]
#             ),
#             serialized_name=yaml_data["language"]["python"]["name"],
#             description=yaml_data["language"]["python"]["description"],
#             implementation=yaml_data["implementation"],
#             required=yaml_data.get("required", False),
#             location=ParameterLocation(http_protocol["in"]),
#             skip_url_encoding=yaml_data.get("extensions", {}).get("x-ms-skip-url-encoding", False),
#             constraints=[],  # FIXME constraints
#             target_property_name=id(yaml_data["targetProperty"]) if yaml_data.get("targetProperty") else None,
#             style=ParameterStyle(http_protocol["style"]) if "style" in http_protocol else None,
#             explode=http_protocol.get("explode", False),
#             grouped_by=yaml_data.get("groupedBy", None),
#             original_parameter=yaml_data.get("originalParameter", None),
#             flattened=yaml_data.get("flattened", False),
#             client_default_value=yaml_data.get("clientDefaultValue"),
#         )

