import logging

try:
    add = 1 + 2
except Exception as e:
    logging.exception(f"wrong {e}")  # @
    logging.debug(f"right {e}")  # @