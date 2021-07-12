# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from typing import Any, Dict
from ._token import Token
from ._token_kind import TokenKind
import re

JSON_FIELDS = [
    "Name",
    "Version",
    "VersionString",
    "Navigation",
    "Tokens",
    "Diagnostics",
    "PackageName",
]
PARAM_FIELDS = ["name", "type", "default", "optional", "indent"]
OP_FIELDS = ["operation", "parameters", "indent"]
R_TYPE = [
    "dictionary",
    "string",
    "bool",
    "int32",
    "int64",
    "float32",
    "float64",
    "binary",
]


class FormattingClass:
    def add_whitespace(self, indent):
        if indent:
            self.add_token(Token(" " * (indent * 4)))

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

    def add_text(self, id, text, nav):
        token = Token(text, TokenKind.Text)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)

    def add_comment(self, id, text, nav):
        token = Token(text, TokenKind.Comment)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)

    def add_typename(self, id, text, nav):
        token = Token(text, TokenKind.TypeName)
        token.DefinitionId = id
        token.NavigateToId = nav
        self.add_token(token)

    def add_stringliteral(self, id, text, nav):
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


class ProtocolClientView(FormattingClass):
    """Entity class that holds LLC view for all namespaces within a package"""

    def __init__(
        self,
        operation_groups,
        pkg_name="",
        endpoint="endpoint",
        endpoint_type="string",
        credential="Credential",
        credential_type="AzureCredential",
    ):
        self.Name = pkg_name
        self.Language = "Protocol"
        self.Tokens = []
        self.Operations = []
        self.Operation_Groups = operation_groups
        self.Navigation = []
        self.Diagnostics = []
        self.endpoint_type = endpoint_type
        self.endpoint = endpoint
        self.credential = credential
        self.credential_type = credential_type
        self.namespace = "Azure." + pkg_name

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any]):
        operation_groups = []
        # Iterate through Operations in OperationGroups
        for op_groups in range(0, len(yaml_data["operationGroups"])):
            operation_group = ProtocolOperationGroupView.from_yaml(
                yaml_data, op_groups, "Azure." + yaml_data["info"]["title"]
            )
            if operation_group.operation_group == "":
                operation_group.operation_group = "<default>"
                operation_groups.insert(0, operation_group)
            else:
                operation_groups.append(operation_group)

        return cls(
            operation_groups=operation_groups,
            pkg_name=yaml_data["info"]["title"],
            endpoint=yaml_data["globalParameters"][0]["language"]["default"]["name"],
            endpoint_type=yaml_data["globalParameters"][0]["schema"]["type"],
        )

    def add_token(self, token):
        self.Tokens.append(token)

    def add_operation_group(self, operation_group):
        self.Operation_Groups.append(operation_group)

    def to_token(self):
        # Create view
        # Namespace
        self.add_keyword(self.namespace, self.namespace, self.namespace)
        self.add_space()
        self.add_punctuation("{")
        self.add_new_line(1)

        # Name of client
        self.add_whitespace(1)
        self.add_keyword(
            self.namespace + self.Name, self.Name, self.namespace + self.Name
        )
        self.add_punctuation("(")
        self.add_stringliteral(None, self.endpoint_type, None)
        self.add_space()
        self.add_text(None, self.endpoint, None)

        self.add_punctuation(",")
        self.add_space()
        self.add_stringliteral(None, self.credential_type, None)
        self.add_space()
        self.add_text(None, self.credential, None)

        self.add_punctuation(")")
        self.add_new_line(1)

        # Create Overview
        navigation = Navigation(None, None)
        navigation.set_tag(NavigationTag(Kind.type_package))
        overview = Navigation("Overview", "overview")
        overview.set_tag(NavigationTag(Kind.type_package))

        self.add_typename(
            "overview",
            "Overview ######################################################################",
            "overview",
        )
        self.add_new_line()
        for operation_group in self.Operation_Groups:
            child_nav3 = Navigation(
                operation_group.operation_group,
                self.namespace + operation_group.operation_group + "overview",
            )
            child_nav3.set_tag(NavigationTag(Kind.type_class))
            overview.add_child(child_nav3)
            operation_group.to_token()
            operation_tokens = operation_group.overview_tokens
            for token in operation_tokens:
                if token:
                    self.add_token(token)
            for operation_view in operation_group.operations:
                child_nav2 = Navigation(
                    operation_view.operation,
                    self.namespace + operation_group.operation_group + operation_view.operation + "overview",
                )
                child_nav2.set_tag(NavigationTag(Kind.type_method))
                child_nav3.add_child(child_nav2)
            self.add_new_line(1)

        self.add_typename(
            "details",
            "Details ######################################################################",
            "details",
        )
        self.add_new_line()
        details = self.to_child_tokens()

        self.add_new_line()

        self.add_punctuation("}")

        navigation.add_child(overview)
        navigation.add_child(details)
        self.add_navigation(navigation)

        return self.Tokens

    def to_child_tokens(self):
        # Set Navigation
        details = Navigation("Details", "details")
        details.set_tag(NavigationTag(Kind.type_package))
        self.add_new_line()
        for operation_group_view in self.Operation_Groups:
            # Add children
            child_nav1 = Navigation(
                operation_group_view.operation_group,
                self.namespace + operation_group_view.operation_group,
            )
            child_nav1.set_tag(NavigationTag(Kind.type_class))
            details.add_child(child_nav1)
            op_group = operation_group_view.get_tokens()
            for token in op_group:
                self.add_token(token)
            # Set up operations and add to token

            for operation_view in operation_group_view.operations:
                # Add operation comments
                child_nav = Navigation(
                    operation_view.operation, self.namespace + operation_group_view.operation_group + operation_view.operation
                )
                child_nav.set_tag(NavigationTag(Kind.type_method))
                child_nav1.add_child(child_nav)

        return details

    def to_json(self):
        obj_dict = {}
        self.to_token()
        for key in JSON_FIELDS:
            if key in self.__dict__:
                obj_dict[key] = self.__dict__[key]
        for i in range(0, len(obj_dict["Tokens"])):
            # Break down token objects into dictionary
            if obj_dict["Tokens"][i]:
                obj_dict["Tokens"][i] = {
                    "Kind": obj_dict["Tokens"][i].Kind.value,
                    "Value": obj_dict["Tokens"][i].Value,
                    "NavigateToId": obj_dict["Tokens"][i].NavigateToId,
                    "DefinitionId": obj_dict["Tokens"][i].DefinitionId,
                }

            # Remove Null Values from Tokens
            obj_dict["Tokens"][i] = {
                key: value
                for key, value in obj_dict["Tokens"][i].items()
                if value is not None
            }
        obj_dict["Language"] = self.Language
        return obj_dict


class ProtocolOperationGroupView(FormattingClass):
    def __init__(self, operation_group_name, operations, namespace):
        self.operation_group = operation_group_name
        self.operations = operations
        self.Tokens = []
        self.overview_tokens = []
        self.namespace = namespace

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any], op_group, name):
        operations = []
        for i in range(0, len(yaml_data["operationGroups"][op_group]["operations"])):
            operations.append(
                ProtocolOperationView.from_yaml(yaml_data, op_group, i, name)
            )
        return cls(
            operation_group_name=yaml_data["operationGroups"][op_group]["language"][
                "default"
            ]["name"],
            operations=operations,
            namespace=name,
        )

    def get_tokens(self):
        return self.Tokens

    def add_token(self, token):
        self.Tokens.append(token)

    # have a to_token to create the line for parameters
    def to_token(self):

        # Each operation will indent itself by 4
        self.add_new_line()

        if self.operation_group:
            self.add_whitespace(1)
            self.overview_tokens.append(Token(" " * 4, TokenKind.Whitespace))
            # Operation Name token
            self.add_text(None, "OperationGroup", None)
            self.overview_tokens.append(Token("OperationGroup", TokenKind.Text))
            self.add_space()
            self.overview_tokens.append(Token(" ", TokenKind.Text))
            self.add_keyword(
                self.namespace + self.operation_group,
                self.operation_group,
                self.namespace + self.operation_group,
            )
            token = Token(self.operation_group, TokenKind.Keyword)
            token.set_navigation_id(self.namespace + self.operation_group + "overview")
            token.set_definition_id(self.namespace + self.operation_group + "overview")
            self.overview_tokens.append(token)

            self.add_new_line()
            self.overview_tokens.append(Token("", TokenKind.Newline))

            for operation in range(0, len(self.operations)):
                if self.operations[operation]:
                    self.operations[operation].to_token()
                    if operation == 0:
                        self.add_whitespace(2)
                        self.overview_tokens.append(Token("  " * (4), TokenKind.Text))
                        self.add_punctuation("{")
                    self.add_new_line()
                    self.overview_tokens.append(Token("", TokenKind.Newline))
                    self.add_whitespace(2)
                    for i in self.operations[operation].overview_tokens:
                        self.overview_tokens.append(i)
                    for t in self.operations[operation].get_tokens():
                        self.add_token(t)
            self.add_whitespace(2)
            self.add_punctuation("}")
            self.add_new_line(1)
            self.overview_tokens.append(Token(" ", TokenKind.Whitespace))
            self.overview_tokens.append(Token("", TokenKind.Newline))

        else:
            for operation in range(0, len(self.operations)):
                if self.operations[operation]:
                    self.operations[operation].to_token()
                    for i in self.operations[operation].overview_tokens:
                        self.overview_tokens.append(i)
                    for t in self.operations[operation].get_tokens():
                        self.add_token(t)

    def to_json(self):
        obj_dict = {}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict


class ProtocolOperationView(FormattingClass):
    def __init__(
        self,
        operation_group,
        operation_name,
        return_type,
        parameters,
        namespace,
        description="",
        paging=False,
        lro=False,
    ):
        self.operation_group = operation_group
        self.operation = operation_name
        self.return_type = return_type
        self.parameters = parameters  # parameterview list
        self.Tokens = []
        self.overview_tokens = []
        self.namespace = namespace
        self.description = description
        self.paging = paging
        self.lro = lro

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any], op_group_num, op_num, namespace):
        param = []
        pageable = None
        lro = None
        
        if yaml_data["operationGroups"][op_group_num]["operations"][op_num].get(
            "extensions"
        ):
            pageable = yaml_data["operationGroups"][op_group_num]["operations"][op_num][
                "extensions"
            ].get("x-ms-pageable")
            lro = yaml_data["operationGroups"][op_group_num]["operations"][op_num][
                "extensions"
            ].get("x-ms-long-running-operation")
        
        paging_op = True if pageable else False
        lro_op = True if lro else False
      
        return_type = get_type(
            yaml_data["operationGroups"][op_group_num]["operations"][op_num][
                "responses"
            ][0].get("schema", []),
            paging_op,
        )

        for i in range(
            0,
            len(
                yaml_data["operationGroups"][op_group_num]["operations"][op_num][
                    "signatureParameters"
                ]
            ),
        ):
            param.append(
                ProtocolParameterView.from_yaml(
                    yaml_data["operationGroups"][op_group_num]["operations"][op_num],
                    i,
                    namespace,
                )
            )
        for j in range(
            0,
            len(
                yaml_data["operationGroups"][op_group_num]["operations"][op_num][
                    "requests"
                ]
            ),
        ):
            for i in range(
                0,
                len(
                    yaml_data["operationGroups"][op_group_num]["operations"][op_num][
                        "requests"
                    ][j].get("signatureParameters", [])
                ),
            ):
                param.append(
                    ProtocolParameterView.from_yaml(
                        yaml_data["operationGroups"][op_group_num]["operations"][
                            op_num
                        ]["requests"][j],
                        i,
                        namespace,
                    )
                )

        description = yaml_data["operationGroups"][op_group_num]["operations"][op_num][
            "language"
        ]["default"].get("summary")
        if description is None:
            description = yaml_data["operationGroups"][op_group_num]["operations"][
                op_num
            ]["language"]["default"]["description"]

        return cls(
            operation_group=yaml_data["operationGroups"][op_group_num]["language"]["default"]["name"],
            operation_name=yaml_data["operationGroups"][op_group_num]["operations"][
                op_num
            ]["language"]["default"]["name"],
            parameters=param,
            return_type=return_type,
            namespace=namespace,
            description=description,
            paging=paging_op,
            lro=lro_op,
        )

    def get_tokens(self):
        return self.Tokens

    def add_token(self, token):
        self.Tokens.append(token)

    def add_first_line(self):
        if self.paging and self.lro:
            self.overview_tokens.append(Token("PagingLro", TokenKind.Text))
            self.overview_tokens.append(Token("[", TokenKind.Text))
            self.add_text(None, "PagingLro", None)
            self.add_text(None, "[", None)

        if self.paging:
            self.overview_tokens.append(Token("Paging", TokenKind.Text))
            self.overview_tokens.append(Token("[", TokenKind.Text))
            self.add_text(None, "Paging", None)
            self.add_text(None, "[", None)

        if self.lro:
            self.overview_tokens.append(Token("lro", TokenKind.Text))
            self.overview_tokens.append(Token("[", TokenKind.Text))
            self.add_text(None, "lro", None)
            self.add_text(None, "[", None)

        if self.return_type is None:
            self.overview_tokens.append(Token("void", TokenKind.Text))
            self.add_text(None, "void", None)

        elif any(i in self.return_type for i in R_TYPE):
            self.add_text(None, self.return_type, None)
            self.overview_tokens.append(Token(self.return_type, TokenKind.Text))
        else:
            self.add_stringliteral(None, self.return_type, None)
            self.overview_tokens.append(
                Token(self.return_type, TokenKind.StringLiteral)
            )
        if self.paging or self.lro:
            self.add_text(None, "]", None)
            self.overview_tokens.append(Token("]", TokenKind.Text))

        self.add_space()
        self.overview_tokens.append(Token(" ", TokenKind.Text))
        token = Token(self.operation, TokenKind.Keyword)
        token.set_definition_id(self.namespace + self.operation_group + self.operation + "overview")
        token.set_navigation_id(self.namespace + self.operation_group + self.operation + "overview")
        self.overview_tokens.append(token)
        self.add_keyword(
            self.namespace + self.operation_group + self.operation,
            self.operation,
            self.namespace + self.operation_group + self.operation,
        )
        self.add_space()

        self.add_new_line()
        self.add_description()
        self.add_whitespace(3)
        self.overview_tokens.append(Token("(", TokenKind.Text))
        self.add_punctuation("(")

    def add_description(self):
        self.add_token(Token(kind=TokenKind.StartDocGroup))
        self.add_whitespace(3)
        self.add_typename(None, self.description, None)
        self.add_new_line()
        self.add_token(Token(kind=TokenKind.EndDocGroup))

    def to_token(self):
        # Remove None Param
        self.parameters = [key for key in self.parameters if key.type]

        # Create Overview:

        # Each operation will indent itself by 4
        self.add_whitespace(1)
        self.overview_tokens.append(Token("  " * 4, TokenKind.Whitespace))

        # Set up operation parameters
        if len(self.parameters) == 0:
            self.add_first_line()
            self.add_new_line()
            self.add_whitespace(3)
            self.overview_tokens.append(Token(")", TokenKind.Text))
            self.add_punctuation(")")
            self.add_new_line(1)

        for param_num in range(0, len(self.parameters)):
            if self.parameters[param_num]:
                self.parameters[param_num].to_token()
            if param_num == 0:
                self.add_first_line()
            self.add_new_line()

            # Add in parameter tokens
            if self.parameters[param_num]:
                self.add_whitespace(4)
                for p in self.parameters[param_num].get_tokens():
                    self.add_token(p)
                for o in self.parameters[param_num].overview_tokens:
                    self.overview_tokens.append(o)

            # Add in comma before the next parameter
            if param_num + 1 in range(0, len(self.parameters)):
                self.add_punctuation(",")
                self.overview_tokens.append(Token(", ", TokenKind.Text))

            # Create a new line for the next operation
            else:
                self.add_new_line()
                self.add_whitespace(3)
                self.overview_tokens.append(Token(")", TokenKind.Text))
                self.add_punctuation(")")
                self.add_new_line(1)

    def to_json(self):
        obj_dict = {}
        self.to_token()
        for key in OP_FIELDS:
            obj_dict[key] = self.__dict__[key]
        return obj_dict


class ProtocolParameterView(FormattingClass):
    def __init__(
        self,
        operation,
        param_name,
        param_type,
        namespace,
        json_request=None,
        default=None,
        required=False,
    ):
        self.operation = operation
        self.name = param_name
        self.type = param_type
        self.default = default
        self.required = required
        self.Tokens = []
        self.overview_tokens = []
        self.json_request = json_request
        self.namespace = namespace

    @classmethod
    def from_yaml(cls, yaml_data: Dict[str, Any], i, name):
        required = True
        default = None
        json_request = {}
        if yaml_data.get("signatureParameters"):
            default = yaml_data["signatureParameters"][i]["schema"].get("defaultValue")
            param_name = yaml_data["signatureParameters"][i]["language"]["default"][
                "name"
            ]
            if yaml_data["signatureParameters"][i]["schema"]["type"] == "object":
                param_type = get_type(
                    yaml_data["signatureParameters"][i]["schema"]["properties"][0][
                        "schema"
                    ]
                )
            else:
                param_type = get_type(yaml_data["signatureParameters"][i]["schema"])
            if param_name == "body":
                try:
                    param_name = yaml_data["signatureParameters"][i]["schema"][
                        "properties"
                    ][0]["serializedName"]
                except:
                    param_name = param_name
            if yaml_data["signatureParameters"][i].get("required"):
                required = yaml_data["signatureParameters"][i]["required"]
            else:
                required = False
        else:
            param_type = None
            param_name = None

        return cls(
            operation = yaml_data['language']['default']['name'],
            param_type=param_type,
            param_name=param_name,
            required=required,
            namespace=name,
            default=default,
            json_request=json_request,
        )

    def add_token(self, token):
        self.Tokens.append(token)

    def get_tokens(self):
        return self.Tokens

    def to_token(self):

        if self.type is not None:
            # Create parameter type token
            self.add_stringliteral(None, self.type, None)
            self.overview_tokens.append(Token(self.type, TokenKind.StringLiteral))

            # If parameter is optional, token for ? created
            if not self.required:
                self.add_stringliteral(None, "?", None)
                self.overview_tokens.append(Token("?", TokenKind.StringLiteral))
            self.add_space()
            self.overview_tokens.append(Token(" ", TokenKind.Text))
            # Create parameter name token
            self.add_text(self.namespace + self.operation + self.type+ self.name + "details", self.name, None)
            token = Token(self.name, TokenKind.Text)
            token.set_definition_id(self.namespace + self.operation + self.type+ self.name + "overview")
            self.overview_tokens.append(token)

            # Check if parameter has a default value or not
            if self.default is not None:
                self.add_space()
                self.overview_tokens.append(Token(" ", TokenKind.Text))
                self.add_text(None, "=", None)
                self.overview_tokens.append(Token("=", TokenKind.Text))
                self.add_space()
                self.overview_tokens.append(Token(" ", TokenKind.Text))
                if self.type == "string":
                    self.add_text(None, "'" + str(self.default) + "'", None)
                    self.overview_tokens.append(
                        Token("'" + str(self.default) + "'", TokenKind.Text)
                    )
                else:
                    self.add_text(None, str(self.default), None)
                    self.overview_tokens.append(
                        Token(str(self.default), TokenKind.Text)
                    )

    def to_json(self):
        obj_dict = {}
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


def get_type(data, page=False):
    # Get type
    try:
        return_type = data["type"]
        if return_type == "choice":
            return_type = data["choiceType"]["type"]
        if return_type == "dictionary":
            value = data["elementType"]["type"]
            if value == "object" or value == "array" or value == "dictionary":
                value = get_type(data["elementType"])
            return_type += "[string, " + value + "]"
        if return_type == "object":
            return_type = data["language"]["default"]["name"]
            if page:
                return_type = get_type(data["properties"][0]["schema"], True)
        if return_type == "array":
            if (
                data["elementType"]["type"] != "object"
                and data["elementType"]["type"] != "choice"
            ):
                return_type = data["elementType"]["type"] + "[]"
            elif not page:
                return_type = data["elementType"]["language"]["default"]["name"] + "[]"
            else:
                return_type = data["elementType"]["language"]["default"]["name"]
        if "number" in return_type:
            if data["precision"] == 32:
               return_type = re.sub("number", "float32",return_type)
            if data["precision"] == 64:
                return_type= re.sub("number", "float64",return_type)
        if "integer" in return_type:
            if data["precision"] == 32:
                return_type = re.sub("integer", "int32",return_type)
            if data["precision"] == 64:
                return_type = re.sub("integer", "int64",return_type)
        if return_type == "boolean":
            return_type = "bool"
        else:
            return_type = return_type
    except:
        return_type = None
    return return_type
