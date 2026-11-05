#!/usr/bin/env bash
# Copies the API's local settings from .env into `dotnet user-secrets`, which are stored
# outside the repository (~/.microsoft/usersecrets) and loaded only in Development.
# Re-run after changing .env.
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ ! -f .env ]]; then
  echo "Missing .env. Create it first: cp .env.example .env" >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source .env
set +a

project="backend/src/Commerce.Api"

set_secret() {
  dotnet user-secrets set --project "$project" "$1" "$2" >/dev/null
}

set_secret "ConnectionStrings:Database" \
  "Host=localhost;Port=${POSTGRES_PORT:-5432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Timeout=5"
set_secret "ObjectStorage:ServiceUrl" "${S3_ENDPOINT}"
set_secret "ObjectStorage:Region" "${S3_REGION}"
set_secret "ObjectStorage:BucketName" "${S3_BUCKET}"
set_secret "ObjectStorage:AccessKey" "${S3_ACCESS_KEY}"
set_secret "ObjectStorage:SecretKey" "${S3_SECRET_KEY}"
set_secret "Cors:AllowedOrigins:0" "${STOREFRONT_URL}"
set_secret "Cors:AllowedOrigins:1" "${ADMIN_URL}"
set_secret "PublicUrls:StorefrontUrl" "${STOREFRONT_URL}"
set_secret "Smtp:Host" "${SMTP_HOST:-localhost}"
set_secret "Smtp:Port" "${SMTP_PORT:-1025}"
set_secret "Smtp:FromAddress" "${SMTP_FROM:-noreply@localhost}"
set_secret "Identity:SeedRoles" "true"

bootstrap="${BOOTSTRAP_ADMIN:-false}"
set_secret "Identity:BootstrapAdmin" "${bootstrap}"
if [[ "${bootstrap}" == "true" ]]; then
  if [[ -z "${ADMIN_EMAIL:-}" || -z "${ADMIN_PASSWORD:-}" ]]; then
    echo "BOOTSTRAP_ADMIN=true requires ADMIN_EMAIL and ADMIN_PASSWORD in .env." >&2
    exit 1
  fi
  set_secret "Identity:AdminEmail" "${ADMIN_EMAIL}"
  set_secret "Identity:AdminPassword" "${ADMIN_PASSWORD}"
fi

echo "API user secrets updated for ${project} (values not shown)."
