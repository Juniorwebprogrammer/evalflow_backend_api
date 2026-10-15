# evalflow_backend_api

API de EvalFlow: .NET 10, minimal APIs con vertical slices (MediatR), EF Core sobre PostgreSQL (Neon en producción) y pruebas con xUnit (`dotnet test`). La configuración y los secretos están en `.env.example`.

## Cuentas y planes: Vexxen

EvalFlow no guarda contraseñas: las cuentas, la verificación del email, el 2FA y los planes son de **Vexxen**. El backend habla con Vexxen con `IVexxenClient` (`Infrastructure/Vexxen`), usando el cliente de EvalFlow (`Vexxen__ClientId`, el mismo que usa el popup del frontend) y su API key (`Vexxen__ApiKey`). Sin esa configuración la API no arranca.

### Sesión

| Endpoint | Qué hace |
| --- | --- |
| `POST /Auth/vexxen` `{ accessToken }` | El access token del popup de Vexxen. Si la persona pertenece a una empresa: `{ status: "signed_in", jwt, … }` (el JWT de siempre: `sub`, `email`, `identificationId`, `role`). Si no: `{ status: "needs_onboarding", email, hasPlan, catalogUrl }`. |
| `POST /Auth/refresh` `{ refreshToken }` | Renueva la sesión **en Vexxen** con su refresh token (el frontend lo guarda en `ef_refresh`) y devuelve un JWT nuevo y el refresh token nuevo (el anterior deja de valer). Si cambia la contraseña o se borra la cuenta en Vexxen, la sesión de EvalFlow termina. |
| `POST /Auth/logout` `{ refreshToken }` | Revoca el refresh token en Vexxen. Siempre 204. |

- El nombre y el email se copian de Vexxen en cada inicio de sesión.
- Errores con `code`: `INVALID_VEXXEN_SESSION` (401), `NOT_A_MEMBER` (401), `USER_INACTIVE` (403), `INVITATION_EXPIRED` (403), `VEXXEN_UNAVAILABLE` (503).
- Los flujos antiguos (login con contraseña, 2FA, verificación del email, recuperar/cambiar contraseña, `PUT /Profile/update` y el `accept-invite` con contraseña) responden **410 `USE_VEXXEN`**.

### Empresas, invitaciones y planes

- **Crear empresa** (`POST /Onboarding/register-owner` con `vexxenAccessToken` y los datos de la empresa): el owner es el usuario de Vexxen y el plan, el que tenga de EvalFlow en Vexxen (o su prueba de 14 días). Sin plan: 409 `NO_PLAN` con `catalogUrl`. El owner queda como **usuario de facturación** (`Companies.BillingVexxenUserId`).
- **Invitar** (`POST /Team/invite`): crea el empleado sin cuenta de Vexxen y le envía el enlace. `GET /Team/invitation/{token}` da la empresa y el email para la pantalla de aceptar. Se acepta **entrando con Vexxen con ese email**: `/Auth/vexxen` enlaza la cuenta (si la invitación no ha caducado).
- **Plan de la empresa**: se copia del usuario de facturación (`getAccess` de Vexxen) al entrar, al renovar la sesión (como mucho cada 5 min) y al crear la empresa (`PlanSyncService`). Planes de Vexxen → EvalFlow: `starter` → Starter (1), `professional` → Professional (2), `enterprise` → Enterprise (3). Estados (`Companies.PlanStatus`): `trialing`, `active`, `canceling`, `past_due` (con acceso) o `none`. Si Vexxen no responde, se mantiene el último estado conocido.
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
