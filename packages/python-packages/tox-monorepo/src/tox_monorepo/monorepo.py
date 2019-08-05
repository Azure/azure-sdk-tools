import pluggy
from tox.reporter import verbosity0
import pdb
import os
import py

hookimpl = pluggy.HookimplMarker("tox")


def replace_command_list(old_path, new_path, command_list):
    if command_list is not None:
        for command_index, command_item in enumerate(command_list):
            if command_item is not None:
                for sub_command_index, sub_command_item in enumerate(command_item):
                    command_item[sub_command_index] = sub_command_item.replace(
                        old_path, new_path
                    )


# used to update a specific TestEnv object
def update_env(old_path, new_path, environment_config):
    # update commands
    replace_command_list(old_path, new_path, environment_config.commands)
    replace_command_list(old_path, new_path, environment_config.commands_pre)
    replace_command_list(old_path, new_path, environment_config.commands_post)

    # update environment configs
    environment_config.envdir = py.path.local(
        environment_config.envdir.strpath.replace(old_path, new_path)
    )
    environment_config.changedir = py.path.local(
        environment_config.changedir.strpath.replace(old_path, new_path)
    )
    environment_config.envtmpdir = py.path.local(
        environment_config.envtmpdir.strpath.replace(old_path, new_path)
    )


@hookimpl
def tox_configure(config):
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

    for environment_name, environment_config in config.envconfigs.items():
        update_env(original_toxinipath, invocationcwd, environment_config)
