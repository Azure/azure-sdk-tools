FROM mcr.microsoft.com/cbl-mariner/base/core:2.0
# Included packages: https://github.com/microsoft/CBL-Mariner/blob/1.0/SPECS/core-packages/core-packages.spec

ADD ./poll.sh /poll.sh
RUN tdnf -y install wget
RUN chmod +x /poll.sh

CMD bash /poll.sh
