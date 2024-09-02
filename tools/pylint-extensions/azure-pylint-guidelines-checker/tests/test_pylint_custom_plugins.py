# ------------------------------------
# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.
# ------------------------------------

import astroid
import pylint.testutils
import pytest
import requests
import os

from azure.core import PipelineClient
from azure.core.configuration import Configuration
import pylint_guidelines_checker as checker

TEST_FOLDER = os.path.abspath(os.path.join(__file__, ".."))


class TestClientMethodsHaveTracingDecorators(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientMethodsHaveTracingDecorators

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_methods_have_tracing_decorators.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_constructor(self, setup):
        function_node = setup.body[3].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_private_method(self, setup):
        function_node = setup.body[3].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_private_method_async(self, setup):
        function_node = setup.body[3].body[2]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)

    def test_ignores_methods_with_decorators(self, setup):
        func_node_a = setup.body[3].body[3]
        func_node_b = setup.body[3].body[4]
        func_node_c = setup.body[3].body[5]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)
            self.checker.visit_functiondef(func_node_c)

    def test_ignores_async_methods_with_decorators(self, setup):
        func_node_a = setup.body[3].body[6]
        func_node_b = setup.body[3].body[7]
        func_node_c = setup.body[3].body[8]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)
            self.checker.visit_asyncfunctiondef(func_node_c)

    def test_finds_sync_decorator_on_async_method(self, setup):
        func_node_a = setup.body[3].body[9]
        func_node_b = setup.body[3].body[10]
        func_node_c = setup.body[3].body[11]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-tracing-decorator-async",
                line=47,
                node=func_node_a,
                col_offset=4,
                end_line=47,
                end_col_offset=34,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-tracing-decorator-async",
                line=51,
                node=func_node_b,
                col_offset=4,
                end_line=51,
                end_col_offset=23,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-tracing-decorator-async",
                line=55,
                node=func_node_c,
                col_offset=4,
                end_line=55,
                end_col_offset=24,
            ),
        ):
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)
            self.checker.visit_asyncfunctiondef(func_node_c)

    def test_finds_async_decorator_on_sync_method(self, setup):
        func_node_a = setup.body[3].body[12]
        func_node_b = setup.body[3].body[13]
        func_node_c = setup.body[3].body[14]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-tracing-decorator",
                line=60,
                node=func_node_a,
                col_offset=4,
                end_line=60,
                end_col_offset=28,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-tracing-decorator",
                line=64,
                node=func_node_b,
                col_offset=4,
                end_line=64,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-tracing-decorator",
                line=68,
                node=func_node_c,
                col_offset=4,
                end_line=68,
                end_col_offset=18,
            ),
        ):
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)
            self.checker.visit_functiondef(func_node_c)

    def test_ignores_other_decorators(self, setup):
        func_node_a = setup.body[3].body[15]
        func_node_b = setup.body[3].body[16]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)

    def test_ignores_other_decorators_async(self, setup):
        func_node_a = setup.body[3].body[17]
        func_node_b = setup.body[3].body[18]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)

    def test_ignores_non_client_method(self, setup):
        func_node_a = setup.body[4].body[0]
        func_node_b = setup.body[4].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_implementation.html#distributed-tracing"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientsDoNotUseStaticMethods(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientsDoNotUseStaticMethods

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "clients_do_not_use_static_methods.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_constructor(self, setup):
        function_node = setup.body[3].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_private_method(self, setup):
        function_node = setup.body[3].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_private_method_async(self, setup):
        function_node = setup.body[3].body[2]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)

    def test_ignores_methods_with_other_decorators(self, setup):
        func_node_a = setup.body[3].body[3]
        func_node_b = setup.body[3].body[4]
        func_node_c = setup.body[3].body[5]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)
            self.checker.visit_functiondef(func_node_c)

    def test_ignores_async_methods_with_other_decorators(self, setup):
        func_node_a = setup.body[3].body[6]
        func_node_b = setup.body[3].body[7]
        func_node_c = setup.body[3].body[8]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)
            self.checker.visit_asyncfunctiondef(func_node_c)

    def test_finds_staticmethod_on_async_method(self, setup):
        func_node_a = setup.body[3].body[9]
        func_node_b = setup.body[3].body[10]
        func_node_c = setup.body[3].body[11]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-should-not-use-static-method",
                line=49,
                node=func_node_a,
                col_offset=4,
                end_line=49,
                end_col_offset=35,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-should-not-use-static-method",
                line=53,
                node=func_node_b,
                col_offset=4,
                end_line=53,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-should-not-use-static-method",
                line=57,
                node=func_node_c,
                col_offset=4,
                end_line=57,
                end_col_offset=25,
            ),
        ):
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)
            self.checker.visit_asyncfunctiondef(func_node_c)

    def test_finds_staticmethod_on_sync_method(self, setup):
        func_node_a = setup.body[3].body[12]
        func_node_b = setup.body[3].body[13]
        func_node_c = setup.body[3].body[14]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-should-not-use-static-method",
                line=62,
                node=func_node_a,
                col_offset=4,
                end_line=62,
                end_col_offset=29,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-should-not-use-static-method",
                line=66,
                node=func_node_b,
                col_offset=4,
                end_line=66,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-should-not-use-static-method",
                line=70,
                node=func_node_c,
                col_offset=4,
                end_line=70,
                end_col_offset=19,
            ),
        ):
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)
            self.checker.visit_functiondef(func_node_c)

    def test_ignores_other_multiple_decorators(self, setup):
        func_node_a = setup.body[3].body[15]
        func_node_b = setup.body[3].body[16]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)

    def test_ignores_other_multiple_decorators_async(self, setup):
        func_node_a = setup.body[3].body[17]
        func_node_b = setup.body[3].body[18]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)

    def test_ignores_non_client_method(self, setup):
        func_node_a = setup.body[4].body[0]
        func_node_b = setup.body[4].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_implementation.html#method-signatures"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientHasApprovedMethodNamePrefix(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientHasApprovedMethodNamePrefix

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_has_approved_method_name_prefix.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_constructor(self, setup):
        class_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_private_method(self, setup):
        class_node = setup.body[2]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_if_exists_suffix(self, setup):
        class_node = setup.body[3]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_from_prefix(self, setup):
        class_node = setup.body[4]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_approved_prefix_names(self, setup):
        class_node = setup.body[5]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_non_client_with_unapproved_prefix_names(self, setup):
        class_node = setup.body[6]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_nested_function_with_unapproved_prefix_names(self, setup):
        class_node = setup.body[7]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_finds_unapproved_prefix_names(self, setup):
        class_node = setup.body[8]
        func_node_a = setup.body[8].body[0]
        func_node_b = setup.body[8].body[1]
        func_node_c = setup.body[8].body[2]
        func_node_d = setup.body[8].body[3]
        func_node_e = setup.body[8].body[4]
        func_node_f = setup.body[8].body[5]
        func_node_g = setup.body[8].body[6]
        func_node_h = setup.body[8].body[7]
        func_node_i = setup.body[8].body[8]
        func_node_j = setup.body[8].body[9]
        func_node_k = setup.body[8].body[10]
        func_node_l = setup.body[8].body[11]
        func_node_m = setup.body[8].body[12]
        func_node_n = setup.body[8].body[13]
        func_node_o = setup.body[8].body[14]
        func_node_p = setup.body[8].body[15]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=83,
                node=func_node_a,
                col_offset=4,
                end_line=83,
                end_col_offset=27,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=86,
                node=func_node_b,
                col_offset=4,
                end_line=86,
                end_col_offset=22,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=89,
                node=func_node_c,
                col_offset=4,
                end_line=89,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=92,
                node=func_node_d,
                col_offset=4,
                end_line=92,
                end_col_offset=20,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=95,
                node=func_node_e,
                col_offset=4,
                end_line=95,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=98,
                node=func_node_f,
                col_offset=4,
                end_line=98,
                end_col_offset=29,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=101,
                node=func_node_g,
                col_offset=4,
                end_line=101,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=104,
                node=func_node_h,
                col_offset=4,
                end_line=104,
                end_col_offset=19,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=107,
                node=func_node_i,
                col_offset=4,
                end_line=107,
                end_col_offset=21,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=110,
                node=func_node_j,
                col_offset=4,
                end_line=110,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=113,
                node=func_node_k,
                col_offset=4,
                end_line=113,
                end_col_offset=21,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=116,
                node=func_node_l,
                col_offset=4,
                end_line=116,
                end_col_offset=22,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=119,
                node=func_node_m,
                col_offset=4,
                end_line=119,
                end_col_offset=21,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=122,
                node=func_node_n,
                col_offset=4,
                end_line=122,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=125,
                node=func_node_o,
                col_offset=4,
                end_line=125,
                end_col_offset=21,
            ),
            pylint.testutils.MessageTest(
                msg_id="unapproved-client-method-name-prefix",
                line=128,
                node=func_node_p,
                col_offset=4,
                end_line=128,
                end_col_offset=21,
            ),
        ):
            self.checker.visit_classdef(class_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#service-operations"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientConstructorTakesCorrectParameters(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientConstructorTakesCorrectParameters

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_constructor_takes_correct_parameters.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_finds_correct_params(self, setup):
        function_node = setup.body[0].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_non_constructor_methods(self, setup):
        function_node = setup.body[0].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_non_client_constructor_methods(self, setup):
        function_node = setup.body[1].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_finds_constructor_without_kwargs(self, setup):
        function_node = setup.body[2].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="missing-client-constructor-parameter-kwargs",
                line=19,
                node=function_node,
                col_offset=4,
                end_line=19,
                end_col_offset=16,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_finds_constructor_without_credentials(self, setup):
        function_node = setup.body[3].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="missing-client-constructor-parameter-credential",
                line=25,
                node=function_node,
                col_offset=4,
                end_line=25,
                end_col_offset=16,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_finds_constructor_with_no_params(self, setup):
        function_node = setup.body[4].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="missing-client-constructor-parameter-credential",
                line=31,
                node=function_node,
                col_offset=4,
                end_line=31,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="missing-client-constructor-parameter-kwargs",
                line=31,
                node=function_node,
                col_offset=4,
                end_line=31,
                end_col_offset=16,
            ),
        ):
            self.checker.visit_functiondef(function_node)

    def test_guidelines_link_active(self):
        url = (
            "https://azure.github.io/azure-sdk/python_design.html#client-configuration"
        )
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientMethodsUseKwargsWithMultipleParameters(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientMethodsUseKwargsWithMultipleParameters

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_methods_use_kwargs_with_multiple_parameters.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_method_abiding_to_guidelines(self, setup):
        function_node = setup.body[2].body[0]
        function_node_a = setup.body[2].body[1]
        function_node_b = setup.body[2].body[2]
        function_node_c = setup.body[2].body[3]
        function_node_d = setup.body[2].body[4]
        function_node_e = setup.body[2].body[5]
        function_node_f = setup.body[2].body[6]
        function_node_g = setup.body[2].body[7]
        function_node_h = setup.body[2].body[8]
        function_node_i = setup.body[2].body[9]
        function_node_j = setup.body[2].body[10]
        function_node_k = setup.body[2].body[11]
        function_node_l = setup.body[2].body[12]
        function_node_m = setup.body[2].body[13]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)
            self.checker.visit_functiondef(function_node_c)
            self.checker.visit_functiondef(function_node_d)
            self.checker.visit_functiondef(function_node_e)
            self.checker.visit_functiondef(function_node_f)
            self.checker.visit_functiondef(function_node_g)
            self.checker.visit_functiondef(function_node_h)
            self.checker.visit_functiondef(function_node_i)
            self.checker.visit_functiondef(function_node_j)
            self.checker.visit_functiondef(function_node_k)
            self.checker.visit_functiondef(function_node_l)
            self.checker.visit_functiondef(function_node_m)

    def test_ignores_method_abiding_to_guidelines_async(self, setup):
        function_node = setup.body[3].body[0]
        function_node_a = setup.body[3].body[1]
        function_node_b = setup.body[3].body[2]
        function_node_c = setup.body[3].body[3]
        function_node_d = setup.body[3].body[4]
        function_node_e = setup.body[3].body[5]
        function_node_f = setup.body[3].body[6]
        function_node_g = setup.body[3].body[7]
        function_node_h = setup.body[3].body[8]
        function_node_i = setup.body[3].body[9]
        function_node_j = setup.body[3].body[10]
        function_node_k = setup.body[3].body[11]
        function_node_l = setup.body[3].body[12]
        function_node_m = setup.body[3].body[13]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)
            self.checker.visit_asyncfunctiondef(function_node_c)
            self.checker.visit_asyncfunctiondef(function_node_d)
            self.checker.visit_asyncfunctiondef(function_node_e)
            self.checker.visit_asyncfunctiondef(function_node_f)
            self.checker.visit_asyncfunctiondef(function_node_g)
            self.checker.visit_asyncfunctiondef(function_node_h)
            self.checker.visit_asyncfunctiondef(function_node_i)
            self.checker.visit_asyncfunctiondef(function_node_j)
            self.checker.visit_asyncfunctiondef(function_node_k)
            self.checker.visit_asyncfunctiondef(function_node_l)
            self.checker.visit_asyncfunctiondef(function_node_m)

    def test_finds_methods_with_too_many_positional_args(self, setup):
        function_node = setup.body[4].body[0]
        function_node_a = setup.body[4].body[1]
        function_node_b = setup.body[4].body[2]
        function_node_c = setup.body[4].body[3]
        function_node_d = setup.body[4].body[4]
        function_node_e = setup.body[4].body[5]
        function_node_f = setup.body[4].body[6]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=100,
                node=function_node,
                col_offset=4,
                end_line=100,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=103,
                node=function_node_a,
                col_offset=4,
                end_line=103,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=106,
                node=function_node_b,
                col_offset=4,
                end_line=106,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=109,
                node=function_node_c,
                col_offset=4,
                end_line=109,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=112,
                node=function_node_d,
                col_offset=4,
                end_line=112,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=115,
                node=function_node_e,
                col_offset=4,
                end_line=115,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=118,
                node=function_node_f,
                col_offset=4,
                end_line=118,
                end_col_offset=18,
            ),
        ):
            self.checker.visit_functiondef(function_node)
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)
            self.checker.visit_functiondef(function_node_c)
            self.checker.visit_functiondef(function_node_d)
            self.checker.visit_functiondef(function_node_e)
            self.checker.visit_functiondef(function_node_f)

    def test_finds_methods_with_too_many_positional_args_async(self, setup):
        function_node = setup.body[5].body[0]
        function_node_a = setup.body[5].body[1]
        function_node_b = setup.body[5].body[2]
        function_node_c = setup.body[5].body[3]
        function_node_d = setup.body[5].body[4]
        function_node_e = setup.body[5].body[5]
        function_node_f = setup.body[5].body[6]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=125,
                node=function_node,
                col_offset=4,
                end_line=125,
                end_col_offset=22,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=128,
                node=function_node_a,
                col_offset=4,
                end_line=128,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=131,
                node=function_node_b,
                col_offset=4,
                end_line=131,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=134,
                node=function_node_c,
                col_offset=4,
                end_line=134,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=137,
                node=function_node_d,
                col_offset=4,
                end_line=137,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=140,
                node=function_node_e,
                col_offset=4,
                end_line=140,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-has-more-than-5-positional-arguments",
                line=143,
                node=function_node_f,
                col_offset=4,
                end_line=143,
                end_col_offset=24,
            ),
        ):
            self.checker.visit_asyncfunctiondef(function_node)
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)
            self.checker.visit_asyncfunctiondef(function_node_c)
            self.checker.visit_asyncfunctiondef(function_node_d)
            self.checker.visit_asyncfunctiondef(function_node_e)
            self.checker.visit_asyncfunctiondef(function_node_f)

    def test_ignores_non_client_methods(self, setup):
        function_node_a = setup.body[6].body[0]
        function_node_b = setup.body[6].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_implementation.html#method-signatures"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientMethodsHaveTypeAnnotations(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientMethodsHaveTypeAnnotations

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_methods_have_type_annotations.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_correct_type_annotations(self, setup):
        function_node_a = setup.body[1].body[0]
        function_node_b = setup.body[1].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_ignores_correct_type_comments(self, setup):
        function_node_a = setup.body[1].body[2]
        function_node_b = setup.body[1].body[3]
        function_node_c = setup.body[1].body[4]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)
            self.checker.visit_functiondef(function_node_c)

    def test_ignores_correct_type_comments_async(self, setup):
        function_node_a = setup.body[1].body[5]
        function_node_b = setup.body[1].body[6]
        function_node_c = setup.body[1].body[7]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)
            self.checker.visit_asyncfunctiondef(function_node_c)

    def test_ignores_no_parameter_method_with_annotations(self, setup):
        function_node_a = setup.body[1].body[8]
        function_node_b = setup.body[1].body[9]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_ignores_no_parameter_method_with_annotations_async(self, setup):
        function_node_a = setup.body[1].body[10]
        function_node_b = setup.body[1].body[11]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_finds_no_parameter_method_without_annotations(self, setup):
        function_node_a = setup.body[2].body[0]
        function_node_b = setup.body[2].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=67,
                node=function_node_a,
                col_offset=4,
                end_line=67,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=70,
                node=function_node_b,
                col_offset=4,
                end_line=70,
                end_col_offset=22,
            ),
        ):
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_finds_method_missing_annotations(self, setup):
        function_node = setup.body[3].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=76,
                node=function_node,
                col_offset=4,
                end_line=76,
                end_col_offset=16,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_finds_method_missing_annotations_async(self, setup):
        function_node = setup.body[4].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=82,
                node=function_node,
                col_offset=4,
                end_line=82,
                end_col_offset=22,
            )
        ):
            self.checker.visit_asyncfunctiondef(function_node)

    def test_finds_constructor_without_annotations(self, setup):
        function_node = setup.body[5].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=88,
                node=function_node,
                col_offset=4,
                end_line=88,
                end_col_offset=16,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_finds_missing_return_annotation_but_has_type_hints(self, setup):
        function_node_a = setup.body[6].body[0]
        function_node_b = setup.body[6].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=94,
                node=function_node_a,
                col_offset=4,
                end_line=94,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=97,
                node=function_node_b,
                col_offset=4,
                end_line=97,
                end_col_offset=18,
            ),
        ):
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_finds_missing_return_annotation_but_has_type_hints_async(self, setup):
        function_node_a = setup.body[7].body[0]
        function_node_b = setup.body[7].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=104,
                node=function_node_a,
                col_offset=4,
                end_line=104,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=107,
                node=function_node_b,
                col_offset=4,
                end_line=107,
                end_col_offset=24,
            ),
        ):
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_finds_missing_annotations_but_has_return_hint(self, setup):
        function_node_a = setup.body[8].body[0]
        function_node_b = setup.body[8].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=114,
                node=function_node_a,
                col_offset=4,
                end_line=114,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=117,
                node=function_node_b,
                col_offset=4,
                end_line=117,
                end_col_offset=18,
            ),
        ):
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_finds_missing_annotations_but_has_return_hint_async(self, setup):
        function_node_a = setup.body[9].body[0]
        function_node_b = setup.body[9].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=124,
                node=function_node_a,
                col_offset=4,
                end_line=124,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-type-annotations",
                line=127,
                node=function_node_b,
                col_offset=4,
                end_line=127,
                end_col_offset=24,
            ),
        ):
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_ignores_non_client_methods(self, setup):
        function_node = setup.body[10].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_private_methods(self, setup):
        function_node = setup.body[10].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_guidelines_link_active(self):
        url = (
            "https://azure.github.io/azure-sdk/python_implementation.html#types-or-not"
        )
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientHasKwargsInPoliciesForCreateConfigurationMethod(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientHasKwargsInPoliciesForCreateConfigurationMethod

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_has_kwargs_in_policies_for_create_config_method.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_config_policies_with_kwargs(self, setup):
        function_node_a = setup.body[4].body[0]
        function_node_b = setup.body[4].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_finds_config_policies_without_kwargs(self, setup):
        function_node_a = setup.body[5].body[0]
        policy_a = setup.body[5].body[0].body[2]
        policy_b = setup.body[5].body[0].body[5]
        policy_c = setup.body[5].body[0].body[6]
        function_node_b = setup.body[5].body[1]
        policy_d = setup.body[5].body[1].body[2]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="config-missing-kwargs-in-policy",
                line=35,
                node=policy_a,
                col_offset=8,
                end_line=35,
                end_col_offset=59,
            ),
            pylint.testutils.MessageTest(
                msg_id="config-missing-kwargs-in-policy",
                line=38,
                node=policy_b,
                col_offset=8,
                end_line=38,
                end_col_offset=54,
            ),
            pylint.testutils.MessageTest(
                msg_id="config-missing-kwargs-in-policy",
                line=39,
                node=policy_c,
                col_offset=8,
                end_line=39,
                end_col_offset=43,
            ),
            pylint.testutils.MessageTest(
                msg_id="config-missing-kwargs-in-policy",
                line=48,
                node=policy_d,
                col_offset=8,
                end_line=48,
                end_col_offset=70,
            ),
        ):
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_ignores_policies_outside_create_config(self, setup):
        function_node_a = setup.body[6].body[0]
        function_node_b = setup.body[6].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_guidelines_link_active(self):
        url = (
            "https://azure.github.io/azure-sdk/python_design.html#client-configuration"
        )
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientUsesCorrectNamingConventions(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientUsesCorrectNamingConventions

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_uses_correct_naming_conventions.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_constructor(self, setup):
        class_node = setup.body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_internal_client(self, setup):
        class_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_private_method(self, setup):
        class_node = setup.body[2]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_correct_client(self, setup):
        class_node = setup.body[3]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_non_client(self, setup):
        class_node = setup.body[4]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_correct_method_names(self, setup):
        class_node = setup.body[5]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_correct_method_names_async(self, setup):
        class_node = setup.body[6]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_correct_class_constant(self, setup):
        class_node = setup.body[7]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_finds_incorrectly_named_client(self, setup):
        class_node_a = setup.body[8]
        class_node_b = setup.body[9]
        class_node_c = setup.body[10]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=64,
                node=class_node_a,
                col_offset=0,
                end_line=64,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=68,
                node=class_node_b,
                col_offset=0,
                end_line=68,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=72,
                node=class_node_c,
                col_offset=0,
                end_line=72,
                end_col_offset=16,
            ),
        ):
            self.checker.visit_classdef(class_node_a)
            self.checker.visit_classdef(class_node_b)
            self.checker.visit_classdef(class_node_c)

    def test_finds_incorrectly_named_methods(self, setup):
        class_node = setup.body[11]
        func_node_a = setup.body[11].body[0]
        func_node_b = setup.body[11].body[1]
        func_node_c = setup.body[11].body[2]
        func_node_d = setup.body[11].body[3]
        func_node_e = setup.body[11].body[4]
        func_node_f = setup.body[11].body[5]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=78,
                node=func_node_a,
                col_offset=4,
                end_line=78,
                end_col_offset=21,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=81,
                node=func_node_b,
                col_offset=4,
                end_line=81,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=84,
                node=func_node_c,
                col_offset=4,
                end_line=84,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=87,
                node=func_node_d,
                col_offset=4,
                end_line=87,
                end_col_offset=19,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=90,
                node=func_node_e,
                col_offset=4,
                end_line=90,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=93,
                node=func_node_f,
                col_offset=4,
                end_line=93,
                end_col_offset=19,
            ),
        ):
            self.checker.visit_classdef(class_node)

    def test_finds_incorrectly_named_methods_async(self, setup):
        class_node = setup.body[12]
        func_node_a = setup.body[12].body[0]
        func_node_b = setup.body[12].body[1]
        func_node_c = setup.body[12].body[2]
        func_node_d = setup.body[12].body[3]
        func_node_e = setup.body[12].body[4]
        func_node_f = setup.body[12].body[5]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=99,
                node=func_node_a,
                col_offset=4,
                end_line=99,
                end_col_offset=27,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=102,
                node=func_node_b,
                col_offset=4,
                end_line=102,
                end_col_offset=22,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=105,
                node=func_node_c,
                col_offset=4,
                end_line=105,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=108,
                node=func_node_d,
                col_offset=4,
                end_line=108,
                end_col_offset=25,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=111,
                node=func_node_e,
                col_offset=4,
                end_line=111,
                end_col_offset=23,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=114,
                node=func_node_f,
                col_offset=4,
                end_line=114,
                end_col_offset=25,
            ),
        ):
            self.checker.visit_classdef(class_node)

    def test_finds_incorrectly_named_class_constant(self, setup):
        class_node = setup.body[13]
        const_a = setup.body[13].body[0]
        const_b = setup.body[13].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=120,
                node=const_a,
                col_offset=4,
                end_line=120,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-incorrect-naming-convention",
                line=121,
                node=const_b,
                col_offset=4,
                end_line=121,
                end_col_offset=16,
            ),
        ):
            self.checker.visit_classdef(class_node)

    def test_ignores_docstrings(self, setup):
        class_node = setup.body[14]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_implementation.html#naming-conventions"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientMethodsHaveKwargsParameter(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientMethodsHaveKwargsParameter

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_methods_have_kwargs_param.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_private_methods(self, setup):
        function_node = setup.body[2].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_properties(self, setup):
        function_node = setup.body[3].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_properties_async(self, setup):
        function_node = setup.body[4].body[0]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)

    def test_ignores_non_client_methods(self, setup):
        function_node = setup.body[5].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_methods_with_kwargs(self, setup):
        function_node_a = setup.body[6].body[0]
        function_node_b = setup.body[6].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_finds_missing_kwargs(self, setup):
        function_node_a = setup.body[7].body[0]
        function_node_b = setup.body[7].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-kwargs",
                line=44,
                node=function_node_a,
                col_offset=4,
                end_line=44,
                end_col_offset=17,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-kwargs",
                line=48,
                node=function_node_b,
                col_offset=4,
                end_line=48,
                end_col_offset=20,
            ),
        ):
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_ignores_methods_with_kwargs_async(self, setup):
        function_node_a = setup.body[8].body[0]
        function_node_b = setup.body[8].body[1]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_finds_missing_kwargs_async(self, setup):
        function_node_a = setup.body[9].body[0]
        function_node_b = setup.body[9].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-kwargs",
                line=64,
                node=function_node_a,
                col_offset=4,
                end_line=64,
                end_col_offset=23,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-missing-kwargs",
                line=68,
                node=function_node_b,
                col_offset=4,
                end_line=68,
                end_col_offset=26,
            ),
        ):
            self.checker.visit_asyncfunctiondef(function_node_a)
            self.checker.visit_asyncfunctiondef(function_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#constructors-and-factory-methods"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestAsyncClientCorrectNaming(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.AsyncClientCorrectNaming

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "async_client_correct_naming.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_private_client(self, setup):
        class_node = setup.body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_correct_client(self, setup):
        class_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_async_base_named_client(self, setup):
        class_node_a = setup.body[2]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node_a)

    def test_finds_incorrectly_named_client(self, setup):
        class_node_a = setup.body[3]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="async-client-bad-name",
                line=20,
                node=class_node_a,
                col_offset=0,
                end_line=20,
                end_col_offset=21,
            ),
        ):
            self.checker.visit_classdef(class_node_a)

    def test_ignores_non_client(self, setup):
        class_node = setup.body[4]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#async-support"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestFileHasCopyrightHeader(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.FileHasCopyrightHeader

    def test_copyright_header_acceptable(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "copyright_header_acceptable.py")
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertNoMessages():
            self.checker.visit_module(node)

    def test_copyright_header_violation(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "copyright_header_violation.py")
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="file-needs-copyright-header", line=0, node=node, col_offset=0
            )
        ):
            self.checker.visit_module(node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/policies_opensource.html"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestSpecifyParameterNamesInCall(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.SpecifyParameterNamesInCall

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "specify_parameter_names_in_call.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_call_with_only_two_unnamed_params(self, setup):
        call_node = setup.body[0].body[0].body[0].value
        with self.assertNoMessages():
            self.checker.visit_call(call_node)

    def test_ignores_call_with_two_unnamed_params_and_one_named(self, setup):
        call_node = setup.body[0].body[1].body[0].value
        with self.assertNoMessages():
            self.checker.visit_call(call_node)

    def test_ignores_call_from_non_client(self, setup):
        call_node = setup.body[1].body[0].body[0].value
        with self.assertNoMessages():
            self.checker.visit_call(call_node)

    def test_ignores_call_with_named_params(self, setup):
        call_node_a = setup.body[2].body[0].body[0].value
        call_node_b = setup.body[2].body[1].body[0].value
        call_node_c = setup.body[2].body[2].body[0].value
        with self.assertNoMessages():
            self.checker.visit_call(call_node_a)
            self.checker.visit_call(call_node_b)
            self.checker.visit_call(call_node_c)

    def test_ignores_non_client_function_call(self, setup):
        call_node = setup.body[3].body[0].body[0].value
        with self.assertNoMessages():
            self.checker.visit_call(call_node)

    def test_finds_call_with_more_than_two_unnamed_params(self, setup):
        call_node = setup.body[4].body[0].body[0].value
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="specify-parameter-names-in-call",
                line=38,
                node=call_node,
                col_offset=8,
                end_line=38,
                end_col_offset=43,
            ),
        ):
            self.checker.visit_call(call_node)

    def test_finds_call_with_more_than_two_unnamed_params_and_some_named(self, setup):
        call_node = setup.body[5].body[0].body[0].value
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="specify-parameter-names-in-call",
                line=44,
                node=call_node,
                col_offset=8,
                end_line=44,
                end_col_offset=59,
            ),
        ):
            self.checker.visit_call(call_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_implementation.html#python-codestyle-positional-params"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientListMethodsUseCorePaging(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientListMethodsUseCorePaging

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_list_methods_use_core_paging.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_private_methods(self, setup):
        function_node = setup.body[5].body[0]
        with self.assertNoMessages():
            self.checker.visit_return(function_node.body[0])

    def test_ignores_non_client_methods(self, setup):
        function_node = setup.body[6].body[0]
        with self.assertNoMessages():
            self.checker.visit_return(function_node.body[0])

    def test_ignores_methods_return_ItemPaged(self, setup):
        function_node_a = setup.body[7].body[0]
        function_node_b = setup.body[7].body[1]
        with self.assertNoMessages():
            self.checker.visit_return(function_node_a.body[0])
            self.checker.visit_return(function_node_b.body[0])

    def test_ignores_methods_return_AsyncItemPaged(self, setup):
        function_node_a = setup.body[8].body[0]
        function_node_b = setup.body[8].body[1]
        with self.assertNoMessages():
            self.checker.visit_return(function_node_a.body[0])
            self.checker.visit_return(function_node_b.body[0])

    def test_finds_method_returning_something_else(self, setup):
        function_node_a = setup.body[9].body[0]
        function_node_b = setup.body[9].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=47,
                node=function_node_a,
                col_offset=4,
                end_line=47,
                end_col_offset=18,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=50,
                node=function_node_b,
                col_offset=4,
                end_line=50,
                end_col_offset=19,
            ),
        ):
            self.checker.visit_return(function_node_a.body[0])
            self.checker.visit_return(function_node_b.body[0])

    def test_finds_method_returning_something_else_async(self, setup):
        function_node_a = setup.body[10].body[0]
        function_node_b = setup.body[10].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=56,
                node=function_node_a,
                col_offset=4,
                end_line=56,
                end_col_offset=24,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=59,
                node=function_node_b,
                col_offset=4,
                end_line=59,
                end_col_offset=25,
            ),
        ):
            self.checker.visit_return(function_node_a.body[0])
            self.checker.visit_return(function_node_b.body[0])

    def test_finds_return_ItemPaged_not_list(self, setup):
        function_node_a = setup.body[11].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=65,
                node=function_node_a,
                col_offset=4,
                end_line=65,
                end_col_offset=18,
            ),
        ):
            self.checker.visit_return(function_node_a.body[0])

    def test_finds_return_AsyncItemPaged_not_list(self, setup):
        function_node_a = setup.body[12].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=71,
                node=function_node_a,
                col_offset=4,
                end_line=71,
                end_col_offset=24,
            ),
        ):
            self.checker.visit_return(function_node_a.body[0])

    def test_core_paging_file_custom_class_acceptable_and_violation(self):
        file = open(
            os.path.join(
                TEST_FOLDER, "test_files", "core_paging_acceptable_and_violation.py"
            )
        )
        node = astroid.parse(file.read())
        file.close()

        function_node = node.body[3].body[0]
        function_node_a = node.body[3].body[1]
        function_node_b = node.body[3].body[2]

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=31,
                node=function_node_b,
                col_offset=4,
                end_line=31,
                end_col_offset=22,
            )
        ):
            self.checker.visit_return(function_node.body[2])
            self.checker.visit_return(function_node_a.body[0])
            self.checker.visit_return(function_node_b.body[0])

    def test_core_paging_file_custom_class_violation(self):
        file = open(os.path.join(TEST_FOLDER, "test_files", "core_paging_violation.py"))
        node = astroid.parse(file.read())
        file.close()

        function_node = node.body[2].body[0]

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-paging-methods-use-list",
                line=10,
                node=function_node,
                col_offset=4,
                end_line=10,
                end_col_offset=18,
            )
        ):
            self.checker.visit_return(function_node.body[0])

    def test_core_paging_file_custom_class_acceptable(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "core_paging_acceptable.py")
        )
        node = astroid.parse(file.read())
        file.close()

        function_node = node.body[2].body[0]

        with self.assertNoMessages():
            self.checker.visit_return(function_node.body[0])

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#response-formats"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientLROMethodsUseCorePolling(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientLROMethodsUseCorePolling

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_LRO_methods_use_core_polling.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_private_methods(self, setup):
        function_node = setup.body[2].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_non_client_methods(self, setup):
        function_node = setup.body[3].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_methods_return_LROPoller(self, setup):
        function_node_a = setup.body[4].body[0]
        function_node_b = setup.body[4].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_finds_method_returning_something_else(self, setup):
        function_node_a = setup.body[5].body[0]
        function_node_b = setup.body[5].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-lro-methods-use-polling",
                line=29,
                node=function_node_a,
                col_offset=4,
                end_line=29,
                end_col_offset=19,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-lro-methods-use-polling",
                line=32,
                node=function_node_b,
                col_offset=4,
                end_line=32,
                end_col_offset=20,
            ),
        ):
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#response-formats"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientLROMethodsUseCorrectNaming(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientLROMethodsUseCorrectNaming

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_LRO_methods_use_correct_naming.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_private_methods(self, setup):
        class_node = setup.body[2]
        return_node = setup.body[2].body[0].body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
            self.checker.visit_return(return_node)

    def test_ignores_non_client_methods(self, setup):
        class_node = setup.body[3]
        return_node = setup.body[3].body[0].body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
            self.checker.visit_return(return_node)

    def test_ignores_methods_return_LROPoller_and_correctly_named(self, setup):
        class_node = setup.body[4]
        return_node_a = setup.body[4].body[0].body[0]
        return_node_b = setup.body[4].body[1].body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
            self.checker.visit_return(return_node_a)
            self.checker.visit_return(return_node_b)

    def test_finds_incorrectly_named_method_returning_LROPoller(self, setup):
        class_node = setup.body[5]
        function_node_a = setup.body[5].body[0]
        return_node_a = setup.body[5].body[0].body[0]
        function_node_b = setup.body[5].body[1]
        return_node_b = setup.body[5].body[1].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="lro-methods-use-correct-naming",
                line=28,
                node=function_node_a,
                col_offset=4,
                end_line=28,
                end_col_offset=20,
            ),
            pylint.testutils.MessageTest(
                msg_id="lro-methods-use-correct-naming",
                line=32,
                node=function_node_b,
                col_offset=4,
                end_line=32,
                end_col_offset=20,
            ),
        ):
            self.checker.visit_classdef(class_node)
            self.checker.visit_return(return_node_a)
            self.checker.visit_return(return_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#service-operations"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientConstructorDoesNotHaveConnectionStringParam(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientConstructorDoesNotHaveConnectionStringParam

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_constructor_does_not_have_connection_string_param.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_client_with_no_conn_str_in_constructor(self, setup):
        class_node = setup.body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_non_client_methods(self, setup):
        class_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_finds_client_method_using_conn_str_in_constructor_a(self, setup):
        class_node = setup.body[2]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="connection-string-should-not-be-constructor-param",
                line=14,
                node=class_node,
                col_offset=0,
                end_line=14,
                end_col_offset=17,
            ),
        ):
            self.checker.visit_classdef(class_node)

    def test_finds_client_method_using_conn_str_in_constructor_b(self, setup):
        class_node = setup.body[3]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="connection-string-should-not-be-constructor-param",
                line=20,
                node=class_node,
                col_offset=0,
                end_line=20,
                end_col_offset=17,
            ),
        ):
            self.checker.visit_classdef(class_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#python-client-connection-string"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestPackageNameDoesNotUseUnderscoreOrPeriod(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.PackageNameDoesNotUseUnderscoreOrPeriod

    def test_package_name_acceptable(self):
        package_name = astroid.extract_node(
            """
        PACKAGE_NAME = "correct-package-name"        
        """
        )
        module_node = astroid.Module(name="node", file="setup.py")
        module_node.doc_node = """ """
        module_node.body = [package_name]

        with self.assertNoMessages():
            self.checker.visit_module(module_node)

    def test_package_name_violation(self):
        package_name = astroid.extract_node(
            """
        PACKAGE_NAME = "incorrect.package-name"        
        """
        )
        module_node = astroid.Module(name="node", file="setup.py")
        module_node.doc_node = """ """
        module_node.body = [package_name]

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="package-name-incorrect", line=0, node=module_node, col_offset=0,
            )
        ):
            self.checker.visit_module(module_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#packaging"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestServiceClientUsesNameWithClientSuffix(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ServiceClientUsesNameWithClientSuffix

    def test_client_suffix_acceptable(self):
        client_node = astroid.extract_node(
            """
        class MyClient():
            def __init__(self):
                pass       
        """
        )
        module_node = astroid.Module(name="node", file="_my_client.py")
        module_node.doc_node = """ """
        module_node.body = [client_node]

        with self.assertNoMessages():
            self.checker.visit_module(module_node)

    def test_client_suffix_violation(self):
        client_node = astroid.extract_node(
            """
        class Violation():
            def __init__(self):
                pass       
        """
        )
        module_node = astroid.Module(name="node", file="_my_client.py")
        module_node.doc_node = """ """
        module_node.body = [client_node]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-suffix-needed", line=0, node=module_node, col_offset=0,
            )
        ):
            self.checker.visit_module(module_node)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#service-client"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestClientMethodNamesDoNotUseDoubleUnderscorePrefix(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.ClientMethodNamesDoNotUseDoubleUnderscorePrefix

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "client_method_names_do_not_use_double_underscore_prefix.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_repr(self, setup):
        function_node = setup.body[2].body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_constructor(self, setup):
        function_node = setup.body[2].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_other_dunder(self, setup):
        function_node_a = setup.body[2].body[2]
        function_node_b = setup.body[2].body[3]
        function_node_c = setup.body[2].body[4]
        function_node_d = setup.body[2].body[5]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node_a)
            self.checker.visit_functiondef(function_node_b)
            self.checker.visit_functiondef(function_node_c)
            self.checker.visit_functiondef(function_node_d)

    def test_ignores_private_method(self, setup):
        function_node = setup.body[2].body[6]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_private_method_async(self, setup):
        function_node = setup.body[2].body[7]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)

    def test_ignores_methods_with_decorators(self, setup):
        func_node_a = setup.body[2].body[8]
        func_node_b = setup.body[2].body[9]
        func_node_c = setup.body[2].body[10]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)
            self.checker.visit_functiondef(func_node_c)

    def test_ignores_async_methods_with_decorators(self, setup):
        func_node_a = setup.body[2].body[8]
        func_node_b = setup.body[2].body[9]
        func_node_c = setup.body[2].body[10]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)
            self.checker.visit_asyncfunctiondef(func_node_c)

    def test_finds_double_underscore_on_async_method(self, setup):
        func_node_a = setup.body[3].body[0]
        func_node_b = setup.body[3].body[1]
        func_node_c = setup.body[3].body[2]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-name-no-double-underscore",
                line=67,
                node=func_node_a,
                col_offset=4,
                end_line=67,
                end_col_offset=36,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-name-no-double-underscore",
                line=71,
                node=func_node_b,
                col_offset=4,
                end_line=71,
                end_col_offset=25,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-name-no-double-underscore",
                line=75,
                node=func_node_c,
                col_offset=4,
                end_line=75,
                end_col_offset=26,
            ),
        ):
            self.checker.visit_asyncfunctiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)
            self.checker.visit_asyncfunctiondef(func_node_c)

    def test_finds_double_underscore_on_sync_method(self, setup):
        func_node_a = setup.body[4].body[0]
        func_node_b = setup.body[4].body[1]
        func_node_c = setup.body[4].body[2]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-method-name-no-double-underscore",
                line=82,
                node=func_node_a,
                col_offset=4,
                end_line=82,
                end_col_offset=30,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-name-no-double-underscore",
                line=86,
                node=func_node_b,
                col_offset=4,
                end_line=86,
                end_col_offset=19,
            ),
            pylint.testutils.MessageTest(
                msg_id="client-method-name-no-double-underscore",
                line=90,
                node=func_node_c,
                col_offset=4,
                end_line=90,
                end_col_offset=20,
            ),
        ):
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_functiondef(func_node_b)
            self.checker.visit_functiondef(func_node_c)

    def test_ignores_non_client_method(self, setup):
        func_node_a = setup.body[5].body[0]
        func_node_b = setup.body[5].body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(func_node_a)
            self.checker.visit_asyncfunctiondef(func_node_b)

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_implementation.html#public-vs-private"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestCheckDocstringAdmonitionNewline(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.CheckDocstringAdmonitionNewline

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "check_docstring_admonition_newline.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignores_correct_admonition_statement_in_function(self, setup):
        function_node = setup.body[0]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_ignores_correct_admonition_statement_in_function_with_comments(self, setup):
        function_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)

    def test_bad_admonition_statement_in_function(self, setup):
        function_node = setup.body[2]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-admonition-needs-newline",
                line=24,
                node=function_node,
                col_offset=0,
                end_line=24,
                end_col_offset=17,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_bad_admonition_statement_in_function_with_comments(self, setup):
        function_node = setup.body[3]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-admonition-needs-newline",
                line=32,
                node=function_node,
                col_offset=0,
                end_line=32,
                end_col_offset=17,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_ignores_correct_admonition_statement_in_function_async(self, setup):
        function_node = setup.body[4]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)

    def test_ignores_correct_admonition_statement_in_function_with_comments_async(self, setup):
        function_node = setup.body[5]
        with self.assertNoMessages():
            self.checker.visit_asyncfunctiondef(function_node)

    def test_bad_admonition_statement_in_function_async(self, setup):
        function_node = setup.body[6]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-admonition-needs-newline",
                line=64,
                node=function_node,
                col_offset=0,
                end_line=64,
                end_col_offset=23,
            )
        ):
            self.checker.visit_asyncfunctiondef(function_node)

    def test_bad_admonition_statement_in_function_with_comments_async(self, setup):
        function_node = setup.body[7]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-admonition-needs-newline",
                line=72,
                node=function_node,
                col_offset=0,
                end_line=72,
                end_col_offset=23,
            )
        ):
            self.checker.visit_asyncfunctiondef(function_node)

    def test_ignores_correct_admonition_statement_in_class(self, setup):
        class_node = setup.body[8]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_ignores_correct_admonition_statement_in_class_with_comments(self, setup):
        class_node = setup.body[9]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_bad_admonition_statement_in_class(self, setup):
        class_node = setup.body[10]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-admonition-needs-newline",
                line=110,
                node=class_node,
                col_offset=0,
                end_line=110,
                end_col_offset=17,
            )
        ):
            self.checker.visit_classdef(class_node)

    def test_bad_admonition_statement_in_class_with_comments(self, setup):
        class_node = setup.body[11]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-admonition-needs-newline",
                line=121,
                node=class_node,
                col_offset=0,
                end_line=121,
                end_col_offset=17,
            )
        ):
            self.checker.visit_classdef(class_node)


class TestCheckNamingMismatchGeneratedCode(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.CheckNamingMismatchGeneratedCode

    def test_import_naming_mismatch_violation(self):
        import_one = astroid.extract_node("import Something")
        import_two = astroid.extract_node("import Something2 as SomethingTwo")
        assign_one = astroid.extract_node(
            """
            __all__ =(
            "Something",
            "SomethingTwo", 
            ) 
          """
        )
        module_node = astroid.Module(name="node", file="__init__.py")
        module_node.doc_node = """ """
        module_node.body = [import_one, import_two, assign_one]

        for name in module_node.body[-1].assigned_stmts():
            err_node = name.elts[1]

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="naming-mismatch",
                line=4,
                node=err_node,
                col_offset=0,
                end_line=4,
                end_col_offset=14,
            )
        ):
            self.checker.visit_module(module_node)

    def test_import_from_naming_mismatch_violation(self):
        import_one = astroid.extract_node("import Something")
        import_two = astroid.extract_node(
            "from Something2 import SomethingToo as SomethingTwo"
        )
        assign_one = astroid.extract_node(
            """
            __all__ =(
            "Something",
            "SomethingTwo", 
            ) 
          """
        )
        module_node = astroid.Module(name="node", file="__init__.py")
        module_node.doc_node = """ """
        module_node.body = [import_one, import_two, assign_one]

        for name in module_node.body[-1].assigned_stmts():
            err_node = name.elts[1]

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="naming-mismatch",
                line=4,
                node=err_node,
                col_offset=0,
                end_line=4,
                end_col_offset=14,
            )
        ):
            self.checker.visit_module(module_node)

    def test_naming_mismatch_acceptable(self):
        import_one = astroid.extract_node("import Something")
        import_two = astroid.extract_node("import Something2 as SomethingTwo")
        assign_one = astroid.extract_node(
            """
            __all__ =(
            "Something",
            "Something2", 
            ) 
          """
        )
        module_node = astroid.Module(name="node", file="__init__.py")
        module_node.doc_node = """ """
        module_node.body = [import_one, import_two, assign_one]

        with self.assertNoMessages():
            self.checker.visit_module(module_node)

    def test_naming_mismatch_pylint_disable(self):
        file = open(os.path.join(TEST_FOLDER, "test_files", "__init__.py"))
        node = astroid.parse(file.read())
        file.close()

        with self.assertNoMessages():
            self.checker.visit_module(node)

    def test_guidelines_link_active(self):
        url = "https://github.com/Azure/autorest/blob/main/docs/generate/built-in-directives.md"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestCheckEnum(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.CheckEnum

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "check_enum.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_ignore_normal_class(self, setup):
        class_node = setup.body[3]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_enum_capitalized_violation_python_two(self, setup):
        class_node = setup.body[4]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="enum-must-be-uppercase",
                line=13,
                node=class_node.body[0].targets[0],
                col_offset=4,
                end_line=13,
                end_col_offset=7,
            )
        ):
            self.checker.visit_classdef(class_node)

    def test_enum_capitalized_violation_python_three(self, setup):
        class_node = setup.body[5]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="enum-must-be-uppercase",
                line=18,
                node=class_node.body[0].targets[0],
                col_offset=4,
                end_line=18,
                end_col_offset=7,
            )
        ):
            self.checker.visit_classdef(class_node)

    def test_inheriting_case_insensitive_violation(self, setup):
        class_node = setup.body[6]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="enum-must-inherit-case-insensitive-enum-meta",
                line=22,
                node=class_node,
                col_offset=0,
                end_line=22,
                end_col_offset=16,
            )
        ):
            self.checker.visit_classdef(class_node)

    def test_acceptable_python_three(self, setup):
        class_node = setup.body[7]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_enum_file_acceptable_python_two(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "enum_checker_acceptable.py")
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertNoMessages():
            self.checker.visit_classdef(node.body[3])

    def test_enum_file_both_violation(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "enum_checker_violation.py")
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="enum-must-inherit-case-insensitive-enum-meta",
                line=5,
                node=node.body[1],
                col_offset=0,
                end_line=5,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="enum-must-be-uppercase",
                line=6,
                node=node.body[1].body[0].targets[0],
                col_offset=4,
                end_line=6,
                end_col_offset=7,
            ),
        ):
            self.checker.visit_classdef(node.body[1])

    def test_guidelines_link_active(self):
        self._create_url_pipeline(
            "https://azure.github.io/azure-sdk/python_design.html#enumerations"
        )
        self._create_url_pipeline(
            "https://azure.github.io/azure-sdk/python_implementation.html#extensible-enumerations"
        )

    def _create_url_pipeline(self, url):
        resp = requests.get(url)
        assert resp.status_code == 200


class TestCheckAPIVersion(pylint.testutils.CheckerTestCase):
    CHECKER_CLASS = checker.CheckAPIVersion

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "check_API_version.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_api_version_violation(self, setup):
        class_node = setup.body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-accepts-api-version-keyword",
                col_offset=0,
                line=2,
                node=class_node,
                end_line=2,
                end_col_offset=16,
            )
        ):
            self.checker.visit_classdef(class_node)

    def test_api_version_acceptable(self, setup):
        class_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)

    def test_api_version_file_class_acceptable(self):
        file = open(
            os.path.join(
                TEST_FOLDER, "test_files", "api_version_checker_acceptable_class.py"
            )
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertNoMessages():
            self.checker.visit_classdef(node.body[0])

    def test_api_version_file_init_acceptable(self):
        file = open(
            os.path.join(
                TEST_FOLDER, "test_files", "api_version_checker_acceptable_init.py"
            )
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertNoMessages():
            self.checker.visit_classdef(node.body[0])

    def test_api_version_file_violation(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "api_version_checker_violation.py")
        )
        node = astroid.parse(file.read())
        file.close()

        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="client-accepts-api-version-keyword",
                line=4,
                node=node.body[0],
                col_offset=0,
                end_line=4,
                end_col_offset=16,
            )
        ):
            self.checker.visit_classdef(node.body[0])

    def test_guidelines_link_active(self):
        url = "https://azure.github.io/azure-sdk/python_design.html#specifying-the-service-version"
        config = Configuration()
        client = PipelineClient(url, config=config)
        request = client.get(url)
        response = client._pipeline.run(request)
        assert response.http_response.status_code == 200


class TestCheckNonCoreNetworkImport(pylint.testutils.CheckerTestCase):
    """Test that we are blocking disallowed imports and allowing allowed imports."""

    CHECKER_CLASS = checker.NonCoreNetworkImport

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "non_core_network_import.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_disallowed_imports(self, setup):
        """Check that illegal imports raise warnings"""
        # Blocked import outside of core.
        import_node = setup.body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="networking-import-outside-azure-core-transport",
                line=2,
                node=import_node,
                col_offset=0,
                end_line=2,
                end_col_offset=15,
            )
        ):
            self.checker.visit_import(import_node)

        # blocked import from outside of core.
        importfrom_node = setup.body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="networking-import-outside-azure-core-transport",
                line=3,
                node=importfrom_node,
                col_offset=0,
                end_line=3,
                end_col_offset=23,
            )
        ):
            self.checker.visit_importfrom(importfrom_node)

    def test_allowed_imports(self, setup):
        """Check that allowed imports don't raise warnings."""
        # import not in the blocked list.
        import_node = setup.body[2]
        with self.assertNoMessages():
            self.checker.visit_import(import_node)

        # from import not in the blocked list.
        importfrom_node = setup.body[3]
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # blocked import, but in core.
        import_node = setup.body[4]
        import_node.root().name = "azure.core.pipeline.transport"
        with self.assertNoMessages():
            self.checker.visit_import(import_node)

        # blocked from import, but in core.
        importfrom_node = setup.body[5]
        importfrom_node.root().name = "azure.core.pipeline.transport._private_module"
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)


class TestCheckNonAbstractTransportImport(pylint.testutils.CheckerTestCase):
    """Test that we are blocking disallowed imports and allowing allowed imports."""

    CHECKER_CLASS = checker.NonAbstractTransportImport

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "non_abstract_transport_import.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_disallowed_imports(self, setup):
        """Check that illegal imports raise warnings"""
        importfrom_node = setup.body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="non-abstract-transport-import",
                line=2,
                node=importfrom_node,
                col_offset=0,
                end_line=2,
                end_col_offset=59,
            )
        ):
            self.checker.visit_importfrom(importfrom_node)

    def test_allowed_imports(self, setup):
        """Check that allowed imports don't raise warnings."""
        # import not in the blocked list.
        importfrom_node = setup.body[1]
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # from import not in the blocked list.
        importfrom_node = setup.body[2]
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # Import abstract classes
        importfrom_node = setup.body[3]
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # Import non-abstract classes, but from in `azure.core.pipeline.transport`.
        importfrom_node = setup.body[4]
        importfrom_node.root().name = "azure.core.pipeline.transport._private_module"
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)


class TestRaiseWithTraceback(pylint.testutils.CheckerTestCase):
    """Test that we don't use raise with traceback"""

    CHECKER_CLASS = checker.NoAzureCoreTracebackUseRaiseFrom

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "raise_with_traceback.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_raise_traceback(self, setup):
        node = setup.body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="no-raise-with-traceback",
                line=1,
                node=node,
                col_offset=0,
                end_line=1,
                end_col_offset=96,
            )
        ):
            self.checker.visit_importfrom(node)


class TestTypePropertyNameLength(pylint.testutils.CheckerTestCase):
    """Test that we are checking the type and property name lengths"""

    CHECKER_CLASS = checker.NameExceedsStandardCharacterLength

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "type_property_name_length.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_class_name_too_long(self, setup):
        class_node = setup.body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="name-too-long",
                line=2,
                node=class_node,
                col_offset=0,
                end_line=2,
                end_col_offset=52,
            )
        ):
            self.checker.visit_classdef(class_node)

    def test_function_name_too_long(self, setup):
        class_node = setup.body[1]
        function_node = setup.body[1].body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="name-too-long",
                line=9,
                node=function_node,
                col_offset=4,
                end_line=9,
                end_col_offset=54,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_variable_name_too_long(self, setup):
        class_node = setup.body[1]
        function_node = setup.body[1].body[1]
        property_node = setup.body[1].body[1].body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="name-too-long",
                line=14,
                node=property_node.targets[0],
                col_offset=8,
                end_line=14,
                end_col_offset=60,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_private_name_too_long(self, setup):
        class_node = setup.body[1]
        function_node = setup.body[1].body[2]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
            self.checker.visit_functiondef(function_node)

    def test_instance_attr_name_too_long(self, setup):
        class_node = setup.body[1]
        function_node = setup.body[1].body[3]
        property_node = setup.body[1].body[3].body[0]
        with self.assertNoMessages():
            self.checker.visit_classdef(class_node)
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="name-too-long",
                line=22,
                node=property_node.targets[0],
                col_offset=8,
                end_line=22,
                end_col_offset=54,
            )
        ):
            self.checker.visit_functiondef(function_node)

    def test_class_var_name_too_long(self, setup):
        class_node = setup.body[1]
        function_node = setup.body[1].body[4]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="name-too-long",
                line=25,
                node=class_node.body[4].targets[0],
                col_offset=4,
                end_line=25,
                end_col_offset=45,
            )
        ):
            self.checker.visit_functiondef(class_node)
        with self.assertNoMessages():
            self.checker.visit_functiondef(function_node)


class TestDeleteOperationReturnType(pylint.testutils.CheckerTestCase):
    """Test that we are checking the return type of delete functions is correct"""

    CHECKER_CLASS = checker.DeleteOperationReturnStatement

    @pytest.fixture(scope="class")
    def setup(self):
        file = open(
            os.path.join(TEST_FOLDER, "test_files", "delete_operation_return_type.py")
        )
        node = astroid.parse(file.read())
        file.close()
        return node

    def test_begin_delete_operation_incorrect_return(self, setup):
        node = setup.body[2].body[0]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="delete-operation-wrong-return-type",
                line=7,
                node=node,
                col_offset=4,
                end_line=7,
                end_col_offset=34,
            )
        ):
            self.checker.visit_functiondef(node)

    def test_delete_operation_incorrect_return(self, setup):
        node = setup.body[2].body[1]
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="delete-operation-wrong-return-type",
                line=11,
                node=node,
                col_offset=4,
                end_line=11,
                end_col_offset=28,
            )
        ):
            self.checker.visit_functiondef(node)

    def test_delete_operation_correct_return(self, setup):
        node = setup.body[2].body[2]
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_begin_delete_operation_correct_return(self, setup):
        node = setup.body[2].body[3]
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)


class TestDocstringParameters(pylint.testutils.CheckerTestCase):

    """Test that we are checking the docstring is correct"""

    CHECKER_CLASS = checker.CheckDocstringParameters

    def test_docstring_vararg(self):
        node = astroid.extract_node(
            # Check that we recognize *args as param in docstring
            """
            def function_foo(x, y, *z):
                '''
                :param x: x
                :type x: str
                :param str y: y
                :param str z: z
                '''
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_vararg_keyword_args(self):
        # Check that we recognize keyword-only args after *args in docstring
        node = astroid.extract_node(
            """
            def function_foo(x, y, *z, a="Hello", b="World"):
                '''
                :param x: x
                :type x: str
                :param str y: y
                :param str z: z
                :keyword str a: a
                :keyword str b: b
                '''
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_varag_no_type(self):
        # Error on documenting keyword only args as param after *args in docstring
        node = astroid.extract_node(
            """
            def function_foo(*x):
                '''
                :param x: x
                :keyword z: z
                :paramtype z: str
                '''
            """
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-keyword-should-match-keyword-only",
                line=2,
                node=node,
                args="z",
                col_offset=0,
                end_line=2,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="docstring-missing-type",
                line=2,
                args="x",
                node=node,
                col_offset=0,
                end_line=2,
                end_col_offset=16,
            ),
        ):
            self.checker.visit_functiondef(node)

    def test_docstring_class_paramtype(self):
        node = astroid.extract_node(
            """
            class MyClass(): #@
                def function_foo(**kwargs): #@
                    '''
                    :keyword z: z
                    :paramtype z: str
                    '''
                
                def function_boo(**kwargs): #@
                    '''
                    :keyword z: z
                    :paramtype z: str
                    '''
            """
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-keyword-should-match-keyword-only",
                line=3,
                node=node[1],
                args="z",
                col_offset=4,
                end_line=3,
                end_col_offset=20,
            ),
        ):
            self.checker.visit_functiondef(node[1])
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-keyword-should-match-keyword-only",
                line=9,
                node=node[2],
                args="z",
                col_offset=4,
                end_line=9,
                end_col_offset=20,
            ),
        ):
            self.checker.visit_functiondef(node[2])

    def test_docstring_property_decorator(self):
        node = astroid.extract_node(
            """
            from typing import Dict
            
            @property
            def function_foo(self) -> Dict[str,str]:
                '''The current headers collection.
                :rtype: dict[str, str]
                '''
                return {"hello": "world"}
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_no_property_decorator(self):
        node = astroid.extract_node(
            """
            from typing import Dict
            def function_foo(self) -> Dict[str,str]:
                '''The current headers collection.
                :rtype: dict[str, str]
                '''
                return {"hello": "world"}
            """
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-missing-return",
                line=3,
                args=None,
                node=node,
                col_offset=0,
                end_line=3,
                end_col_offset=16,
            ),
        ):
            self.checker.visit_functiondef(node)

    def test_docstring_type_has_space(self):
        # Don't error if there is extra spacing in the type
        node = astroid.extract_node(
            """
            def function_foo(x):
                '''
                :param dict[str, int] x: x
                '''
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_type_has_many_spaces(self):
        # Don't error if there is extra spacing around the type
        node = astroid.extract_node(
            """
            def function_foo(x):
                '''
                :param  dict[str, int]  x: x
                '''
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_raises(self):
        node = astroid.extract_node(
            """
            def function_foo():
                '''
                :raises: ValueError
                '''
                print("hello")
                raise ValueError("hello")
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_keyword_only(self):
        node = astroid.extract_node(
            """
            def function_foo(self, x, *, z, y=None):
                '''
                :param x: x
                :type x: str
                :keyword str y: y
                :keyword str z: z
                '''
                print("hello")
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_correct_rtype(self):
        node = astroid.extract_node(
            """
            def function_foo(self, x, *, z, y=None) -> str:
                '''
                :param x: x
                :type x: str
                :keyword str y: y
                :keyword str z: z
                :rtype: str
                '''
                print("hello")
            """
        )
        with self.assertNoMessages():
            self.checker.visit_functiondef(node)

    def test_docstring_class_type(self):
        node = astroid.extract_node(
            """
            def function_foo(self, x, y):
                '''
                :param x: x
                :type x: :class:`azure.core.credentials.AccessToken`
                :param y: y
                :type y: str
                :rtype: :class:`azure.core.credentials.AccessToken`
                '''
                print("hello")
            """
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="docstring-type-do-not-use-class",
                line=2,
                args="x",
                node=node,
                col_offset=0,
                end_line=2,
                end_col_offset=16,
            ),
            pylint.testutils.MessageTest(
                msg_id="docstring-type-do-not-use-class",
                line=2,
                args="rtype",
                node=node,
                col_offset=0,
                end_line=2,
                end_col_offset=16,
            ),
        ):
            self.checker.visit_functiondef(node)


class TestDoNotImportLegacySix(pylint.testutils.CheckerTestCase):
    """Test that we are blocking disallowed imports and allowing allowed imports."""

    CHECKER_CLASS = checker.DoNotImportLegacySix

    def test_disallowed_import_from(self):
        """Check that illegal imports raise warnings"""
        importfrom_node = astroid.extract_node("from six import with_metaclass")
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="do-not-import-legacy-six",
                line=1,
                node=importfrom_node,
                col_offset=0,
                end_line=1,
                end_col_offset=30,
            )
        ):
            self.checker.visit_importfrom(importfrom_node)

    def test_disallowed_import(self):
        """Check that illegal imports raise warnings"""
        importfrom_node = astroid.extract_node("import six")
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="do-not-import-legacy-six",
                line=1,
                node=importfrom_node,
                col_offset=0,
                end_line=1,
                end_col_offset=10,
            )
        ):
            self.checker.visit_import(importfrom_node)

    def test_allowed_imports(self):
        """Check that allowed imports don't raise warnings."""
        # import not in the blocked list.
        importfrom_node = astroid.extract_node("from math import PI")
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # from import not in the blocked list.
        importfrom_node = astroid.extract_node(
            "from azure.core.pipeline import Pipeline"
        )
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)


class TestCheckNoLegacyAzureCoreHttpResponseImport(pylint.testutils.CheckerTestCase):
    """Test that we are blocking disallowed imports and allowing allowed imports."""

    CHECKER_CLASS = checker.NoLegacyAzureCoreHttpResponseImport

    def test_disallowed_import_from(self):
        """Check that illegal imports raise warnings"""
        importfrom_node = astroid.extract_node(
            "from azure.core.pipeline.transport import HttpResponse"
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="no-legacy-azure-core-http-response-import",
                line=1,
                node=importfrom_node,
                col_offset=0,
                end_line=1,
                end_col_offset=54,
            )
        ):
            self.checker.visit_importfrom(importfrom_node)

    def test_allowed_imports(self):
        """Check that allowed imports don't raise warnings."""
        # import not in the blocked list.
        importfrom_node = astroid.extract_node("from math import PI")
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # from import not in the blocked list.
        importfrom_node = astroid.extract_node(
            "from azure.core.pipeline import Pipeline"
        )
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

        # Import HttpResponse, but from in `azure.core`.
        importfrom_node = astroid.extract_node("from .. import HttpResponse")
        importfrom_node.root().name = "azure.core"
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)


class TestCheckNoTypingUnderTypeChecking(pylint.testutils.CheckerTestCase):
    """Test that we are blocking disallowed imports and allowing allowed imports."""

    CHECKER_CLASS = checker.NoImportTypingFromTypeCheck

    def test_disallowed_import_from(self):
        """Check that illegal imports raise warnings"""
        import_node = astroid.extract_node(
            """
            from typing import TYPE_CHECKING

            if TYPE_CHECKING:
                from typing import Any #@
            """
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="no-typing-import-in-type-check",
                line=5,
                node=import_node,
                col_offset=4,
                end_line=5,
                end_col_offset=26,
            )
        ):
            self.checker.visit_importfrom(import_node)

    def test_disallowed_import_from_extensions(self):
        """Check that illegal imports raise warnings"""
        import_node = astroid.extract_node(
            """
            from typing import TYPE_CHECKING

            if TYPE_CHECKING:
                import typing_extensions #@
            """
        )
        with self.assertAddsMessages(
            pylint.testutils.MessageTest(
                msg_id="no-typing-import-in-type-check",
                line=5,
                node=import_node,
                col_offset=4,
                end_line=5,
                end_col_offset=28,
            )
        ):
            self.checker.visit_import(import_node)

    def test_allowed_imports(self):
        """Check that allowed imports don't raise warnings."""
        # import not in the blocked list.
        importfrom_node = astroid.extract_node(
        """
            from typing import TYPE_CHECKING

            if TYPE_CHECKING:

                from math import PI
            """
        )
        with self.assertNoMessages():
            self.checker.visit_importfrom(importfrom_node)

    def test_allowed_import_else(self):
        """Check that illegal imports raise warnings"""
        ima, imb, imc, imd = astroid.extract_node(
            """
            if sys.version_info >= (3, 9):
                from collections.abc import MutableMapping
            else:
                from typing import MutableMapping #@
                import typing #@
                import typing_extensions #@
                from typing_extensions import Protocol #@
            """
        )
        with self.assertNoMessages():
            self.checker.visit_importfrom(ima)
            self.checker.visit_import(imb)
            self.checker.visit_import(imc)
            self.checker.visit_importfrom(imd)

# [Pylint] custom linter check for invalid use of @overload #3229
# [Pylint] Custom Linter check for Exception Logging #3227
# [Pylint] Address Commented out Pylint Custom Plugin Checkers #3228
# [Pylint] Add a check for connection_verify hardcoded settings #35355
# [Pylint] Refactor test suite for custom pylint checkers to use files instead of docstrings #3233
# [Pylint] Investigate pylint rule around missing dependency #3231