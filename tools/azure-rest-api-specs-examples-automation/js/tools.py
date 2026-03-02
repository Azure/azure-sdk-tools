import os
import platform
import subprocess
import logging
from typing import List


OS_WINDOWS = platform.system().lower() == "windows"


def check_call(cmd: List[str], work_dir: str):
    logging.info("Command line: " + " ".join(cmd))
    subprocess.check_call(cmd, cwd=work_dir)


def publish_samples(sdk_path: str, module_relative_path: str):
    pnpm_cmd = "pnpm" + (".cmd" if OS_WINDOWS else "")
    npx_cmd = "npx" + (".cmd" if OS_WINDOWS else "")

    cmd = [pnpm_cmd, "install"]
    check_call(cmd, sdk_path)

    cmd = [npx_cmd, "dev-tool", "samples", "publish"]
    check_call(cmd, os.path.join(sdk_path, module_relative_path))
