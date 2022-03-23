import re
import inspect
import logging


find_type_hint_ret_type = "(?<!#)\\s?->\\s?([^\n:#]*)"

class TypeHintParser:
    """TypeHintParser helps to find return type from type hint is type hint is available
    :param object: obj
    """

    def __init__(self, obj):
        self.obj = obj
        try:
            code = inspect.getsource(obj)
            self.ret_type = self._parse_typehint(code)
        except:
            self.ret_type = None
            logging.error("Failed to get source of object {}".format(obj))

    def _parse_typehint(self, source):
        # Find return type from type hint
        ret_type = re.search(find_type_hint_ret_type, source)
        # Don't return None as string literal
        if ret_type and ret_type != "None":
            return ret_type.groups()[-1].strip().replace('"', "")
        else:
            return None
