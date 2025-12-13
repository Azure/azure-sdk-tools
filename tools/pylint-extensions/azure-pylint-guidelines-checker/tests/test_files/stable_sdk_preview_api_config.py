# Test file for stable SDK preview API checker - Config object pattern
# This simulates the pattern used in azure-ai-evaluation and other generated SDKs

# Case 1: Stable SDK version
VERSION = "1.0.0"


class MyClientConfiguration:
    """Configuration for MyClient - simulates generated config classes"""
    
    def __init__(self, endpoint: str, **kwargs):
        self.endpoint = endpoint
        # Pattern 1: Direct assignment of preview API in config (should error)
        self.api_version: str = "2024-01-01-preview"  # @
        
        
class MyOtherClientConfiguration:
    """Another config pattern with kwargs.pop"""
    
    def __init__(self, endpoint: str, **kwargs):
        self.endpoint = endpoint
        # Pattern 2: kwargs.pop with preview API as default (should error)
        self.api_version: str = kwargs.pop("api_version", "2024-02-01-preview")  # @


class MyThirdClientConfiguration:
    """Config with variable for default API"""
    
    # Pattern 3: Class-level constant (should error)
    DEFAULT_API_VERSION = "2024-03-01-preview"  # @
    
    def __init__(self, endpoint: str, **kwargs):
        self.endpoint = endpoint
        # Using the constant - the checker won't catch this indirection
        self.api_version = kwargs.pop("api_version", self.DEFAULT_API_VERSION)


class MyClient:
    """Client that uses a configuration object"""
    
    def __init__(self, endpoint: str, credential, **kwargs):
        self.endpoint = endpoint
        self.credential = credential
        # Client uses the config object
        self._config = MyClientConfiguration(endpoint, **kwargs)
        
    def some_operation(self):
        """Operation that would use the api_version from config"""
        # The config's api_version would be used here
        pass


class MyOtherClient:
    """Another client pattern"""
    
    def __init__(self, endpoint: str, credential, **kwargs):
        # Pattern 4: Inline kwargs.pop in client (should error)
        api_version = kwargs.pop("api_version", "2024-04-01-preview")  # @
        self._api_version = api_version
        

class MyThirdClient:
    """Client with annotated assignment"""
    
    def __init__(self, endpoint: str, credential, **kwargs):
        # Pattern 5: Annotated assignment with kwargs.pop (should error)
        self._api_version: str = kwargs.pop("api_version", "2024-05-01-preview")  # @


# Non-error cases (stable SDK with stable API)
class StableConfiguration:
    """Configuration using stable API - should NOT error"""
    
    def __init__(self, endpoint: str, **kwargs):
        self.endpoint = endpoint
        self.api_version: str = "2024-01-01"  # OK - stable API
        

class StableConfigWithPop:
    """Configuration using stable API with kwargs.pop - should NOT error"""
    
    def __init__(self, endpoint: str, **kwargs):
        self.endpoint = endpoint
        self.api_version: str = kwargs.pop("api_version", "2024-02-01")  # OK - stable API
