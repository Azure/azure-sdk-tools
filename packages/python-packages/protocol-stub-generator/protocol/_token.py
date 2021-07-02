# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
from ._token_kind import TokenKind


class Token:
    """Entity class to hold individual token information"""

    def __init__(self, value="", kind=TokenKind.Text):
        self.Kind = kind
        self.DefinitionId = None
        self.NavigateToId = None
        self.Value = value

    def set_definition_id(self, id):
        self.DefinitionId = id

    def set_navigation_id(self, id):
        self.NavigateToId = id

    def set_value(self, value):
        self.Value = value
