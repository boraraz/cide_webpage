FROM node:24-bookworm-slim AS build
WORKDIR /src
COPY package.json package-lock.json ./
COPY apps/admin/package.json apps/admin/package.json
COPY apps/storefront/package.json apps/storefront/package.json
COPY packages packages
RUN npm ci
COPY apps/admin apps/admin
COPY tsconfig.base.json ./
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
ENV ADMIN_BASE=/admin/
RUN npm run build --workspace @commerce/admin

FROM node:24-bookworm-slim
WORKDIR /app
RUN npm install -g serve@14
COPY --from=build /src/apps/admin/dist dist
ENV PORT=4174
EXPOSE 4174
CMD ["serve", "-s", "dist", "-l", "4174", "--no-port-switching"]
