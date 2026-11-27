FROM mcr.microsoft.com/azurelinux/base/nodejs:24

ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

WORKDIR /app

COPY . .

RUN npm ci --omit=dev

CMD [ "npm", "run", "start" ]