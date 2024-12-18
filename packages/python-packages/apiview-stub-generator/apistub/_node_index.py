#!/usr/bin/env python

# --------------------------------------------------------------------------------------------
# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License. See License.txt in the project root for license information.
# --------------------------------------------------------------------------------------------


class NodeIndex:
    """Maintains name to navigation ID"""

    def __init__(self):
        self.index = {}

    def add(self, name, node):
        if name in self.index:
            raise ValueError("Index already has {} node".format(name))
        self.index[name] = node

    def get(self, name):
        return self.index.get(name, None)

    def get_id(self, name):
        node = self.get(name)
        if node and hasattr(node, "namespace_id"):
            return node.namespace_id
        return None
