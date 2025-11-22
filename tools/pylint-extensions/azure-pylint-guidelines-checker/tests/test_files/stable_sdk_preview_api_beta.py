# Test file for beta SDK - should NOT trigger errors

# Beta SDK version (can use preview API without error)
VERSION = "1.0.0b1"

class MyClient:
    def __init__(self, endpoint, credential, **kwargs):
        self.endpoint = endpoint
        # This should NOT trigger error - beta SDK can use preview API
        self._api_version = "2023-01-01-preview"
    
    def operation_with_preview_default(self, api_version="2023-05-01-preview"):
        """Beta SDK can use preview API"""
        pass
    
    def operation_calling_preview(self):
        """Beta SDK can call preview API"""
        self._some_method(api_version="2023-01-01-preview")
    
    def _some_method(self, api_version):
        pass
