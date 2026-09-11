from __future__ import annotations

import asyncio
import logging

from .sync import sync_repositories


def main() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    logging.getLogger("azure").setLevel(logging.WARNING)
    asyncio.run(sync_repositories())


if __name__ == "__main__":
    main()
