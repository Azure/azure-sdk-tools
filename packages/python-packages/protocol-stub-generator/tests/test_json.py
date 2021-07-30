# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
import json
import os
import pytest

JSON_PATH = os.getenv("TESTJSONPATH")


class TestJsonFormat:
    def format_tokens(self, data):
        d = []
        for token in data:
            if (
                token["Kind"] == 4
                or token["Kind"] == 3
                or token["Kind"] == 0
                or token["Kind"] == 6
                or token["Kind"] == 7
                or token["Kind"] == 8
                or token["Kind"] == 10
            ):
                d.append(token["Value"])
            if token["Kind"] == 2:
                d.append(" ")
            if token["Kind"] == 1:
                d.append("\n")
        return d
    def test_print_out(self):
        with open(JSON_PATH) as f:
            data = json.load(f)
        d = self.format_tokens(data["Tokens"])
        print(*d)



    def test_operation_format(self):
        with open(JSON_PATH) as f:
            data = json.load(f)
        OUTPUT = data["Tokens"]
        for t in OUTPUT:
            if t["Value"] == "OperationGroup":
                assert t["Kind"] == 0

    def test_request_format(self):
        r = []
        d = []
        start = False
        with open(JSON_PATH) as f:
            data = json.load(f)
        for t in data["Tokens"]:
            if t["Kind"] == 12:
                start = False
            if t["Kind"] == 11:
                start = True
            if start:
                r.append(t)
        d = self.format_tokens(r)
        print(*d)