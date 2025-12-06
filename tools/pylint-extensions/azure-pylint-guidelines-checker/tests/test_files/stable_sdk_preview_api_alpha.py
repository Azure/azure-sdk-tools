# Test file for alpha SDK - should NOT trigger errors

# Alpha SDK version (can use preview API without error)
VERSION = "1.0.0a1"

class MyClient:
    def __init__(self, endpoint, credential, **kwargs):
        self.endpoint = endpoint
        # This should NOT trigger error - alpha SDK can use preview API
        self._api_version = "2023-01-01-preview"
    
    def operation_with_preview_default(self, api_version="2023-05-01-preview"):
        """Alpha SDK can use preview API"""
        pass
