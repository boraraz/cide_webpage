#!/bin/sh
# Restore a backup into a new Compose project. Never selects the development
# project or its postgres-data and object-storage-data volumes.
set -eu
backup=${1:?Pass the backup directory}
test ! -d "$backup/postgres.dump" || true
test -f "$backup/postgres.dump"
name=sportswear-commerce-restore
docker volume rm "${name}_postgres" "${name}_rustfs" >/dev/null 2>&1 || true
docker volume create "${name}_postgres" >/dev/null
docker volume create "${name}_rustfs" >/dev/null
docker run -d --name "${name}-postgres" \
  -e POSTGRES_USER=restore -e POSTGRES_PASSWORD=restore -e POSTGRES_DB=restore \
  -v "${name}_postgres:/var/lib/postgresql" \
  -p 127.0.0.1:55432:5432 postgres:18-alpine
docker run -d --name "${name}-rustfs" \
  -e RUSTFS_ACCESS_KEY=restore -e RUSTFS_SECRET_KEY=restore-secret \
  -e RUSTFS_ADDRESS=:9000 -v "${name}_rustfs:/data" \
  -p 127.0.0.1:59000:9000 rustfs/rustfs:1.0.4
for _ in 1 2 3 4 5 6 7 8 9 10; do
  docker exec "${name}-postgres" pg_isready -U restore -d restore && break
  sleep 1
done
docker exec -i "${name}-postgres" pg_restore -U restore -d restore --no-owner --exit-on-error < "$backup/postgres.dump"
docker run --rm --network "container:${name}-rustfs" \
  -e AWS_ACCESS_KEY_ID=restore -e AWS_SECRET_ACCESS_KEY=restore-secret -e AWS_DEFAULT_REGION=us-east-1 \
  -v "$backup/rustfs:/backup" amazon/aws-cli:2.39.4 \
  s3 mb s3://restore --endpoint-url http://127.0.0.1:9000 || true
docker run --rm --network "container:${name}-rustfs" \
  -e AWS_ACCESS_KEY_ID=restore -e AWS_SECRET_ACCESS_KEY=restore-secret -e AWS_DEFAULT_REGION=us-east-1 \
  -v "$backup/rustfs:/backup" amazon/aws-cli:2.39.4 \
  s3 sync /backup s3://restore --endpoint-url http://127.0.0.1:9000
printf 'Restored into project %s. Development volumes were not selected.\n' "$name"
