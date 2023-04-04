FROM mcr.microsoft.com/dotnet/runtime:3.1-focal

RUN apt-get update -y &&  apt upgrade -y && apt install curl -y
RUN apt install build-essential -y

# install java
ENV JAVA_HOME /usr/java/openjdk-17
ENV PATH $JAVA_HOME/bin:$PATH
ENV LANG en_US.UTF-8
ENV JAVA_VERSION 17.0.1
RUN curl -fL -o openjdk.tgz https://download.java.net/java/GA/jdk17.0.1/2a2082e5a09d4267845be086888add4f/12/GPL/openjdk-17.0.1_linux-x64_bin.tar.gz
RUN mkdir -p "$JAVA_HOME"
RUN tar --extract --file openjdk.tgz --directory "$JAVA_HOME" --strip-components 1 --no-same-owner
RUN rm openjdk.tgz
## install maven
ENV M2_HOME /usr/maven
ENV MAVEN_HOME=/usr/maven
ENV PATH $M2_HOME/bin:$PATH
RUN mkdir -p "$MAVEN_HOME"
RUN curl -fL -o maven.tgz https://dlcdn.apache.org/maven/maven-3/3.8.8/binaries/apache-maven-3.8.8-bin.tar.gz
RUN tar --extract --file maven.tgz --directory "$MAVEN_HOME" --strip-components 1 --no-same-owner
RUN rm maven.tgz

# install node
RUN curl -sL https://deb.nodesource.com/setup_16.x | bash -
RUN apt -y install nodejs
RUN npm install -g npm@9.5.0

# install python
RUN DEBIAN_FRONTEND=noninteractive apt-get install -y wget apt-transport-https software-properties-common
RUN apt install python3-pip -y && apt install python3-venv -y && pip3 install --upgrade pip
RUN add-apt-repository ppa:deadsnakes/ppa -y && apt-get install python3.10 -y && apt-get install python3.10-venv -y

# install powershell
RUN wget -q https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN apt-get update && apt-get install -y powershell
RUN rm packages-microsoft-prod.deb

# install .NET
RUN wget https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh && chmod 777 ./dotnet-install.sh
RUN bash ./dotnet-install.sh
RUN bash ./dotnet-install.sh -c 3.1
RUN bash ./dotnet-install.sh --version 7.0.101
ENV DOTNET_ROOT=/root/.dotnet PATH=/root/.dotnet:$PATH
RUN rm /dotnet-install.sh

# install git
RUN add-apt-repository ppa:git-core/ppa -y && apt update && apt upgrade -y && apt install git -y
RUN git config --global credential.helper store && git config --global core.fileMode false
# install depended packages
RUN pip3 install --upgrade wheel PyYAML requests
RUN npm install -g typescript
RUN npm install -g @microsoft/rush
RUN npm install -g autorest

RUN mkdir "/mock-host" && cd /mock-host && npm install @azure-tools/mock-service-host@0.1.16

# install chrome
RUN curl -LO https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb
RUN DEBIAN_FRONTEND=noninteractive apt-get install -y ./google-chrome-stable_current_amd64.deb
RUN rm google-chrome-stable_current_amd64.deb
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true PUPPETEER_EXECUTABLE_PATH=/usr/bin/google-chrome-stable

# install vscode code server and extensions
COPY scripts/install-vscode-server.sh /install-vscode-server.sh
RUN sh /install-vscode-server.sh

# install docker because it's required by test proxy
RUN curl -fsSL https://test.docker.com -o docker.sh && sh docker.sh

COPY packages/sdk-generation-cli/*.tgz /pack.tgz
RUN npm install -g /pack.tgz

COPY scripts/entrypoint.sh /entrypoint.sh
COPY scripts/rerun-tasks /usr/bin/rerun-tasks
COPY scripts/change-owner.sh /change-owner.sh

ENTRYPOINT ["bash", "/entrypoint.sh"]
