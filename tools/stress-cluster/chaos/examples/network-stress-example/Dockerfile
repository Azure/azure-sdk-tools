FROM mcr.microsoft.com/dotnet/sdk:6.0.202-alpine3.14-amd64
RUN apk add --no-cache wget
RUN apk add --no-cache bash

ADD ./poll.sh /poll.sh
RUN chmod +x /poll.sh

CMD bash /poll.sh
