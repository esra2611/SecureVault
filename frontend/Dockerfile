FROM node:20-alpine AS build
# Build-time: client bundle gets this API URL (browser calls host:8080). Override with --build-arg if needed.
ARG NUXT_PUBLIC_API_BASE=http://localhost:8080
ENV NUXT_PUBLIC_API_BASE=$NUXT_PUBLIC_API_BASE
WORKDIR /app

# Copy dependency manifests first for cache-friendly, reproducible install
COPY frontend/package*.json ./
# Prefer npm ci when lockfile exists; fallback to npm install only if lock is missing
RUN if [ -f package-lock.json ]; then npm ci; else npm install; fi

# Copy only Nuxt app sources (context is repo root; .dockerignore excludes node_modules, .nuxt, .output, etc.)
COPY frontend/ .
RUN npm run build

FROM node:20-alpine
RUN addgroup -S -g 10001 app && adduser -S -G app -u 10001 app
WORKDIR /app
# Listen on all interfaces so container is reachable from host
ENV HOST=0.0.0.0
ENV PORT=3000
COPY --from=build --chown=app:app /app/.output .output
RUN chown -R app:app /app
USER app
EXPOSE 3000
CMD ["node", ".output/server/index.mjs"]