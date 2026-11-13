#!/bin/sh
# Copy committed PostgreSQL data and the RustFS bucket to a directory outside
# the live volumes. This does not stop the database and is not one atomic
# snapshot across both stores. Mail is not included. Default retention is the
# 7 newest dated directories.
set -eu
root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
destination=${1:?Pass a backup directory that is not a Docker volume}
stamp=$(date -u +%Y%m%dT%H%M%SZ)
target="$destination/$stamp"
mkdir -p "$target"
docker exec sportswear-commerce-postgres-1 sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$target/postgres.dump"
docker run --rm --network container:sportswear-commerce-object-storage-1 \
  -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY -e AWS_DEFAULT_REGION \
  -v "$target:/backup" amazon/aws-cli:2.39.4 \
  s3 sync "s3://${S3_BUCKET:?Set S3_BUCKET}" /backup/rustfs --endpoint-url http://127.0.0.1:9000
# Keep the 7 newest copies. A negative head count is not portable.
kept=0
for dir in $(find "$destination" -mindepth 1 -maxdepth 1 -type d | sort -r); do
  kept=$((kept + 1))
  if [ "$kept" -gt 7 ]; then
    rm -rf "$dir"
  fi
done
printf '%s\n' "$target"
