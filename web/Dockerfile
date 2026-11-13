# Production image for the web app: static files + a proxy that forwards /api to API_URL.
FROM node:22.23.5-alpine3.24 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27.5-alpine3.21
# nginx fills ${API_URL} (and only it) into the template at start.
ENV NGINX_ENVSUBST_FILTER="API_URL|PORT"
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
