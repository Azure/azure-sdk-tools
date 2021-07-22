# -------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for
# license information.
# --------------------------------------------------------------------------
from typing import Any, Callable, Dict, List, Optional, Union, TYPE_CHECKING
from abc import abstractmethod
from .base_model import BaseModel
from .schema_response import SchemaResponse

if TYPE_CHECKING:
    from . import ParameterListType


_M4_HEADER_PARAMETERS = ["content_type", "accept"]

def get_converted_parameters(yaml_data: Dict[str, Any], parameter_converter: Callable):
    multiple_requests = len(yaml_data["requests"]) > 1

    multiple_media_type_parameters = []
    parameters = [parameter_converter(yaml) for yaml in yaml_data.get("parameters", [])]

    for request in yaml_data["requests"]:
        for yaml in request.get("parameters", []):
            parameter = parameter_converter(yaml)
            name = yaml["language"]["python"]["name"]
            if name in _M4_HEADER_PARAMETERS:
                parameters.append(parameter)
            elif multiple_requests:
                parameter.has_multiple_media_types = True
                multiple_media_type_parameters.append(parameter)
            else:
                parameters.append(parameter)

    if multiple_media_type_parameters:
        body_parameters_name_set = set(
            p.serialized_name for p in multiple_media_type_parameters
        )
        if len(body_parameters_name_set) > 1:
            raise ValueError(
            f"The body parameter with multiple media types has different names: {body_parameters_name_set}"
        )


    parameters_index = {id(parameter.yaml_data): parameter for parameter in parameters}

    # Need to connect the groupBy and originalParameter
    for parameter in parameters:
        parameter_grouped_by_id = id(parameter.grouped_by)
        if parameter_grouped_by_id in parameters_index:
            parameter.grouped_by = parameters_index[parameter_grouped_by_id]

        parameter_original_id = id(parameter.original_parameter)
        if parameter_original_id in parameters_index:
            parameter.original_parameter = parameters_index[parameter_original_id]

    return parameters, multiple_media_type_parameters

class BaseBuilder(BaseModel):
    """Base class for Operations and Request Builders"""

    def __init__(
        self,
        yaml_data: Dict[str, Any],
        name: str,
        description: str,
        parameters: "ParameterListType",
        responses: Optional[List[SchemaResponse]] = None,
        summary: Optional[str] = None,
    ) -> None:
        super().__init__(yaml_data=yaml_data)
        self.name = name
        self.description = description
        self.parameters = parameters
        self.responses = responses or []
        self.summary = summary

    @property
    def default_content_type_declaration(self) -> str:
        return f'"{self.parameters.default_content_type}"'

    def get_response_from_status(self, status_code: int) -> SchemaResponse:
        for response in self.responses:
            if status_code in response.status_codes:
                return response
        raise ValueError(f"Incorrect status code {status_code}, operation {self.name}")

    @property
    def success_status_code(self) -> List[Union[str, int]]:
        """The list of all successfull status code."""
        return [code for response in self.responses for code in response.status_codes if code != "default"]

    @staticmethod
    @abstractmethod
    def get_parameter_converter() -> Callable:
        ...
