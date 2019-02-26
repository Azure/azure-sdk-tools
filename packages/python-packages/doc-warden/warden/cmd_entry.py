# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

from __future__ import print_function

from .enforce_readme_presence import find_missing_readmes
from .enforce_readme_content import verify_readme_content
from .WardenConfiguration import WardenConfiguration

# CONFIGURATION. ENTRY POINT. EXECUTION.
def console_entry_point():
    cfg = WardenConfiguration()
    print(cfg.dump())

    command_selector = {
        'scan': scan,
    }
    
    if cfg.command in command_selector:
        command_selector.get(cfg.command)(cfg)
    else:
        print('Unrecognized command invocation {}.'.format(cfg.command))
        exit(1)

def scan(config):
    find_missing_readmes(config)
    verify_readme_content(config)
