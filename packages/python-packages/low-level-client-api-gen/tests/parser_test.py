# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

import yaml
from llcapi import LLCClientView 


class TestParser:
    def _test_client_name(self):
        self.path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(self.path) as f:
            self.data = yaml.safe_load(f)
        client = LLCClientView(self.data)
        assert client.name == "Batch Document Translation Client"
    
    def _test_client_operationGroups(self):
        self.path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(self.path) as f:
            self.data = yaml.safe_load(f)
        client = LLCClientView(self.data)
        groups = client.Operation_Groups 
        for g in groups:
            assert g.name == "DocumentTranslation"