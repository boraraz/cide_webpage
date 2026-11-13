FROM node:24-bookworm-slim AS build
WORKDIR /src
COPY package.json package-lock.json ./
COPY apps/storefront/package.json apps/storefront/package.json
COPY apps/admin/package.json apps/admin/package.json
COPY packages packages
RUN npm ci
COPY apps/storefront apps/storefront
COPY tsconfig.base.json ./
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build --workspace @commerce/storefront

FROM node:24-bookworm-slim
WORKDIR /app
COPY --from=build /src/apps/storefront/dist dist
COPY --from=build /src/apps/storefront/server server
ENV PORT=4173
EXPOSE 4173
CMD ["node", "server/index.mjs"]
