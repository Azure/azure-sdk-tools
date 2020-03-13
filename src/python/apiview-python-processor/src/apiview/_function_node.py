import logging
import inspect
import astroid
from inspect import Parameter
from _base_node import Docstring

from _base_node import NodeEntityBase
from _base_node import ArgType


logging.getLogger().setLevel(logging.INFO)


    
class FunctionNode(NodeEntityBase):
    """Function node to represent parsed function and method nodes
    """
    def __init__(self, namespace, parent_node, obj):
        super().__init__(namespace, parent_node, obj)
        self.annotations = []
        self.args = []
        self.return_type = None
        self._inspect()


    def _inspect(self):
        code = inspect.getsource(self.obj).strip()
        self.is_async = code.startswith("async def")
        self.def_key = "async def" if self.is_async else "def"
        # Find decorators and any annotations
        node = astroid.extract_node(inspect.getsource(self.obj))
        if node.decorators:
            self.annotations = ["@{}".format(x.name) for x in node.decorators.nodes if hasattr(x, "name")]
        self._parse_function()

    
    def _parse_function(self):
        """
        Find positional and keyword arguements, type and default value and return type of method
        Parsing logic will follow below order to identify these information
        1. Identify args, types, default and ret type using inspect
        2. Parse type annotations if inspect doesn't have complete info
        3. Parse docstring to find keyword arguements
        """
        if "@classmethod" in self.annotations:
            self.args.append(ArgType("cls"))
            
        sig = inspect.signature(self.obj)
        params = sig.parameters
        kw_arg = None
        for argname in params:
            arg = ArgType(argname, NodeEntityBase.get_qualified_name(params[argname].annotation))
            if params[argname].default != Parameter.empty:
                arg.default = NodeEntityBase.get_qualified_name(params[argname].default)

            # Store handle to kwarg object to replace it later
            if params[argname].kind == Parameter.VAR_KEYWORD:
                kw_arg = arg

            self.args.append(arg)

        if sig.return_annotation:
            self.return_type = NodeEntityBase.get_qualified_name(sig.return_annotation)

        docstring = ""
        if self.name == "__init__" and hasattr(self.parent_node.obj, "__doc__"):
            docstring = self.parent_node.obj.__doc__
        else:
            docstring = self.obj.__doc__

        #  Parse doc string to find missing types, kwargs and return type
        parsed_docstring = Docstring()
        parsed_docstring.parse(docstring)
        # Copy missing types and kwargs and return type
        if not self.return_type and parsed_docstring.ret_type:
            self.return_type = parsed_docstring.ret_type

        arg_type_dict = dict([(x.argname, x.argtype) for x in parsed_docstring.pos_args])
        for pos_arg in self.args:
            pos_arg.argtype = arg_type_dict.get(pos_arg.argname, pos_arg.argtype)

        if parsed_docstring.kw_args:
            self.args.remove(kw_arg)
            self.args.extend(parsed_docstring.kw_args)
            

        # Generate function signature
        params = ", ".join([str(x) for x in self.args])
        if self.return_type:
            self.display_name = "{0} {1}({2}) -> {3}".format(self.def_key, self.name, params, self.return_type)
        else:
            self.display_name = "{0} {1}({2})".format(self.def_key, self.name, params)
        #print(self.display_name)


    def validate_signature(self):
        pass


    def dump(self, delim):
        space = ' '* delim
        for annot in self.annotations:
            print("{0}{1}".format(space, annot))
        print("{0}{1}".format(space, self.display_name))    
            


    def generate_tokens(self):
        pass