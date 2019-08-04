import pluggy
from tox.reporter import verbosity0
import pdb
import os

hookimpl = pluggy.HookimplMarker("tox")

@hookimpl
def tox_configure(config):
    # update all configuration values that leverage {toxinidir}
    # or {toxinipath} natively. 


    config.toxinidir = os.getcwd()
    pdb.set_trace()

