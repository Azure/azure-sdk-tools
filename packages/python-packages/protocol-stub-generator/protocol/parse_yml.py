# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
import yaml
import json
import argparse
import logging
import os
import tempfile
from .protocol_models import ProtocolClientView

class LLCGenerator:
    def __init__(self):
        parser = argparse.ArgumentParser(
            description="Parse a python package and generate json token file to be supplied to API review tool"
        )
        parser.add_argument(
            "--pkg-path",
            required=True,
            help=("Package root path"),
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


    def parse_yaml(self):
        # open the yaml file and load data
        with open(self.pkg_path) as f:
            data = yaml.safe_load(f)

        create_python_name(data)

        # Create main_view for LLC API View
        main_view = ProtocolClientView.from_yaml(data)

        parsed_json = json.dumps(main_view.to_json(), default=lambda o: o.__dict__)

        # Write output to .json
        # APIView provides full path with json file name.
        # if given path doesn't have json file name then append file name
        out_file_path = self.out_path
        if not out_file_path.endswith(".json"):
            file_name, _ = os.path.splitext(self.pkg_path)
            out_file_path = os.path.join(self.out_path, os.path.basename(file_name) + ".json")
        logging.info ("Generating out file: [{0}]".format(out_file_path))
        with open(out_file_path, "w") as j:
            j.write(parsed_json)


def create_python_name(data):
    ##Iterate through all data duplicate all language_default_name to lanuage_python_name for ops and params so you have both
    data["language"]["python"] = data["language"].get("default")
    for op_group in data["schemas"]:
        for op in range(0, len(data["schemas"][op_group])):
            data["schemas"][op_group][op]["language"]["python"] = data["schemas"][
                op_group
            ][op]["language"].get("default")
            if op_group == "objects":
                for o in range(
                    0, len(data["schemas"][op_group][op].get("properties", []))
                ):
                    data["schemas"][op_group][op]["properties"][o]["language"][
                        "python"
                    ] = data["schemas"][op_group][op]["properties"][o]["language"].get(
                        "default"
                    )
            if op_group == "arrays":
                data["schemas"][op_group][op]["elementType"]["language"][
                    "python"
                ] = data["schemas"][op_group][op]["elementType"]["language"].get(
                    "default"
                )
                for p in range(
                    0,
                    len(
                        data["schemas"][op_group][op]["elementType"].get(
                            "properties", []
                        )
                    ),
                ):
                    data["schemas"][op_group][op]["elementType"]["properties"][p][
                        "language"
                    ]["python"] = data["schemas"][op_group][op]["elementType"][
                        "properties"
                    ][
                        p
                    ][
                        "language"
                    ].get(
                        "default"
                    )
            if op_group == "choices" or op_group == "sealedChoices":
                for o in range(0, len(data["schemas"][op_group][op].get("choices"))):
                    data["schemas"][op_group][op]["choices"][o]["language"][
                        "python"
                    ] = data["schemas"][op_group][op]["choices"][o]["language"].get(
                        "default"
                    )
    for op_group in range(0, len(data["operationGroups"])):
        data["operationGroups"][op_group]["language"]["python"] = data[
            "operationGroups"
        ][op_group]["language"].get("default")
        for op in range(0, len(data["operationGroups"][op_group]["operations"])):
            data["operationGroups"][op_group]["operations"][op]["language"][
                "python"
            ] = data["operationGroups"][op_group]["operations"][op]["language"].get(
                "default"
            )
            for p in range(
                0,
                len(
                    data["operationGroups"][op_group]["operations"][op].get(
                        "signatureParameters"
                    )
                ),
            ):
                data["operationGroups"][op_group]["operations"][op][
                    "signatureParameters"
                ][p]["language"]["python"] = data["operationGroups"][op_group][
                    "operations"
                ][
                    op
                ][
                    "signatureParameters"
                ][
                    p
                ][
                    "language"
                ].get(
                    "default"
                )
            for p in range(
                0,
                len(
                    data["operationGroups"][op_group]["operations"][op].get(
                        "parameters"
                    )
                ),
            ):
                data["operationGroups"][op_group]["operations"][op]["parameters"][p][
                    "language"
                ]["python"] = data["operationGroups"][op_group]["operations"][op][
                    "parameters"
                ][
                    p
                ][
                    "language"
                ].get(
                    "default"
                )
            for p in range(
                0,
                len(
                    data["operationGroups"][op_group]["operations"][op].get("requests")
                ),
            ):
                data["operationGroups"][op_group]["operations"][op]["requests"][p][
                    "language"
                ]["python"] = data["operationGroups"][op_group]["operations"][op][
                    "requests"
                ][
                    p
                ][
                    "language"
                ].get(
                    "default"
                )
                for r in range(
                    0,
                    len(
                        data["operationGroups"][op_group]["operations"][op]["requests"][
                            p
                        ].get("parameters", [])
                    ),
                ):
                    data["operationGroups"][op_group]["operations"][op]["requests"][p][
                        "parameters"
                    ][r]["language"]["python"] = data["operationGroups"][op_group][
                        "operations"
                    ][
                        op
                    ][
                        "requests"
                    ][
                        p
                    ][
                        "parameters"
                    ][
                        r
                    ][
                        "language"
                    ].get(
                        "default"
                    )
            for p in range(
                0,
                len(
                    data["operationGroups"][op_group]["operations"][op].get("responses")
                ),
            ):
                data["operationGroups"][op_group]["operations"][op]["responses"][p][
                    "language"
                ]["python"] = data["operationGroups"][op_group]["operations"][op][
                    "responses"
                ][
                    p
                ][
                    "language"
                ].get(
                    "default"
                )
