import logging
import sys

from apistub import console_entry_point

if __name__ == "__main__":
    try:
        console_entry_point()
        sys.exit(0)
    except Exception as err:
        logging.error(err)
        sys.exit(1)
