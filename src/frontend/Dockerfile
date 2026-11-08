# syntax=docker/dockerfile:1
# OPS-01. Build context: the repository root (docker compose sets it): `tsc -b` also typechecks the contract-drift tests,
# which import contracts/openapi.json, so the repo layout is kept. Serves the SPA over HTTPS with nginx, which proxies
# /api and /hubs to the API.
FROM node:22-alpine AS build
WORKDIR /repo/src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY contracts/ /repo/contracts/
COPY src/frontend/ ./
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.27-alpine AS web
USER root
RUN apk add --no-cache openssl && install -d -o nginx -g nginx /etc/nginx/certs
USER nginx
COPY src/frontend/docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/frontend/docker/flowops-proxy.conf /etc/nginx/flowops-proxy.conf
COPY --chmod=755 src/frontend/docker/40-flowops-tls.sh /docker-entrypoint.d/
COPY --from=build /repo/src/frontend/dist /usr/share/nginx/html
EXPOSE 8443
