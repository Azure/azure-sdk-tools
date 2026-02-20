# Test file for DoNotStoreSecretsInTestVariables checker

# Mock objects for testing
class MockClient:
    @property
    def secret(self):
        return "mock_secret"
    
    def get_data(self):
        return "mock_data"
    
    @property 
    def secret_config(self):
        return "mock_config"

class MockAuth:
    @property
    def secret(self):
        return "mock_auth_secret"

class MockService:
    @property
    def secret(self):
        return "mock_service_secret"

class MockItem:
    @property
    def secret(self):
        return "mock_item_secret"

class MockConfig:
    def __init__(self):
        self.auth = MockAuth()

# Mock variables
my_client = MockClient()
client = MockClient()
auth = MockAuth()
config = MockConfig()
service = MockService()
condition = True
items = [MockItem(), MockItem()]

# Mock functions
def some_function(arg):
    pass

def other_function(param=None):
    pass

def function_call(arg1, arg2):
    pass

def another_call(param=None):
    pass

def process(arg):
    pass

def handle(arg):
    pass

# This should trigger the rule
def test_bad_secret_usage():
    # This assigns a secret to a variable (should be flagged)
    secret_value = my_client.secret
    
    # Using the secret variable (should also be flagged) 
    some_function(secret_value)
    
    # Also check keyword args
    other_function(param=secret_value)

# Multiple secret assignments
def test_multiple_secrets():
    secret1 = client.secret
    secret2 = auth.secret
    secret3 = config.auth.secret
    
    # Using the secret variables
    function_call(secret1, secret2)
    another_call(param=secret3)

# Secret assignments in different contexts
def test_secret_in_contexts():
    # In if statement
    if condition:
        temp_secret = service.secret
        process(temp_secret)
    
    # In for loop
    for item in items:
        loop_secret = item.secret
        handle(loop_secret)

# This should NOT trigger the rule
def test_good_secret_usage():
    # Direct usage is preferred
    some_function(my_client.secret)
    other_function(param=my_client.secret)
    
    # Non-secret variables are fine
    normal_value = my_client.get_data()
    some_function(normal_value)
    
    # Other attributes ending with 'secret' but not actually secret
    not_secret = my_client.secret_config  # This won't trigger since it's not .secret