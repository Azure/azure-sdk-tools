# Pinned to Node 20 (LTS): botframework-connector transitively depends on
# node-fetch@2.x, whose gzip stream handling is not compatible with Node 22+
# (throws `ERR_STREAM_PREMATURE_CLOSE` on keep-alive'd gzipped responses,
# most visibly when fetching https://login.botframework.com/v1/.well-known/keys
# during Bot Framework JWKS validation → `AuthenticationError: Signing Key
# could not be retrieved` for every incoming Teams activity). Do not bump to
# 22/24 until botframework-connector switches to native fetch.
FROM mcr.microsoft.com/azurelinux/base/nodejs:20

ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

WORKDIR /app

COPY . .

RUN npm install

CMD [ "npm", "run", "start" ]