#!/usr/bin/env bash
# Lanzamiento de EvalFlow con Vexxen: copia de seguridad de Neon y vaciado de los datos.
# Las cuentas pasan a Vexxen y las de ahora no tienen cuenta de Vexxen enlazada, así que se empieza de cero.
# Se conserva la configuración: Plans, Roles, RoleFeatures, el historial de migraciones y la tabla antigua "Plan".
#
# Uso: ./scripts/reset-for-vexxen-launch.sh          (pide confirmación; NEON_DATABASE_URL en .env)
# Ejecutarlo ANTES de desplegar el backend nuevo (que aplica las migraciones al arrancar).
set -euo pipefail

cd "$(dirname "$0")/.."
set -a; source .env; set +a
: "${NEON_DATABASE_URL:?Falta NEON_DATABASE_URL en .env}"

TABLES='"AiEvaluationAnalyses", "Answers", "ClarificationRequests", "Companies", "Departments", "DiscrepancyAcceptances",
  "EmailOutboxMessages", "EvaluationCycleTemplate", "EvaluationCycles", "EvaluationResults", "EvaluationSubmissions",
  "JobPositions", "Questions", "TemplateFavoriteLists", "TemplateTemplateFavoriteList", "TemplateUser", "Templates",
  "UserAvatars", "Users"'

docker compose up -d --wait evalflow-db
psql_neon() { docker compose exec -T evalflow-db psql "$NEON_DATABASE_URL" -v ON_ERROR_STOP=1 "$@"; }

echo "Datos que se van a borrar:"
psql_neon -c 'select (select count(*) from "Companies") as empresas, (select count(*) from "Users") as usuarios,
  (select count(*) from "EvaluationCycles") as ciclos, (select count(*) from "EvaluationSubmissions") as envios;'

read -r -p "Escribe VACIAR para hacer la copia y vaciar Neon: " answer
[ "$answer" = "VACIAR" ] || { echo "Cancelado."; exit 1; }

mkdir -p backups
backup="backups/neon-before-vexxen-$(date +%Y%m%d-%H%M%S).sql"
echo "💾 Copia de seguridad → $backup"
# pg_dump dentro del contenedor (misma versión que Postgres 18). Para restaurar: psql "$NEON_DATABASE_URL" < "$backup"
docker compose exec -T evalflow-db pg_dump "$NEON_DATABASE_URL" --no-owner --no-acl > "$backup"
[ -s "$backup" ] || { echo "La copia está vacía: no se vacía nada."; exit 1; }

echo "🗑️  Vaciando las tablas de datos…"
psql_neon -c "BEGIN; TRUNCATE TABLE $TABLES RESTART IDENTITY CASCADE; COMMIT;"

psql_neon -c 'select (select count(*) from "Companies") as empresas, (select count(*) from "Users") as usuarios,
  (select count(*) from "Plans") as planes, (select count(*) from "Roles") as roles;'
echo "✅ Neon vacío (configuración conservada). Copia: $backup"
