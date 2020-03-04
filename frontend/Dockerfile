FROM node:13-alpine

RUN mkdir -p /usr/src/app
WORKDIR /usr/src/app

COPY package.json ./
COPY package-lock.json ./

RUN apk add --no-cache --virtual .build-deps python make g++
RUN npm ci
RUN apk del .build-deps

COPY . .

EXPOSE 3001

CMD ["npm", "run", "start"]