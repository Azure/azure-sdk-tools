from .llc_view_models.llc_view_models import LLCClientView
import yaml


class TestParser:
    def __init__(self) -> None:
        self.path = "c:\\Users\\t-llawrence\\Desktop\\yaml\\translator_test.yaml"
        with open(self.path) as f:
            self.data = yaml.safe_load(f)
        print(self.data)


    def _test_return_type(self):
        client = LLCClientView(self.data)
        assert client.name == "Batch Document Translation Client"