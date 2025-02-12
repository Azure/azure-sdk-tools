import astroid
import inspect
from ._base_node import get_qualified_name
from ._argtype import ArgType


class AstroidFunctionParser:

    def __init__(self, node: astroid.FunctionDef, namespace: str, apiview, func_node):
        if not isinstance(node, astroid.FunctionDef):
            raise TypeError(f"Can only pass in an astroid FunctionDef node, not {func_node}")
        self._namespace = namespace
        self._node = node
        self._args = node.args
        self._parent = func_node
        self.special_vararg = None
        self.special_kwarg = None
        self.args = {}
        self.kwargs = {}
        self.posargs = {}
        self.varargs = None
        self.return_type = node.returns or node.type_comment_returns or inspect.Parameter.empty
        self.apiview = apiview
        self._parse_args()
        self._parse_kwargs()
        self._parse_posonly_args()
        self._parse_kwarg_and_vararg()

    def _default_value(self, name):
        try:
            default = self._args.default_value(name)
            if isinstance(default, astroid.node_classes.Const):
                return default.value
            else:
                return default
        except astroid.NoDefault:
            return inspect.Parameter.empty

    def _argtype(self, name, idx, annotations, type_comments):
        annotation = annotations[idx]
        type_comment = type_comments[idx]
        argtype = annotation or type_comment
        return get_qualified_name(argtype, self._namespace) if argtype else None

    def _parse_args(self):
        for idx, arg in enumerate(self._args.args):
            name = arg.name
            argtype = self._argtype(name, idx, self._args.annotations, self._args.type_comment_args)
            default = self._default_value(name)
            self.args[name] = ArgType(
                name, argtype=argtype, default=default, keyword=None, apiview=self.apiview, func_node=self._parent
            )

    def _parse_kwargs(self):
        for idx, arg in enumerate(self._args.kwonlyargs):
            name = arg.name
            argtype = self._argtype(name, idx, self._args.kwonlyargs_annotations, self._args.type_comment_kwonlyargs)
            default = self._default_value(name)
            self.kwargs[name] = ArgType(
                name, argtype=argtype, default=default, keyword="keyword", apiview=self.apiview, func_node=self._parent
            )

    def _parse_posonly_args(self):
        for idx, arg in enumerate(self._args.posonlyargs):
            name = arg.name
            argtype = self._argtype(name, idx, self._args.posonlyargs_annotations, self._args.type_comment_posonlyargs)
            default = self._default_value(name)
            self.posargs[name] = ArgType(
                name, argtype=argtype, default=default, keyword=None, apiview=self.apiview, func_node=self._parent
            )

    def _parse_kwarg_and_vararg(self):
        if self._args.vararg:
            name = self._args.vararg
            if self._args.varargannotation:
                argtype = self._args.varargannotation.as_string()
            else:
                argtype = None
            arg = ArgType(
                name,
                argtype=argtype,
                default=inspect.Parameter.empty,
                keyword=None,
                apiview=self.apiview,
                func_node=self._parent,
            )
            self.special_vararg = arg
        if self._args.kwarg:
            kwarg_name = self._args.kwarg
            if self._args.kwargannotation:
                kwarg_type = self._args.kwargannotation.as_string()
            else:
                kwarg_type = None
            arg = ArgType(
                kwarg_name,
                argtype=kwarg_type,
                default=inspect.Parameter.empty,
                keyword="keyword",
                apiview=self.apiview,
                func_node=self._parent,
            )
            self.special_kwarg = arg
