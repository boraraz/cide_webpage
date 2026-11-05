#!/bin/sh
# Idempotently creates the media bucket in the local S3-compatible service.
set -eu

if aws --endpoint-url "$S3_INTERNAL_ENDPOINT" s3api head-bucket --bucket "$S3_BUCKET" >/dev/null 2>&1; then
  echo "Bucket '$S3_BUCKET' already exists."
else
  aws --endpoint-url "$S3_INTERNAL_ENDPOINT" s3api create-bucket --bucket "$S3_BUCKET" >/dev/null
  echo "Created bucket '$S3_BUCKET'."
fi
