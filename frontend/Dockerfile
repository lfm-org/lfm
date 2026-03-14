FROM node:24.14.0-alpine AS deps
WORKDIR /usr/src/app
COPY package.json package-lock.json ./
RUN npm ci

FROM node:24.14.0-alpine AS builder
WORKDIR /usr/src/app
COPY --from=deps /usr/src/app/node_modules ./node_modules
COPY . .
RUN npm run build

FROM node:24.14.0-alpine AS runner
WORKDIR /usr/src/app
ENV NODE_ENV=production
RUN addgroup --system --gid 1001 nodejs && adduser --system --uid 1001 nextjs
COPY --from=builder /usr/src/app/.next/standalone ./
COPY --from=builder /usr/src/app/.next/static ./.next/static
COPY --from=builder /usr/src/app/public ./public
USER nextjs
EXPOSE 3000
ENV PORT=3000
ENV HOSTNAME="0.0.0.0"
CMD ["node", "server.js"]
