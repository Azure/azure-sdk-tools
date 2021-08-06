FROM node:14-alpine

WORKDIR /usr/src/app

ENV NODE_ENV production
ENV PORT 443

COPY package.json ./
COPY package-lock.json ./

RUN npm install

COPY . .

EXPOSE 443

CMD ["npm", "run", "start"]