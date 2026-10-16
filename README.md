# evalflow_backend_api

API de EvalFlow: .NET 10, minimal APIs con vertical slices (MediatR), EF Core sobre PostgreSQL (Neon en producción) y pruebas con xUnit (`dotnet test`). La configuración y los secretos están en `.env.example`.

## Cuentas y planes: Vexxen

EvalFlow no guarda contraseñas: las cuentas, la verificación del email, el 2FA y los planes son de **Vexxen**. El backend habla con Vexxen con `IVexxenClient` (`Infrastructure/Vexxen`), usando el cliente de EvalFlow (`Vexxen__ClientId`, el mismo que usa el popup del frontend) y su API key (`Vexxen__ApiKey`). Sin esa configuración la API no arranca.

### Sesión

| Endpoint | Qué hace |
| --- | --- |
| `POST /Auth/vexxen` `{ accessToken }` | El access token del popup de Vexxen. Si la persona pertenece a una empresa: `{ status: "signed_in", jwt, … }` (el JWT de siempre: `sub`, `email`, `identificationId`, `role`). Si no: `{ status: "needs_onboarding", email, hasPlan, catalogUrl }`. |
| `POST /Auth/refresh` `{ refreshToken }` | Renueva la sesión **en Vexxen** con su refresh token (el frontend lo guarda en `ef_refresh`) y devuelve un JWT nuevo y el refresh token nuevo (el anterior deja de valer). Si cambia la contraseña, se suspende o se borra la cuenta en Vexxen, la sesión de EvalFlow termina (y el refresh token nuevo se revoca). |
| `POST /Auth/logout` `{ refreshToken }` | Revoca el refresh token en Vexxen. Siempre 204. |

- El nombre y el email se copian de Vexxen en cada inicio de sesión.
- Antes de emitir el JWT (inicio de sesión, renovación y alta de la empresa) se pregunta a Vexxen si la cuenta sigue activa (`GET /api/access/{userId}`): suspendida → `ACCOUNT_SUSPENDED` (403 al entrar, 401 al renovar); borrada → `INVALID_VEXXEN_SESSION`. Si Vexxen no contesta a esa comprobación, la sesión sigue (queda un aviso en el log).
- Errores con `code`: `INVALID_VEXXEN_SESSION` (401), `NOT_A_MEMBER` (401), `ACCOUNT_SUSPENDED` (403 / 401), `USER_INACTIVE` (403), `INVITATION_EXPIRED` (403), `VEXXEN_UNAVAILABLE` (503).
- Los flujos antiguos (login con contraseña, 2FA, verificación del email, recuperar/cambiar contraseña, `PUT /Profile/update` y el `accept-invite` con contraseña) responden **410 `USE_VEXXEN`**.

### Empresas, invitaciones y planes

- **Crear empresa** (`POST /Onboarding/register-owner` con `vexxenAccessToken` y los datos de la empresa): el owner es el usuario de Vexxen y el plan, el que tenga de EvalFlow en Vexxen (o su prueba de 14 días). Sin plan: 409 `NO_PLAN` con `catalogUrl`. El owner queda como **usuario de facturación** (`Companies.BillingVexxenUserId`).
- **Invitar** (`POST /Team/invite`): crea el empleado sin cuenta de Vexxen y le envía el enlace. `GET /Team/invitation/{token}` da la empresa y el email para la pantalla de aceptar. Se acepta **entrando con Vexxen con ese email**: `/Auth/vexxen` enlaza la cuenta (si la invitación no ha caducado).
- **Plan de la empresa**: se copia del usuario de facturación (`getAccess` de Vexxen) al entrar, al renovar la sesión (como mucho cada 5 min) y al crear la empresa (`PlanSyncService`). Planes de Vexxen → EvalFlow: `starter` → Starter (1), `professional` → Professional (2), `enterprise` → Enterprise (3). Estados (`Companies.PlanStatus`): `trialing`, `active`, `canceling`, `past_due` (con acceso) o `none`. Si Vexxen no responde, se mantiene el último estado conocido.
- **Traspaso de la facturación**: el usuario de facturación transfiere su suscripción desde Vexxen a otra cuenta, que la acepta en su panel. En la siguiente sincronización, `getAccess` del anterior trae `transferredTo`: si esa persona es miembro activo de la empresa, pasa a usuario de facturación y **Owner**, y el Owner anterior queda como **RRHH**; si no es miembro, la empresa se queda sin plan (solo lectura). El catálogo de Vexxen avisa de que tiene que ser alguien de la empresa.
- **Sin plan (`none`) la empresa es de solo lectura**: las escrituras responden 403 `PLAN_INACTIVE` (`PlanAccessMiddleware`). Siguen disponibles la sesión, el onboarding, el perfil, borrar la empresa y el rol Administrator.
- **Enterprise** lo asigna a mano un Administrator: `PUT /Company/{identificationId}/plan` `{ planId }` lo marca como manual (la sincronización no lo toca); con `{ planId: null }` vuelve a la suscripción de Vexxen.
- `GET /Company/plan` incluye `status` (estado, fechas de fin y `manageUrl`, el catálogo de Vexxen) para los avisos del frontend.
- **Borrar** la cuenta se confirma con el email y la empresa con su nombre. Quien paga no puede borrar su cuenta (409 `BILLING_OWNER`): tiene que borrar la empresa. La suscripción de Vexxen no se toca: se cancela desde Vexxen.

### Administradores (personal de EvalFlow)

El rol `Administrator` es global: asigna planes a mano (Enterprise) a cualquier empresa y la solo lectura no le afecta. Se crean con un comando de la propia API, contra la base de datos de `ConnectionStrings__DefaultConnection` (local o Neon):

```bash
dotnet run -- create-admin <email> <nombre> <apellidos>
```

- Como todo usuario necesita empresa, los administradores viven en una interna (`IdentificationId` `evalflow-internal`, Enterprise, plan manual, sin usuario de facturación), que se crea la primera vez.
- Queda como una invitación: se enlaza la primera vez que esa persona entra en EvalFlow con Vexxen usando ese email (necesita una cuenta de Vexxen con él). Caduca a los 30 días; repetir el comando la renueva.
- Rechaza un email que ya pertenece a una empresa cliente (un email solo puede estar en un sitio).

## Admin API (Vexxen Administrator)

`/admin/v1` es la Admin API que lee y usa **Vexxen Administrator** (la app de escritorio), según el contrato v1.2 (`docs/admin-contract-v1.md` del repo vexxen-administrator). `app.id` = `evalflow`.

| Endpoint | Qué hace |
| --- | --- |
| `GET /admin/v1/manifest` | Recursos, campos, filtros y relaciones. Las acciones y `edit` solo aparecen si el token puede escribir. |
| `GET /admin/v1/health` | Siempre 200; el estado (`ok` / `down`) y la latencia de la base de datos van en el cuerpo. |
| `GET /admin/v1/stats` | Empresas (total, por plan y sin plan), usuarios, ciclos activos, evaluaciones completadas en 30 días y emails fallidos/pendientes. |
| `GET /admin/v1/{resource}` | Lista con `page`, `pageSize` (máx. 100), `q`, `sort` y los filtros del manifest. Un filtro u orden desconocido es `400 invalid_query`. |
| `GET /admin/v1/{resource}/{id}` | Ficha: `{ item }`. |
| `POST /admin/v1/{resource}/{id}/actions/{action}` | `{ inputs }` → `{ item }`. Hoy: `companies` / `set-plan` (`planId`: `starter`, `professional`, `enterprise` o `vexxen_sync` para volver a la suscripción de Vexxen), lo mismo que `PUT /Company/{identificationId}/plan`; `email-outbox` / `retry`. |
| `PATCH /admin/v1/{resource}/{id}` | `{ changes }` → `{ item }`, solo con los campos de `edit.inputs`. `companies`: `name`, `cif`, `fiscalAddress`, `sector`. `users`: `role` (`owner`, `rrhh`, `superior`, `employee`; nunca Administrator). `409 conflict` si deja la empresa sin Owner activo o rebaja al Owner que paga la suscripción. |

Recursos: `companies`, `users`, `evaluation-cycles` y `email-outbox`. La empresa interna (`evalflow-internal`) y sus usuarios (los Administrators) no aparecen ni cuentan en las estadísticas. Nunca se devuelven tokens de invitación, el cuerpo cifrado de los emails ni otros secretos.

**Autenticación.** Solo con el access token de Vexxen Administrator (esquema JWT `VexxenAdmin`, aparte del JWT de EvalFlow): ES256, `typ` `at+jwt`, `iss` = `VexxenAdmin:Issuer` (las claves se descubren en `{issuer}/.well-known/openid-configuration`), `aud` = `VexxenAdmin:Audience`, `exp`/`nbf` con 60 s de tolerancia. Después: `403 forbidden` sin scope `admin:read` o sin permisos, `403 unsupported_grant` si algún permiso tiene `tenant`, y las acciones y la edición necesitan además el scope `admin:write` y el rol `operator`. No pide `X-Api-Key` ni usa CORS. El JWT de EvalFlow no abre `/admin/v1` y el token de administración no abre el resto de la API.

Todas las respuestas llevan `Cache-Control: no-store` y `X-Request-Id` (el que llega o uno nuevo); los errores son `application/problem+json` con `code` y `requestId`. Cada petición, acción y edición quedan en el log (`[admin-api]`, con `sub`, email, ruta, query, inputs o, por campo, el valor anterior y el nuevo).

| Variable | Por defecto | Qué es |
| --- | --- | --- |
| `VexxenAdmin__Issuer` | `https://vexxen-home.vercel.app` (`http://localhost:3000` en Development) | El Vexxen que firma los tokens (su `APP_URL`). |
| `VexxenAdmin__Audience` | `evalflow` | El appId de EvalFlow en Vexxen. |
| `VexxenAdmin__Environment` | `production` / `local` según `ASPNETCORE_ENVIRONMENT` | Opcional: el entorno que dice el manifest. |
| `APP_VERSION` | la versión del ensamblado | Opcional: la versión que dicen el manifest y `/health`. |
