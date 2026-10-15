#!/usr/bin/env bash
# Crea un Administrator de EvalFlow (ver README, «Administradores»).
#
# Uso: ./scripts/create-admin.sh <email> <nombre> <apellidos>            → en Neon (NEON_DATABASE_URL del .env)
#      ./scripts/create-admin.sh --local <email> <nombre> <apellidos>    → en el Postgres local de docker compose
set -euo pipefail

cd "$(dirname "$0")/.."
set -a; source .env; set +a

if [ "${1:-}" = "--local" ]; then
  shift
  export ConnectionStrings__DefaultConnection="Host=localhost;Port=${LOCAL_DB_PORT:-5432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
  echo "→ Postgres local"
else
  : "${NEON_DATABASE_URL:?Falta NEON_DATABASE_URL en .env}"
  export ConnectionStrings__DefaultConnection="$NEON_DATABASE_URL"
  echo "→ Neon"
fi

[ $# -ge 3 ] || { echo "Uso: $0 [--local] <email> <nombre> <apellidos>"; exit 1; }

# Sin launchSettings.json: no debe cambiar el entorno ni la base de datos.
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --no-launch-profile -- create-admin "$@" 2> >(grep -v "warning NU1903" >&2)
