# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.
class Diagnostic:
    id_counter = 1

    def __init__(self, target_id, message):
        self.DiagnosticId = "AZ_PY_{}".format(Diagnostic.id_counter)
        Diagnostic.id_counter += 1
        self.Text = message
        self.HelpLinkUri = ""
        self.TargetId = target_id

    def set_text(self, text):
        self.Text = text

    def set_helplink(self, helplink):
        self.HelpLinkUri = helplink
