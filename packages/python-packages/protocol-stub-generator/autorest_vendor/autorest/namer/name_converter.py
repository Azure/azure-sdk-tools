# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
import re
from typing import cast, Any, Dict, List, Match, Optional
from .python_mappings import basic_latin_chars, reserved_words, PadType

_M4_HEADER_PARAMETERS = ["content_type", "accept"]
class NameConverter:
    @staticmethod
    def convert_yaml_names(yaml_data: Dict[str, Any]) -> None:
        NameConverter._convert_language_default_python_case(yaml_data)
        yaml_data["info"]["python_title"] = NameConverter._to_valid_python_name(
            name=yaml_data["info"]["title"].replace(" ", ""), convert_name=True
        )
        yaml_data['info']['pascal_case_title'] = yaml_data["language"]["default"]["name"]
        if yaml_data['info'].get("description"):
            if yaml_data["info"]["description"][-1] != ".":
                yaml_data["info"]["description"] += "."
        else:
            yaml_data["info"]["description"] = yaml_data['info']['pascal_case_title'] + "."
        NameConverter._convert_schemas(yaml_data['schemas'])
        NameConverter._convert_operation_groups(yaml_data['operationGroups'], yaml_data['info']['pascal_case_title'])
        if yaml_data.get('globalParameters'):
            NameConverter._convert_global_parameters(yaml_data['globalParameters'])

    @staticmethod
    def _convert_global_parameters(global_parameters: List[Dict[str, Any]]) -> None:
        for global_parameter in global_parameters:
            NameConverter._convert_language_default_python_case(global_parameter)

    @staticmethod
    def _convert_operation_groups(operation_groups: List[Dict[str, Any]], code_model_title: str) -> None:
        for operation_group in operation_groups:
            NameConverter._convert_language_default_python_case(
                operation_group, pad_string=PadType.Model, convert_name=True
            )
            operation_group_name = operation_group['language']['default']['name']
            if not operation_group_name:
                operation_group['language']['python']['className'] = code_model_title + "OperationsMixin"
            elif operation_group_name == 'Operations':
                operation_group['language']['python']['className'] = operation_group_name
            else:
                operation_group['language']['python']['className'] = operation_group_name + "Operations"
            for operation in operation_group['operations']:
                NameConverter._convert_language_default_python_case(operation, pad_string=PadType.Method)
                if operation_group_name:
                    operation['language']['python']['operationGroupName'] = (
                        operation_group['language']['python']['name'].lower()
                    )
                else:
                    operation['language']['python']['operationGroupName'] = ""
                for exception in operation.get('exceptions', []):
                    NameConverter._convert_language_default_python_case(exception)
                for parameter in operation.get("parameters", []):
                    NameConverter._add_multipart_information(parameter, operation)
                    NameConverter._convert_language_default_python_case(parameter, pad_string=PadType.Parameter)
                for request in operation.get("requests", []):
                    NameConverter._convert_language_default_python_case(request)
                    for parameter in request.get("parameters", []):
                        NameConverter._add_multipart_information(parameter, request)
                        NameConverter._convert_language_default_python_case(parameter, pad_string=PadType.Parameter)
                        if parameter.get("origin", "") == "modelerfour:synthesized/content-type":
                            parameter["required"] = False
                NameConverter._handle_m4_header_parameters(operation.get("requests", []))
                for response in operation.get("responses", []):
                    NameConverter._convert_language_default_python_case(response)
                if operation.get("extensions"):
                    NameConverter._convert_extensions(operation)

    @staticmethod
    def _handle_m4_header_parameters(requests):
        m4_header_params = []
        for request in requests:
            m4_header_params.extend([
                p for p in request.get('parameters', [])
                if NameConverter._is_schema_an_m4_header_parameter(p['language']['default']['name'], p)
            ])
        m4_header_params_to_remove = []
        for m4_header in _M4_HEADER_PARAMETERS:
            params_of_header = [
                p for p in m4_header_params
                if p['language']['default']['name'] == m4_header
            ]
            if len(params_of_header) < 2:
                continue
            param_schema_to_param = {  # if they share the same schema, we don't need to keep both of them in this case
                id(param['schema']): param
                for param in params_of_header
            }
            if len(param_schema_to_param) == 1:
                # we'll remove the ones that aren't the first
                m4_header_params_to_remove.extend([
                    id(p) for p in params_of_header[1:]
                ])
            else:
                # currently there's max of 2, so assume this is 2 for now
                # in this case, one of them is a constant and one is not.
                # Set the client default value to the one of the constant
                param_with_constant_schema = next(p for p in params_of_header if p['schema']['type'] == 'constant')
                param_with_enum_schema = next(
                    p for p in params_of_header
                    if p['schema']['type'] == 'sealed-choice' or p.schema['type'] == 'choice'
                )
                param_with_enum_schema['clientDefaultValue'] = param_with_constant_schema['schema']['value']['value']
                m4_header_params_to_remove.append(id(param_with_constant_schema))

        for request in requests:
            if not request.get('parameters'):
                continue
            request['parameters'] = [p for p in request['parameters'] if id(p) not in m4_header_params_to_remove]


    @staticmethod
    def _add_multipart_information(parameter: Dict[str, Any], request: Dict[str, Any]):
        multipart = request["protocol"].get("http", {}).get("multipart", False)
        if multipart:
            if parameter["protocol"]["http"]["in"] == "body":
                parameter["language"]["default"]["multipart"] = True
            if parameter["language"]["default"]["serializedName"] == "Content-Type":
                parameter['schema']['value']['value'] = None


    @staticmethod
    def _convert_extensions(operation: Dict[str, Any]) -> None:
        operation_extensions = operation["extensions"]
        if operation_extensions.get('x-ms-pageable'):
            operation["extensions"]["pager-sync"] = operation_extensions.get(
                "x-python-custom-pager-sync", "azure.core.paging.ItemPaged"
            )
            operation["extensions"]["pager-async"] = operation_extensions.get(
                "x-python-custom-pager-async", "azure.core.async_paging.AsyncItemPaged"
            )
        if operation_extensions.get("x-ms-long-running-operation"):
            # poller
            operation["extensions"]["poller-sync"] = operation_extensions.get(
                "x-python-custom-poller-sync", "azure.core.polling.LROPoller"
            )
            operation["extensions"]["poller-async"] = operation_extensions.get(
                "x-python-custom-poller-async", "azure.core.polling.AsyncLROPoller"
            )

            # polling methods
            sync_polling_method_directive = "x-python-custom-default-polling-method-sync"
            operation["extensions"]["default-polling-method-sync"] = {
                "azure-arm": operation_extensions.get(
                    sync_polling_method_directive, "azure.mgmt.core.polling.arm_polling.ARMPolling"
                ),
                "data-plane": operation_extensions.get(
                    sync_polling_method_directive, "azure.core.polling.base_polling.LROBasePolling"
                ),
            }
            async_polling_method_directive = "x-python-custom-default-polling-method-async"
            operation["extensions"]["default-polling-method-async"] = {
                "azure-arm": operation_extensions.get(
                    async_polling_method_directive, "azure.mgmt.core.polling.async_arm_polling.AsyncARMPolling"
                ),
                "data-plane": operation_extensions.get(
                    async_polling_method_directive, "azure.core.polling.async_base_polling.AsyncLROBasePolling"
                ),
            }

            operation["extensions"]["default-no-polling-method-sync"] = "azure.core.polling.NoPolling"
            operation["extensions"]["default-no-polling-method-async"] = "azure.core.polling.AsyncNoPolling"
            operation["extensions"]["base-polling-method-sync"] = "azure.core.polling.PollingMethod"
            operation["extensions"]["base-polling-method-async"] = "azure.core.polling.AsyncPollingMethod"

    @staticmethod
    def _convert_schemas(schemas: Dict[str, Any]) -> None:
        for enum in schemas.get("sealedChoices", []) + schemas.get("choices", []):
            NameConverter._convert_enum_schema(enum)
        for obj in schemas.get("objects", []) + schemas.get("groups", []):
            NameConverter._convert_object_schema(obj)
        for type_list, schema_yamls in schemas.items():
            for schema in schema_yamls:
                if type_list == "objects":
                    continue
                if type_list in ["arrays", "dictionaries"]:
                    NameConverter._convert_language_default_python_case(schema)
                    NameConverter._convert_language_default_python_case(schema["elementType"])
                elif type_list == "constants":
                    NameConverter._convert_language_default_python_case(schema)
                    NameConverter._convert_language_default_python_case(schema["value"])
                    NameConverter._convert_language_default_python_case(schema["valueType"])
                else:
                    NameConverter._convert_language_default_python_case(schema)

    @staticmethod
    def _convert_enum_schema(schema: Dict[str, Any]) -> None:
        NameConverter._convert_language_default_pascal_case(schema)
        for choice in schema["choices"]:
            NameConverter._convert_language_default_python_case(choice, pad_string=PadType.Enum, all_upper=True)

    @staticmethod
    def _convert_object_schema(schema: Dict[str, Any]) -> None:
        NameConverter._convert_language_default_pascal_case(schema)
        schema_description = schema["language"]["python"]["description"]
        if not schema_description:
            # what is being used for empty ObjectSchema descriptions
            schema_description = schema["language"]["python"]["name"]
        if schema_description and schema_description[-1] != ".":
            schema_description += "."
        schema["language"]["python"]["description"] = schema_description
        for prop in schema.get("properties", []):
            NameConverter._convert_language_default_python_case(schema=prop, pad_string=PadType.Property)

    @staticmethod
    def _is_schema_an_m4_header_parameter(schema_name: str, schema: Dict[str, Any]) -> bool:
        return (
            schema_name in _M4_HEADER_PARAMETERS and
            schema.get('protocol', {}).get('http', {}).get('in', {}) == 'header'
        )

    @staticmethod
    def _convert_language_default_python_case(
        schema: Dict[str, Any],
        *,
        pad_string: Optional[PadType] = None,
        convert_name: bool = False,
        all_upper: bool = False
    ) -> None:
        if not schema.get("language") or schema["language"].get("python"):
            return
        schema['language']['python'] = dict(schema['language']['default'])
        schema_name = schema['language']['default']['name']
        schema_python_name = schema['language']['python']['name']

        if not NameConverter._is_schema_an_m4_header_parameter(
            schema_name, schema
        ):
            # only escaping name if it's not a content_type header parameter
            schema_python_name = NameConverter._to_valid_python_name(
                name=schema_name, pad_string=pad_string, convert_name=convert_name
            )
        # need to add the lower in case certain words, like LRO, are overriden to
        # always return LRO. Without .lower(), for example, begin_lro would be
        # begin_LRO
        schema['language']['python']['name'] = (
            schema_python_name.upper() if all_upper else schema_python_name.lower()
        )

        schema_description = schema["language"]["default"]["description"].strip()
        if pad_string == PadType.Method and not schema_description and not schema["language"]["default"].get("summary"):
            schema_description = schema["language"]["python"]["name"]
        if schema_description and schema_description[-1] != ".":
            schema_description += "."
        schema["language"]["python"]["description"] = schema_description

        schema_summary = schema["language"]["python"].get("summary")
        if schema_summary:
            schema_summary = schema_summary.strip()
            if schema_summary[-1] != ".":
                schema_summary += "."
            schema["language"]["python"]["summary"] = schema_summary

    @staticmethod
    def _convert_language_default_pascal_case(schema: Dict[str, Any]) -> None:
        if schema["language"].get("python"):
            return
        schema['language']['python'] = dict(schema['language']['default'])

        schema_description = schema["language"]["default"]["description"].strip()

        schema["language"]["python"]["description"] = schema_description

    @staticmethod
    def _to_pascal_case(name: str) -> str:
        name_list = re.split("[^a-zA-Z\\d]", name)
        name_list = [s[0].upper() + s[1:] if len(s) > 1 else s.upper() for s in name_list]
        return "".join(name_list)

    @staticmethod
    def _to_valid_python_name(name: str, *, pad_string: Optional[PadType] = None, convert_name: bool = False) -> str:
        if not name:
            return NameConverter._to_python_case(pad_string.value if pad_string else "")
        escaped_name = NameConverter._get_escaped_reserved_name(
            NameConverter._to_valid_name(name.replace("-", "_"), ["_"]), pad_string
        )
        if convert_name or name != escaped_name:
            return NameConverter._to_python_case(escaped_name)
        return escaped_name

    @staticmethod
    def _to_python_case(name: str) -> str:
        def replace_upper_characters(m: Match[str]) -> str:
            match_str = m.group().lower()
            if m.start() > 0 and name[m.start() - 1] == "_":
                # we are good if a '_' already exists
                return match_str
            # the first letter should not have _
            prefix = "_" if m.start() > 0 else ""

            # we will add an extra _ if there are multiple upper case chars together
            next_non_upper_case_char_location = m.start() + len(match_str)
            if (
                len(match_str) > 2
                and len(name) - next_non_upper_case_char_location > 1
                and name[next_non_upper_case_char_location].isalpha()
            ):

                return prefix + match_str[: len(match_str) - 1] + "_" + match_str[len(match_str) - 1]

            return prefix + match_str
        return re.sub("[A-Z]+", replace_upper_characters, name)

    @staticmethod
    def _get_escaped_reserved_name(name: str, pad_string: Optional[PadType] = None) -> str:
        if name is None:
            raise ValueError("The value for name can not be None")
        try:
            # check to see if name is reserved for the type of name we are converting
            pad_string = cast(PadType, pad_string)
            # there are some private variables, such as grouped parameters
            # that are private. We still want to escape them for LLC
            name_prefix = ""
            if name[0] == "_":
                # i am private
                name_prefix = "_"
                name = name[1:]
            if pad_string and name.lower() in reserved_words[pad_string]:
                name += pad_string.value
            return name_prefix + name
        except AttributeError:
            raise ValueError(f"The name {name} is a reserved word and you have not specified a pad string for it.")

    @staticmethod
    def _remove_invalid_characters(name: str, allowed_characters: List[str]) -> str:
        name = name.replace("[]", "Sequence")
        valid_string = "".join([n for n in name if n.isalpha() or n.isdigit() or n in allowed_characters])
        return valid_string

    @staticmethod
    def _to_valid_name(name: str, allowed_characters: List[str]) -> str:
        correct_name = NameConverter._remove_invalid_characters(name, allowed_characters)

        # here we have an empty string or a string that consists only of invalid characters
        if not correct_name or correct_name[0] in basic_latin_chars.keys():
            ret_name = ""
            for c in name:
                if c in basic_latin_chars.keys():
                    ret_name += basic_latin_chars[c]
                else:
                    ret_name += c
            correct_name = NameConverter._remove_invalid_characters(ret_name, allowed_characters)

        if not correct_name:
            raise ValueError(
                "Property name {} cannot be used as an identifier, as it contains only invalid characters.".format(name)
            )
        return correct_name
