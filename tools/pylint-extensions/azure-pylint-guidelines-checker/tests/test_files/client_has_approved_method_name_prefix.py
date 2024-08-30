from azure.core.tracing.decorator import distributed_trace


# test_ignores_constructor
class SomeClient():  # @
    def __init__(self, **kwargs):  # @
        pass


# test_ignores_private_method
class Some1Client():  # @
    def _private_method(self, **kwargs):  # @
        pass


# test_ignores_if_exists_suffix
class Some2Client():  # @
    def check_if_exists(self, **kwargs):  # @
        pass


# test_ignores_from_prefix
class Some3Client():  # @
    def from_connection_string(self, **kwargs):  # @
        pass


# test_ignores_approved_prefix_names
class Some4Client():  # @
    def create_configuration(self):  # @
        pass

    def get_thing(self):  # @
        pass

    def list_thing(self):  # @
        pass

    def upsert_thing(self):  # @
        pass

    def set_thing(self):  # @
        pass

    def update_thing(self):  # @
        pass

    def replace_thing(self):  # @
        pass

    def append_thing(self):  # @
        pass

    def add_thing(self):  # @
        pass

    def delete_thing(self):  # @
        pass

    def remove_thing(self):  # @
        pass

    def begin_thing(self):  # @
        pass


# test_ignores_non_client_with_unapproved_prefix_names
class SomethingElse():  # @
    def download_thing(self, some, **kwargs):  # @
        pass


# test_ignores_nested_function_with_unapproved_prefix_names
class Some5Client():  # @
    def create_configuration(self, **kwargs):  # @
        def nested(hello, world):
            pass


# test_finds_unapproved_prefix_names
class Some6Client():  # @
    @distributed_trace
    def build_configuration(self):  # @
        pass

    def generate_thing(self):  # @
        pass

    def make_thing(self):  # @
        pass

    def insert_thing(self):  # @
        pass

    def put_thing(self):  # @
        pass

    def creates_configuration(self):  # @
        pass

    def gets_thing(self):  # @
        pass

    def lists_thing(self):  # @
        pass

    def upserts_thing(self):  # @
        pass

    def sets_thing(self):  # @
        pass

    def updates_thing(self):  # @
        pass

    def replaces_thing(self):  # @
        pass

    def appends_thing(self):  # @
        pass

    def adds_thing(self):  # @
        pass

    def deletes_thing(self):  # @
        pass

    def removes_thing(self):  # @
        pass
