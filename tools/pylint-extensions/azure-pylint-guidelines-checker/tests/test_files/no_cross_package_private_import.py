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
