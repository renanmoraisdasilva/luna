FROM node:22-alpine AS dependencies
WORKDIR /app
RUN npm install -g npm@12.0.2
COPY src/Frontend/package*.json ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
RUN npm install -g npm@12.0.2
COPY --from=dependencies /app/node_modules ./node_modules
COPY src/Frontend/ .
ARG IDENTITY_API_INTERNAL_URL
ARG CATALOG_API_INTERNAL_URL
ARG ORDERS_API_INTERNAL_URL
ARG PAYMENTS_API_INTERNAL_URL
ARG INVENTORY_API_INTERNAL_URL
ARG SHIPPING_API_INTERNAL_URL
ARG NEXT_PUBLIC_SIGNOZ_URL
ENV IDENTITY_API_INTERNAL_URL=$IDENTITY_API_INTERNAL_URL
ENV CATALOG_API_INTERNAL_URL=$CATALOG_API_INTERNAL_URL
ENV ORDERS_API_INTERNAL_URL=$ORDERS_API_INTERNAL_URL
ENV PAYMENTS_API_INTERNAL_URL=$PAYMENTS_API_INTERNAL_URL
ENV INVENTORY_API_INTERNAL_URL=$INVENTORY_API_INTERNAL_URL
ENV SHIPPING_API_INTERNAL_URL=$SHIPPING_API_INTERNAL_URL
ENV NEXT_PUBLIC_SIGNOZ_URL=$NEXT_PUBLIC_SIGNOZ_URL
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
RUN npm install -g npm@12.0.2
ENV NODE_ENV=production
ENV PORT=3000
COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY infrastructure/docker-compose.prod.yml /opt/luna-deployment/docker-compose.prod.yml
COPY infrastructure/docker-compose.observability.yml /opt/luna-deployment/docker-compose.observability.yml
COPY infrastructure/observability /opt/luna-deployment/observability
COPY infrastructure/update.sh /opt/luna-deployment/update.sh
RUN chown -R node:node /app /opt/luna-deployment
USER node
EXPOSE 3000
CMD ["node", "server.js"]
