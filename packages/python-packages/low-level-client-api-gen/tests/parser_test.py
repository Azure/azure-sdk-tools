# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
import os, sys
sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))

from llcapi.llc_view_models import LLCClientView
import yaml


class TestParser:
    def _test_client_name(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        client = LLCClientView(data)
        print(client.Name)
        assert client.Name == "Batch Document Translation Client"
    
    def _test_client_operationGroups(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        client = LLCClientView(data)
        groups = client.Operation_Groups 
        for g in groups:
            assert g.name == "DocumentTranslation"
    
    def _test_client_operation(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        client = LLCClientView(data)
        operations = client.Operations
        for o in operations:
            print(o)