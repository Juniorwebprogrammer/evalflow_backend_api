#!/usr/bin/env bash
# Migra la base de datos de un proyecto Neon a otro (p. ej. Londres → Frankfurt).
#
#   1. Comprueba que el destino está vacío y que las versiones de Postgres son compatibles.
#   2. Hace un backup del origen en ./backups/ (formato custom de pg_dump).
#   3. Lo restaura en el destino en una sola transacción (o todo o nada).
#   4. Compara el número de filas de cada tabla entre origen y destino.
#
# No toca el origen: si algo sale mal, la base antigua sigue intacta.
#
# Uso: ./scripts/migrate-neon.sh [--force]
#   --force  restaura aunque el destino ya tenga tablas (pueden aparecer conflictos).
#
# Variables en .env:
#   NEON_DATABASE_URL         origen  (postgresql://usuario:pass@host/db?sslmode=require)
#   NEON_TARGET_DATABASE_URL  destino (mismo formato)
# Usa los endpoints directos (sin "-pooler" en el host): pg_dump/pg_restore no van bien
# a través de PgBouncer. Si pasas uno con "-pooler", el script lo cambia por el directo.
set -euo pipefail

cd "$(dirname "$0")/.."
set -a; source .env; set +a

FORCE=false
[ "${1:-}" = "--force" ] && FORCE=true

: "${NEON_DATABASE_URL:?Falta NEON_DATABASE_URL (origen) en .env}"
: "${NEON_TARGET_DATABASE_URL:?Falta NEON_TARGET_DATABASE_URL (destino) en .env}"

# Endpoint directo en vez del pooler (ep-xxx-pooler.region... → ep-xxx.region...).
SRC_URL="${NEON_DATABASE_URL/-pooler./.}"
DST_URL="${NEON_TARGET_DATABASE_URL/-pooler./.}"

if [ "$SRC_URL" = "$DST_URL" ]; then
  echo "❌ Origen y destino son la misma base de datos." >&2
  exit 1
fi

host_of() { printf '%s' "$1" | sed -E 's#^[a-z]+://[^@]*@([^/:?]+).*#\1#'; }
echo "🔎 Origen : $(host_of "$SRC_URL")"
echo "🔎 Destino: $(host_of "$DST_URL")"

# Ejecuta un cliente de Postgres en un contenedor. Las URLs (con contraseña) viajan como
# variables de entorno, no como argumentos, para que no aparezcan en `ps` ni en el historial.
pg() {
  local image=$1; shift
  docker run --rm -i --user "$(id -u):$(id -g)" \
    -e SRC_URL="$SRC_URL" -e DST_URL="$DST_URL" \
    -v "$PWD/backups:/backups" \
    "$image" "$@"
}

mkdir -p backups

# --- 1. Versiones ---------------------------------------------------------------
# psql de cualquier versión reciente puede consultar la versión del servidor.
server_major() {
  pg postgres:17-alpine sh -c "psql \"\$$1\" -XAtq -c 'SHOW server_version_num'" | cut -c1-2
}
SRC_MAJOR=$(server_major SRC_URL)
DST_MAJOR=$(server_major DST_URL)
echo "🐘 Postgres origen: $SRC_MAJOR · destino: $DST_MAJOR"

if [ "$DST_MAJOR" -lt "$SRC_MAJOR" ]; then
  echo "❌ El destino ($DST_MAJOR) es más antiguo que el origen ($SRC_MAJOR). Crea el proyecto Neon con Postgres $SRC_MAJOR o superior." >&2
  exit 1
fi

# pg_dump tiene que ser de la misma versión que el servidor de origen o más nueva.
PG_IMAGE="postgres:${DST_MAJOR}-alpine"

# --- 2. Destino vacío -----------------------------------------------------------
DST_TABLES=$(pg "$PG_IMAGE" sh -c "psql \"\$DST_URL\" -XAtq -c \"SELECT count(*) FROM pg_tables WHERE schemaname = 'public'\"")
if [ "$DST_TABLES" != "0" ] && [ "$FORCE" != "true" ]; then
  echo "❌ El destino ya tiene $DST_TABLES tablas en 'public'. Usa un proyecto nuevo o ejecuta con --force." >&2
  exit 1
fi

echo
echo "⚠️  Las escrituras que se hagan en el origen a partir de ahora NO se copiarán."
echo "   Pausa el servicio en Koyeb (o evita usar la app) hasta terminar y cambiar la conexión."
read -r -p "¿Continuar? [s/N] " answer
[[ "$answer" =~ ^[sS]$ ]] || { echo "Cancelado."; exit 0; }

# --- 3. Backup + restore --------------------------------------------------------
DUMP_FILE="neon-$(date +%Y%m%d-%H%M%S).dump"

echo "📥 Volcando origen → backups/$DUMP_FILE ..."
pg "$PG_IMAGE" sh -c "pg_dump \"\$SRC_URL\" --format=custom --no-owner --no-acl --file=/backups/$DUMP_FILE"

echo "📤 Restaurando en destino (una sola transacción)..."
pg "$PG_IMAGE" sh -c "pg_restore --dbname=\"\$DST_URL\" --no-owner --no-acl --single-transaction --exit-on-error /backups/$DUMP_FILE"

# --- 4. Verificación ------------------------------------------------------------
# Conteo exacto de filas por tabla del esquema public, ordenado, en ambos lados.
COUNT_SQL="SELECT format('SELECT %L AS t, count(*) FROM public.%I', tablename, tablename) FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename \\gexec"
count_rows() {
  pg "$PG_IMAGE" sh -c "psql \"\$$1\" -XAtq -F ' ' -v ON_ERROR_STOP=1" <<< "$COUNT_SQL" | sort
}

echo "🔢 Comparando filas por tabla..."
SRC_COUNTS=$(count_rows SRC_URL)
DST_COUNTS=$(count_rows DST_URL)

if [ "$SRC_COUNTS" != "$DST_COUNTS" ]; then
  echo "❌ Los conteos no coinciden (origen ← | → destino):" >&2
  diff <(echo "$SRC_COUNTS") <(echo "$DST_COUNTS") >&2 || true
  exit 1
fi

echo "$DST_COUNTS" | awk '{ printf "   %-40s %s\n", $1, $2 }'
echo
echo "✅ Migración completada. Backup en backups/$DUMP_FILE"
echo
echo "Siguientes pasos:"
echo "  1. Koyeb → ConnectionStrings__DefaultConnection con el host nuevo (formato Host=...;Database=...)."
echo "  2. .env → NEON_DATABASE_URL = la URL nueva (para sync-db-from-neon.sh)."
echo "  3. Redeploy en Koyeb y comprueba los logs."
echo "  4. Cuando todo funcione unos días, borra el proyecto antiguo de Neon."
