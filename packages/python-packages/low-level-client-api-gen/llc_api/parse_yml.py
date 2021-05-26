import yaml
import json
import argparse
import logging
import os
import tempfile
from .llc_view_models import LLCOperationGroupView, LLCParameterView, LLCOperationView, LLCClientView 

#Allow for user to input file names
# OUTPUT_FILE = input("Enter OUTPUT file name: ") + ".json"
#YAML_FILE = "code-model-v4-no-tags.yaml"
OUTPUT_FILE = "llc_api_view.json"

class llc_generator:
    def __init__(self):
        parser = argparse.ArgumentParser(
            description="Parse a python package and generate json token file to be supplied to API review tool"
        )
        parser.add_argument(
            "--pkg-path", required=True, help=("Package root path"),
        )
        parser.add_argument(
            "--temp-path", 
            help=("Temp path to extract package"),
            default=tempfile.gettempdir(),
        )
        parser.add_argument(
            "--out-path",
            default=os.getcwd(),
            help=("Path to generate json file with parsed tokens"),
        )
        parser.add_argument(
            "--verbose",
            help=("Enable verbose logging"),
            default=False,
            action="store_true",
        )

        parser.add_argument(
            "--hide-report",
            help=("Hide diagnostic report"),
            default=False,
            action="store_true",
        )

        parser.add_argument(
            "--filter-namespace",
            help=("Generate Api view only for a specific namespace"),
        )
        

        args = parser.parse_args()
        if not os.path.exists(args.pkg_path):
            logging.error("Package path [{}] is invalid".format(args.pkg_path))
            exit(1)
        elif not os.path.exists(args.temp_path):
            logging.error("Temp path [{0}] is invalid".format(args.temp_path))
            exit(1)


        self.pkg_path = args.pkg_path
        self.temp_path = args.temp_path
        self.out_path = args.out_path
        self.hide_report = args.hide_report
        if args.verbose:
            logging.getLogger().setLevel(logging.DEBUG)

        self.filter_namespace = ''
        if args.filter_namespace:
            self.filter_namespace = args.filter_namespace

    def parse_yaml(self):
        # open the yaml file and load data
        with open(self.pkg_path) as f:
            data = yaml.safe_load(f)

        #Create main_view for LLC API View
        main_view = LLCClientView.from_yaml(data)


        #Iterate through Operations in OperationGroups
        for k in range(0,len(data["operationGroups"])):
            operation_groups = LLCOperationGroupView.from_yaml(data,k,main_view.namespace)
            # for i in range(0,len(data["operationGroups"][k]["operations"])):
            #     op = LLCOperationView.from_yaml(data,k,i)
            #     main_view.add_operation(LLCOperationView(op.operation,op.parameters))
                #Add each operation to the main_view
            main_view.add_operation_group(LLCOperationGroupView(operation_groups.operation_group,operation_groups.operations,main_view.namespace))

        # Write output to .json
        j = open (OUTPUT_FILE, "w")
        j.write(json.dumps(main_view.to_json(), default= lambda o : o.__dict__))

        j.close()
