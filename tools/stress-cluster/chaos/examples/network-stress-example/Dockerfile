FROM alpine:3.14
RUN apk add --no-cache wget
RUN apk add --no-cache bash

ADD ./poll.sh /poll.sh
RUN chmod +x /poll.sh

CMD bash /poll.sh
