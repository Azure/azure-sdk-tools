import yaml
import json
from llc_api.llc_view_models import ParameterView, LLCOperationView, LLCClientView 

#Allow for user to input file names
# YAML_FILE = input("Enter YAML file name: ") + ".yaml"
# OUTPUT_FILE = input("Enter OUTPUT file name: ") + ".json"
YAML_FILE = "code-model-v4-no-tags.yaml"
OUTPUT_FILE = "llc_api_view.json"

# open the yaml file and load data
with open(YAML_FILE) as f:
    data = yaml.safe_load(f)

#Create main_view for LLC API View
main_view = LLCClientView.from_yaml(data)

#Iterate through Operations in OperationGroups
for k in range(0,len(data["operationGroups"])):
    for i in range(0,len(data["operationGroups"][k]["operations"])):
        op = LLCOperationView.from_yaml(data,i)

        #Add each operation to the main_view
        main_view.add_operation(LLCOperationView(op.operation,op.parameters))

# Write output to .json
j = open (OUTPUT_FILE, "w")
j.write(json.dumps(main_view.to_json(), default= lambda o : o.__dict__))

j.close()
