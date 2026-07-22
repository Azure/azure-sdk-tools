# Test no_cross_package_private_import
# Importing private API from a different azure package
from azure.storage.blob._generated.models import BlobProperties
from azure.storage.blob._serialize import serialize_blob_headers
import azure.core._pipeline_client
from azure.mgmt.core._serialization import Serializer

# ---- acceptable ----
# Importing from your own package's private modules (same prefix)
from azure.storage.file.datalake._models import DataLakeFileClient
import azure.storage.file.datalake._internal

# Importing public APIs from other packages
from azure.storage.blob import BlobServiceClient
from azure.core import PipelineClient
import azure.identity

# Non-azure imports are fine
import os
from collections import OrderedDict

# ---- relative imports ----
# Same-package level-1 relative import: resolves to azure.storage.file.datalake._operations — NOT flagged
from ._operations import SomeMixin  # noqa: F401, E402
# Same-package level-1 with leading-private modname: resolves to azure.storage.file.datalake._utils — NOT flagged
from ._utils import helper  # noqa: F401, E402

# Cross-package level-3 relative import: from azure.storage.file.datalake._some_module,
# "from ...blob._generated.models import …" resolves to azure.storage.blob._generated.models — FLAGGED
from ...blob._generated.models import BlobItem  # noqa: F401, E402

# Absolute import whose modname starts with '_': _get_private_prefix returns None (i==0) — NOT flagged
from _utils import something_absolute  # noqa: F401, E402

