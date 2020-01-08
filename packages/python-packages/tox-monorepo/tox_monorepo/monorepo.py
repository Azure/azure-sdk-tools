# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

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
    environment_config.envlogdir = py.path.local(
        environment_config.envlogdir.strpath.replace(old_path, new_path)
    )


    # update the cachedir
    environment_config.setenv["TOX_ENV_DIR"] = environment_config.setenv[
        "TOX_ENV_DIR"
    ].replace(old_path, new_path)

@hookimpl
def tox_configure(config):
    invocationcwd = config.invocationcwd.strpath
    original_toxinipath = config.toxinidir.strpath

    if config.toxinidir:
        config.toxinidir = py.path.local(
            config.toxinidir.strpath.replace(original_toxinipath, invocationcwd)
        )

    if config.toxworkdir:
        config.toxworkdir = py.path.local(
            config.toxworkdir.strpath.replace(original_toxinipath, invocationcwd)
        )

    if config.temp_dir:
        config.temp_dir = py.path.local(
            config.temp_dir.strpath.replace(original_toxinipath, invocationcwd)
        )

    if config.setupdir:
        config.setupdir = py.path.local(
            config.setupdir.strpath.replace(original_toxinipath, invocationcwd)
        )

    if config.distdir:
        config.distdir = py.path.local(
            config.distdir.strpath.replace(original_toxinipath, invocationcwd)
        )

    if config.sdistsrc:
        config.sdistsrc = py.path.local(
            config.sdistsrc.strpath.replace(original_toxinipath, invocationcwd)
        )

    if config.logdir:
        config.logdir = py.path.local(
            config.logdir.strpath.replace(original_toxinipath, invocationcwd)
        )

    for environment_name, environment_config in config.envconfigs.items():
        update_env(original_toxinipath, invocationcwd, environment_config)
