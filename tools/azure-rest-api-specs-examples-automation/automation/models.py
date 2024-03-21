import dataclasses
import re
from datetime import datetime
from typing import List, Dict


@dataclasses.dataclass(eq=True, frozen=True)
class OperationConfiguration:
    sdk_examples_repository: str
    build_id: str
    skip_processed: bool
    persist_data: bool
    date_start: datetime
    date_end: datetime

    @property
    def repository_owner(self) -> str:
        return re.match(r'https://github.com/([^/:]+)/.*', self.sdk_examples_repository).group(1)

    @property
    def repository_name(self) -> str:
        return re.match(r'https://github.com/[^/:]+/(.*)', self.sdk_examples_repository).group(1)


@dataclasses.dataclass(eq=True, frozen=True)
class ReleaseTagConfiguration:
    regex_match: str
    package_regex_group: str
    version_regex_group: str


@dataclasses.dataclass(eq=True)
class Script:
    run: str


@dataclasses.dataclass(eq=True, frozen=True)
class SdkConfiguration:
    name: str
    language: str
    repository: str
    release_tag: ReleaseTagConfiguration
    script: Script
    ignored_packages: List[str]

    @property
    def repository_owner(self) -> str:
        return re.match(r'https://github.com/([^/:]+)/.*', self.repository).group(1)

    @property
    def repository_name(self) -> str:
        return re.match(r'https://github.com/[^/:]+/(.*)', self.repository).group(1)


@dataclasses.dataclass(eq=True, frozen=True)
class Configuration:
    operation: OperationConfiguration
    sdks: List[SdkConfiguration]


@dataclasses.dataclass(eq=True, frozen=True)
class CommandLineConfiguration:
    build_id: str
    release_in_days: int
    language: str
    persist_data: bool
    skip_processed: bool
    merge_pr: bool


@dataclasses.dataclass(eq=True, frozen=True)
class Release:
    tag: str
    package: str
    version: str
    date: datetime


@dataclasses.dataclass(eq=True, frozen=True)
class AggregatedError:
    errors: List[Exception]


@dataclasses.dataclass(eq=True, frozen=True)
class Report:
    statuses: Dict[str, str]
    aggregated_error: AggregatedError
