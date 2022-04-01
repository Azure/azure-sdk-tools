import astroid
import inspect
from ._base_node import get_qualified_name
from ._argtype import ArgType


class AstroidArgumentParser:

    def __init__(self, node: astroid.Arguments, namespace: str, func_node):
        if not isinstance(node, astroid.Arguments):
            raise TypeError("Can only pass in an astroid Arguments node.")
        self._namespace = namespace
        self._node = node
        self._parent = func_node
        self.args = {}
        self.kwargs = {}
        self.posargs = {}
        self.varargs = None
        self._parse_args()
        self._parse_kwargs()
        self._parse_posonly_args()
        self._parse_varargs()

    def _default_value(self, name):
        try:
            return self._node.default_value(name).as_string()
        except astroid.NoDefault:
            return inspect.Parameter.empty

    def _argtype(self, name, idx, annotations, type_comments):
        if annotations:
            argtype = annotations[idx]
        elif type_comments:
            argtype = type_comments[idx]
        else:
            argtype = None
        return get_qualified_name(argtype, self._namespace) if argtype else None

    def _parse_args(self):
        for (idx, arg) in enumerate(self._node.args):
            name = arg.name
            argtype = self._argtype(name, idx, self._node.annotations, self._node.type_comment_args)
            default = self._default_value(name)
            self.args[name] = ArgType(name, argtype=argtype, default=default, keyword=None, func_node=self._parent)

    def _parse_kwargs(self):
        for (idx, arg) in enumerate(self._node.kwonlyargs):
            name = arg.name
            argtype = self._argtype(name, idx, self._node.kwonlyargs_annotations, self._node.type_comment_kwonlyargs)
            default = self._default_value(name)
            self.kwargs[name] = ArgType(name, argtype=argtype, default=default, keyword="keyword", func_node=self._parent)
        if self._node.kwarg:
            kwarg_name = self._node.kwarg
            if self._node.kwargannotation:
                kwarg_type = self._node.kwargannotation.as_string()
            else:
                kwarg_type = None
            # This wonky logic matches the existing code
            arg = ArgType(kwarg_name, argtype=kwarg_type, default=inspect.Parameter.empty, keyword="keyword", func_node=self._parent)
            arg.argname = f"**{kwarg_name}"
            self.args[arg.argname] = arg

    def _parse_posonly_args(self):
        for (idx, arg) in enumerate(self._node.posonlyargs):
            name = arg.name
            argtype = self._argtype(name, idx, self._node.posonlyargs_annotations, self._node.type_comment_posonlyargs)
            default = self._default_value(name)
            self.posargs[name] = ArgType(name, argtype=argtype, default=default, keyword=None, func_node=self._parent)

    def _parse_varargs(self):
        if self._node.vararg:
            name = self._node.vararg
            if self._node.varargannotation:
                argtype = self._node.varargannotation.as_string()
            else:
                argtype = None
            arg = ArgType(name, argtype=argtype, default=inspect.Parameter.empty, keyword=None, func_node=self._parent)
            arg.argname = f"*{name}"
            self.args[name] = arg
