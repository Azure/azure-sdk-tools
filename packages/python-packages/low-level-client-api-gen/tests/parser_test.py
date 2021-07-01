# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
import os, sys
sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))

from llcapi.llc_view_models import LLCClientView, LLCOperationView
from llcapi.parse_yml import create_python_name
import yaml


class TestParser:
    def _test_client_name(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)
        client = LLCClientView.from_yaml(data)
        assert client.Name == 'Batch Document Translation Client'
    
    def _test_client_operationGroups(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)
        client = LLCClientView.from_yaml(data)
        groups = client.Operation_Groups 
        for g in groups:
            assert g.operation_group == "DocumentTranslation"
    
    def _test_client_operation(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        client = LLCClientView.from_yaml(data)
        groups = client.Operation_Groups 
        for g in groups:
            assert len(g.operations) == 9
    
    def _test_client_parameter_return_type(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        client = LLCClientView.from_yaml(data)
        groups = client.Operation_Groups
        for g in groups:
            for o in g.operations:
                if o.return_type == "void":
                    assert True
    
    def _test_parameter_request(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        operation = LLCOperationView.from_yaml(data,0,0," ")
        assert operation.json_request !=None
        operation1 = LLCOperationView.from_yaml(data,0,1," ")
        assert operation1.json_request == {}

    def _test_parameter_response(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        for i in range(0,8):
            operation = LLCOperationView.from_yaml(data,0,i," ")
            assert operation.json_response =={}
    
    def _test_parameter_empty(self):
        path = "C:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(path) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        
        operation = LLCOperationView.from_yaml(data,0,8," ")
        assert operation.parameters == []