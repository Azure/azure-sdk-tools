import sys
import traceback

from apistub import console_entry_point

if __name__ == "__main__":
    try:
        console_entry_point()
        sys.exit(0)
    except Exception as err:
        exc_type, exc_val, exc_tb = sys.exc_info()
        traceback.print_exception(exc_type, exc_val, exc_tb, file=sys.stderr)
        sys.exit(1)
