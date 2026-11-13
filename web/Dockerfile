# Production image for the web app: static files + a proxy that forwards /api to API_URL.
FROM node:22.23.5-alpine3.24 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27.5-alpine3.21
# nginx fills ${API_URL}, ${PORT} and ${DNS_RESOLVER} (and only those) into the template at start.
# API_URL is required; PORT and DNS_RESOLVER have defaults.
ENV NGINX_ENVSUBST_FILTER="API_URL|PORT|DNS_RESOLVER" \
    PORT=8000 \
    DNS_RESOLVER=127.0.0.11
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8000
