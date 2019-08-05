import pluggy
from tox.reporter import verbosity0
import pdb
import os
import py

from tox.config import ParseIni

hookimpl = pluggy.HookimplMarker("tox")

REPLACABLE_STRINGS = [""]

def replace_command_list(old_path, new_path, command_list):
  for index, item in enumerate(command_list):
    print(item)
    exit(1)
    #command_list[index] = item.replace(old_path, new_path)

def update_env(old_path, new_path, environment_config):
  environment_config_values = dir(environment_config)
  
  environment_config.commands = replace_command_list(old_path, new_path, environment_config.commands)
  environment_config.commands_pre = replace_command_list(old_path, new_path, environment_config.commands_pre)
  environment_config.commands_post = replace_command_list(old_path, new_path, environment_config.commands_post)
#  print(environment_config.pip_pre)
#  print(environment_config.download)
 
  environment_config.envdir = py.path.local(environment_config.envdir.strpath.replace(old_path, new_path))
  environment_config.changedir = py.path.local(environment_config.changedir.strpath.replace(old_path, new_path))
  environment_config.envtmpdir = py.path.local(environment_config.envtmpdir.strpath.replace(old_path, new_path))


@hookimpl
def tox_configure(config):
    # update all configuration values that leverage {toxinidir}
    # or {toxinipath} natively.
    surface_config_values = dir(config)

    invocationcwd = config.invocationcwd.strpath
    original_toxinipath = config.toxinidir.strpath

    # surface values
    toxinidir = invocationcwd
    toxworkdir = "{toxinidir}/.tox".format(toxinidir=toxinidir)
    temp_dir = "{toxworkdir}/.tmp".format(toxworkdir=toxworkdir)
    setupdir = "{toxinidir}".format(toxinidir=toxinidir)
    distdir = "{toxworkdir}/dist".format(toxworkdir=toxworkdir)
    sdistsrc = "{toxworkdir}/dist".format(toxworkdir=toxworkdir)
    changedir = "{toxinidir}".format(toxinidir=toxinidir)

    config.toxinidir = py.path.local(toxinidir)
    config.toxworkdir = py.path.local(toxworkdir)
    config.temp_dir = py.path.local(temp_dir)
    config.setupdir = py.path.local(setupdir)
    config.distdir = py.path.local(distdir)
    config.sdistsrc = py.path.local(sdistsrc)
    config.changedir = py.path.local(changedir)

    # possibly logdir


    # these are the surface configs, but what about the environment configs?


    # # setter method

    # # the documentation (https://tox.readthedocs.io/en/latest/config.html#tox-global-settings)
    # # seems unclear about whether or not the {provision_tox_env} needs to be updated. holding
    # # off for now
    for environment_name, environment_config in config.envconfigs.items():
        update_env(original_toxinipath, invocationcwd, environment_config)

        #print(dir(environment_config))
        #print(type(environment_config))

        #old_val = getattr(config, config_value)
        #print('name:{} ; value: {}'.format(config_value, old_val))
        # #print('type: {}'.format(type(old_val)))
        #new_changedir = py.path.local(environment_config.changedir.strpath.replace(original_toxinipath, invocationcwd))
        #environment_config.changedir = new_changedir
        # new_envbindir = py.path.local(environment_config.envbindir.strpath.replace(original_toxinipath, invocationcwd))
        # environment_config.envbindir = new_envbindir
        #environment_config.envdir = py.path.local(environment_config.envdir.strpath.replace(original_toxinipath, invocationcwd))
        # environment_config.envlogdir = py.path.local(environment_config.envlogdir.strpath.replace(original_toxinipath, invocationcwd))
        # environment_config.envpython = py.path.local(environment_config.envpython.strpath.replace(original_toxinipath, invocationcwd))
        # environment_config.envtmpdir = py.path.local(environment_config.envtmpdir.strpath.replace(original_toxinipath, invocationcwd))

