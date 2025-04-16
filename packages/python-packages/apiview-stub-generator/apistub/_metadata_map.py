#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------

import json
import os

MAPPING_FILE_NAME = "apiview-properties.json"
OLD_MAPPING_FILE_NAME = "apiview_mapping_python.json"

"""
Loads metadata from the mapping file for use
by the stub generator.
"""


class MetadataMap:

    def __init__(self, pkg_path, mapping_path=None):
        if not mapping_path:
            if pkg_path.endswith((".whl", ".zip", ".tar.gz")):
                self.cross_language_map = {}
                self.cross_language_package_id = ""
                return
            mapping_path = os.path.join(pkg_path, MAPPING_FILE_NAME)
            old_mapping_path = os.path.join(pkg_path, OLD_MAPPING_FILE_NAME)
        try:
            with open(mapping_path, "r") as json_file:
                mapping = json.load(json_file)
                self.cross_language_map = mapping.get("CrossLanguageDefinitionId", {})
                self.cross_language_package_id = mapping.get(
                    "CrossLanguagePackageId", ""
                )
        except OSError:
            try:
                with open(old_mapping_path, "r") as json_file:
                    mapping = json.load(json_file)
                    self.cross_language_map = mapping.get("CrossLanguageDefinitionId", {})
                    self.cross_language_package_id = mapping.get(
                        "CrossLanguagePackageId", ""
                    )
            except OSError:
                self.cross_language_map = {}
                self.cross_language_package_id = ""
                return
