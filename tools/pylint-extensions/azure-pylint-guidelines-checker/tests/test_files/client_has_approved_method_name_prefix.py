# test_ignores_constructor
class ConstrClient():  #@
    def __init__(self, **kwargs):  #@
        pass


# test_ignores_private_method
class PrivClient():  #@
    def _private_method(self, **kwargs):  #@
        pass


# test_ignores_if_exists_suffix
class ExistsClient():  #@
    def check_if_exists(self, **kwargs):  #@
        pass


# test_ignores_approved_prefix_names
class ApprovedClient():  #@
    def get_noun(self):  #@
        pass
    
    def list_noun(self):  #@
        pass
    
    def create_noun(self):  #@
        pass
    
    def upsert_noun(self):  #@
        pass
    
    def set_noun(self):  #@
        pass
    
    def update_noun(self):  #@
        pass
    
    def replace_noun(self):  #@
        pass
    
    def append_noun(self):  #@
        pass
    
    def add_noun(self):  #@
        pass
    
    def delete_noun(self):  #@
        pass
    
    def remove_noun(self):  #@
        pass
    
    def begin_noun(self):  #@
        pass
    
    def upload_noun(self):  #@
        pass
    
    def download_noun(self):  #@
        pass
    
    def close_noun(self):  #@
        pass
    
    def cancel_noun(self):  #@
        pass
    
    def clear_noun(self):  #@
        pass
    
    def subscribe_noun(self):  #@
        pass
    
    def send_noun(self):  #@
        pass
    
    def query_noun(self):  #@
        pass
    
    def analyze_noun(self):  #@
        pass
    
    def train_noun(self):  #@
        pass
    
    def detect_noun(self):  #@
        pass
    
    def from_noun(self):  #@
        pass


# test_ignores_non_client_with_unapproved_prefix_names
class SomethingElse():  #@
    def download_thing(self, some, **kwargs):  #@
        pass


# test_ignores_nested_function_with_unapproved_prefix_names
class NestedClient():  #@
    def create_configuration(self, **kwargs):  #@
        def nested(hello, world): #@
            pass


# test_finds_unapproved_prefix_names
class UnapprovedClient():  #@
    def build_configuration(self):  #@
        pass

    def generate_thing(self):  #@
        pass

    def make_thing(self):  #@
        pass

    def insert_thing(self):  #@
        pass

    def put_thing(self):  #@
        pass

    def creates_configuration(self):  #@
        pass

    def gets_thing(self):  #@
        pass

    def lists_thing(self):  #@
        pass

    def upserts_thing(self):  #@
        pass

    def sets_thing(self):  #@
        pass

    def updates_thing(self):  #@
        pass

    def replaces_thing(self):  #@
        pass

    def appends_thing(self):  #@
        pass

    def adds_thing(self):  #@
        pass

    def deletes_thing(self):  #@
        pass

    def removes_thing(self):  #@
        pass


# test_ignores_property
class PropClient():  #@
    @property
    def func(self):  #@
        pass


# test_ignores_private_client
class _PrivateClient():  #@
    def get_thing(self):  #@
        pass


# test_ignores_private_module
class PrivateModuleClient():  #@
    def get_thing(self):  #@
        pass