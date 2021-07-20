import json

JSON_PATH = "C:\\Users\\t-llawrence\\Desktop\\azure-sdk-tools\\purview_test.json"
OUTPUT =  []

class TestJsonFormat:
    
    def test_print_out(self):
        with open(JSON_PATH) as f:
            data = json.load(f)
        d = []
        OUTPUT = data['Tokens']
        for token in data['Tokens']:
            if token['Kind']==4 or token['Kind']==3 or token['Kind']==0 or token['Kind']==6 or token['Kind']==7 or token['Kind']==8 or token['Kind']==10:
                d.append(token['Value'])
            if token['Kind']==2:
                d.append(" ")
            if token['Kind']==1:
                d.append("\n")
            else:
                pass
        print(*d)
    
    def test_operation_format(self):
         for t in OUTPUT:
                if t['Value']=="OperationGroup":
                    assert t['Kind']==4
    
    def test_request_format(self):
        r = []
        for t in OUTPUT:
            if t['Kind']==10:
                r.append(t['Value'])
        print(*r)