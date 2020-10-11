FROM node:13-alpine as build

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

CMD ["npm", "run", "build"]

FROM nginx:1.19.3-alpine

COPY --from=build /usr/src/app/build/ /usr/share/nginx/html
COPY /nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]