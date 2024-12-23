import inspect
from ._base_node import get_qualified_name
from ._argtype import ArgType


class FunctionAnnotationParser:

    def __init__(self, obj, namespace: str, apiview, func_node):
        self._namespace = namespace
        self._parent = func_node
        self.special_vararg = None
        self.special_kwarg = None
        self.args = {}
        self.kwargs = {}
        self.posargs = {}
        self.varargs = None
        self.return_type = None
        self.apiview = apiview
        # TODO: Replace with "get_annotations" once min Python is 3.10+
        # See: https://docs.python.org/3.10/howto/annotations.html#accessing-the-annotations-dict-of-an-object-in-python-3-9-and-older
        if isinstance(obj, type):
            annotations = obj.__dict__.get("__annotations__", None)
        else:
            annotations = getattr(obj, "__annotations__", None)
        if annotations:
            self.return_type = annotations.pop("return", inspect.Parameter.empty)
            self.args = {
                name: ArgType(
                    name,
                    apiview=self.apiview,
                    argtype=get_qualified_name(argtype, namespace),
                    default=inspect.Parameter.empty,
                    keyword=None,
                    func_node=func_node,
                )
                for (name, argtype) in annotations.items()
            }
