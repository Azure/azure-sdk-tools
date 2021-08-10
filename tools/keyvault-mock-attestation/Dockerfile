FROM node:14-alpine

WORKDIR /usr/src/app

# Set default environment variables
ENV NODE_ENV production
ENV PORT 5000
ENV HOST 0.0.0.0

COPY package.json ./
COPY package-lock.json ./

RUN npm install

COPY . .

EXPOSE $PORT

CMD ["npm", "run", "start"]