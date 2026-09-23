#!/usr/bin/env bash
# Copia la base de datos de Neon (producción) a la PostgreSQL local de docker compose.
# ¡Sobrescribe por completo la DB local! Uso: ./scripts/sync-db-from-neon.sh
set -euo pipefail

cd "$(dirname "$0")/.."
set -a; source .env; set +a

: "${NEON_DATABASE_URL:?Falta NEON_DATABASE_URL en .env}"

docker compose up -d --wait evalflow-db

echo "🗑️  Recreando la base local '$POSTGRES_DB'..."
docker compose exec -T evalflow-db psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 \
  -c "DROP DATABASE IF EXISTS \"$POSTGRES_DB\" WITH (FORCE);" \
  -c "CREATE DATABASE \"$POSTGRES_DB\";"

echo "📥 Volcando Neon → local..."
# pg_dump se ejecuta dentro del contenedor, así su versión coincide con la de la DB local.
docker compose exec -T evalflow-db pg_dump "$NEON_DATABASE_URL" --no-owner --no-acl \
  | docker compose exec -T evalflow-db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -q

echo "✅ Copia completada."
