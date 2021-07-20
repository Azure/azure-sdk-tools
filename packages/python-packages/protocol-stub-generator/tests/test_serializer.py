import os, sys
import unittest
sys.path.append(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))

from protocol.protocol_models import ProtocolClientView, ProtocolOperationView
from protocol.parse_yml import create_python_name
import yaml

PATH = "c:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"


class TestCases(unittest.TestCase):

    def test_client_name(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)
        client = ProtocolClientView.from_yaml(data)
        assert client.Name == 'Batch Document Translation Client'
        
    def test_client_operationGroups(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)
        client = ProtocolClientView.from_yaml(data)
        groups = client.Operation_Groups 
        for g in groups:
            assert g.operation_group == "DocumentTranslation"
    
    def test_client_operation(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        client = ProtocolClientView.from_yaml(data)
        groups = client.Operation_Groups 
        for g in groups:
            assert len(g.operations) == 9
    
    def test_client_parameter_return_type(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        client = ProtocolClientView.from_yaml(data)
        groups = client.Operation_Groups
        for g in groups:
            for o in g.operations:
                if o.return_type == "void":
                    assert True
    
    def test_parameter_request(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        operation = ProtocolOperationView.from_yaml(data,0,0," ")
        assert operation.json_request !=None
        operation1 = ProtocolOperationView.from_yaml(data,0,1," ")
        assert operation1.json_request == {}

    def test_parameter_response(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        for i in range(0,8):
            operation = ProtocolOperationView.from_yaml(data,0,i," ")
            assert operation.json_response =={}
    
    def test_parameter_empty(self):
        with open(PATH) as f:
            data = yaml.safe_load(f)
        create_python_name(data)   
        
        operation = ProtocolOperationView.from_yaml(data,0,8," ")
        assert operation.parameters == []