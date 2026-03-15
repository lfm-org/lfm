FROM mcr.microsoft.com/playwright:v1.47.0-noble
WORKDIR /e2e
COPY app/package.json app/package-lock.json ./
RUN npm ci --ignore-scripts
