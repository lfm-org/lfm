FROM node:24.14.0-alpine AS dev

RUN apk add --no-cache --virtual .build-deps python3 make g++

RUN mkdir -p /usr/src/app && chown -R node:node /usr/src/app

WORKDIR /usr/src/app

COPY --chown=node:node package.json ./
COPY --chown=node:node package-lock.json ./

USER node:node

RUN npm ci

USER root:root

RUN apk del .build-deps

USER node:node

EXPOSE 3000

CMD ["npm", "start"]

FROM node:24.14.0-alpine AS build

RUN apk add --no-cache --virtual .build-deps python3 make g++

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

ARG REACT_APP_API_BASE_URL=
ARG REACT_APP_API_SCHEME=
ARG REACT_APP_API_HOST=
ARG REACT_APP_API_PORT=

ENV REACT_APP_API_BASE_URL=${REACT_APP_API_BASE_URL}
ENV REACT_APP_API_SCHEME=${REACT_APP_API_SCHEME}
ENV REACT_APP_API_HOST=${REACT_APP_API_HOST}
ENV REACT_APP_API_PORT=${REACT_APP_API_PORT}

RUN npm run build

FROM nginx:1.23.2-alpine

COPY --from=build /usr/src/app/build/ /usr/share/nginx/html
COPY /nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
