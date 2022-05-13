import inspect
from ._base_node import get_qualified_name
from ._argtype import ArgType


class FunctionAnnotationParser:

    def __init__(self, obj, namespace: str, func_node):
        self._namespace = namespace
        self._parent = func_node
        self.special_vararg = None
        self.special_kwarg = None
        self.args = {}
        self.kwargs = {}
        self.posargs = {}
        self.varargs = None
        self.return_type = None
        annotations = getattr(obj, "__annotations__", None)
        if annotations:
            self.return_type = annotations.pop('return', inspect.Parameter.empty)
            self.args = {
                name: ArgType(name, argtype=get_qualified_name(argtype, namespace), default=inspect.Parameter.empty, keyword=None, func_node=func_node) for (name, argtype) in annotations.items()
            }
