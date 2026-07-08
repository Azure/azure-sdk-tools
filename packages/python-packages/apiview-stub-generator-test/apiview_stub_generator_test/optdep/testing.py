try:
    import httpx
    import pandas
except ImportError:
    raise ImportError("Missing optional 'testing' dependencies.")

class Testing:
    def __init__(self):
        pass
