FROM node:13-alpine

RUN apk add --no-cache --virtual .build-deps python make g++

RUN mkdir -p /usr/src/app && chown -R node:node /usr/src/app

WORKDIR /usr/src/app

COPY --chown=node:node package.json ./
COPY --chown=node:node package-lock.json ./

USER node:node

RUN npm ci

COPY --chown=node:node . .

USER root:root

RUN apk del .build-deps

USER node:node

EXPOSE 3001

CMD ["npm", "run", "start"]