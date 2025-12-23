# Test file for stable SDK preview API checker

# Case 1: Stable SDK version (should trigger error when using preview api)
VERSION = "1.0.0"

class MyClient:
    def __init__(self, endpoint, credential, **kwargs):
        self.endpoint = endpoint
        # This should trigger error - stable SDK using preview API as default
        self._api_version = "2023-01-01-preview"  # @
    
    def operation_with_preview_default(self, api_version="2023-05-01-preview"):  # @
        """Operation with preview API version as default - should error"""
        pass
    
    def operation_with_stable_default(self, api_version="2023-05-01"):
        """Operation with stable API version - should be OK"""
        pass
    
    def operation_calling_preview(self):
        """Calling something with preview API - should error"""
        # This should trigger error
        self._some_method(api_version="2023-01-01-preview")  # @
    
    def operation_calling_stable(self):
        """Calling something with stable API - should be OK"""
        self._some_method(api_version="2023-01-01")
    
    def _some_method(self, api_version):
        pass


# Case 2: Beta SDK version (should NOT trigger error even with preview API)
# This would be in a different file, but shown for reference
