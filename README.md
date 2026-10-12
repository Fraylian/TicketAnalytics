# TicketAnalytics — Backend (migracion de Excel a base de datos MDB)

Reemplaza el flujo basado en archivos Excel por consulta directa a la base de
CA Service Desk Manager (`MDB`), con una base local propia para lo que el
aplicativo genera. Clean Architecture, .NET 8, EF Core 8, SQL Server.

## Cumplimiento de la especificacion

| Requisito | Como se cumple |
|-----------|----------------|
| MDB solo lectura | `SdmReadContext` con NoTracking global; `SaveChanges`/`SaveChangesAsync` lanzan excepcion. |
| Nada de INSERT/UPDATE/DELETE/ALTER/CREATE en MDB | No hay DbSet de escritura ni migraciones sobre MDB. Solo mapeo de tablas existentes. |
| Sin migraciones ni EnsureCreated en MDB | Las migraciones aplican solo a `AnalyticsContext` (base local). |
| Contextos separados | `SdmReadContext` (MDB, RO) y `AnalyticsContext` (local, RW). |
| Windows Authentication | Cadenas con `Trusted_Connection=True`. Sin usuarios/claves en codigo. |
| AsNoTracking | Aplicado en todas las consultas de MDB. |
| Cadenas en configuracion | En `appsettings.json`, no en codigo. |
| OC + tickets unificados | `ObtenerOrdenCambioConTicketsAsync` / `BuscarOrdenesCambioAsync` traen la OC y sus tickets desde el mismo contexto, en una consulta. |
| Logica desacoplada | Servicios/repositorios tras interfaces en Application. |

## Estructura

```
Domain          Entidades, enums, conversion epoch RD.
Application     Interfaces, DTOs (incluye OrdenCambioConTicketsDto), filtro maestro.
Infrastructure  Los dos DbContext, servicios de consulta, migraciones (solo base local).
API             Minimal API + Swagger.
```

## Las dos conexiones (Windows Authentication)

Ambas en `LOVSRVDQCONS\MSSQLDEV15`, en `appsettings.json`:

- `SdmConnection` -> `Database=MDB` (solo lectura; incluye `ApplicationIntent=ReadOnly`).
- `AnalyticsConnection` -> `Database=TicketAnalytics` (se crea con migraciones).

No hay usuarios ni contraseñas: se usa la identidad de Windows del proceso.
Asegurate de que esa identidad tenga **solo lectura** sobre MDB.

## Puesta en marcha

```
dotnet restore
dotnet build
dotnet run --project src/TicketAnalytics.API
```
La base local se crea/actualiza sola al arrancar (migraciones). Swagger en
`https://localhost:7100/swagger`.

Para aplicar migraciones manualmente:
```
dotnet ef database update --project src/TicketAnalytics.Infrastructure --startup-project src/TicketAnalytics.API
```

## Endpoints

| Metodo | Ruta | Descripcion |
|--------|------|-------------|
| POST | `/api/tickets/buscar` | Tickets enriquecidos con todos los filtros. |
| GET | `/api/tickets/{refNum}` | Detalle de ticket con OC y SLA. |
| GET | `/api/ordenes-cambio/{chgRefNum}` | **OC con TODOS sus tickets** (consulta unificada). |
| POST | `/api/ordenes-cambio/buscar` | OCs del alcance con sus tickets. |
| POST | `/api/dashboard/volumen` | Volumen por estado. |
| POST | `/api/dashboard/sla` | Cumplimiento SLA. |
| POST | `/api/dashboard/productividad` | Productividad por analista/grupo. |
| POST | `/api/dashboard/tendencias/{dia\|semana\|mes}` | Tendencias. |
| POST | `/api/dashboard/cruce-oc` | Cruce Req<->OC, caso critico. |
| POST | `/api/admin/complejidad` | Asigna Baja/Media/Alta a una OC (base local). |
| POST | `/api/admin/snapshots/capturar` | Snapshots + trazabilidad. |

## Que cambia respecto al flujo Excel

- El cargador de Excel deja de ser la fuente. Los servicios de consulta leen de MDB.
- ClosedXML (si lo mantienes) pasa a ser solo **exportador** de reportes, no fuente.
- OC y tickets ya NO se procesan como archivos separados: se obtienen juntos por la
  relacion nativa `call_req.change -> chg.id`.

## Pendiente antes de produccion

- Agregar los 2 persid de **Problema/Error** (Incidentes) a `CategoriaAlcance`
  (seed comentado en la migracion). Ejecuta y confirma:
  ```sql
  SELECT persid, sym FROM MDB.dbo.prob_ctg
  WHERE sym LIKE 'Tecnología de la Información.Problema/Error.Sistemas.%';
  ```
- Verificar que la identidad Windows del servicio sea solo lectura en MDB.
- Conciliar Excel vs MDB en paralelo antes de apagar el Excel.

---

## Frontend (TicketAnalytics.Web) — Blazor WASM + MudBlazor

Interfaz web con el branding del banco (azul #15528E, verde #76B82A). Consume la API.

### Pantallas

- **Dashboard** (`/`) — KPIs (total tickets, % SLA, con OC, caso crítico), volumen por estado, cumplimiento SLA por categoría, tendencias diaria/semanal/mensual (gráfico de líneas), y productividad por analista.
- **Tickets** (`/tickets`) — Tabla paginada del lado del servidor con todos los filtros, detalle en diálogo, y exportación a CSV. Las filas de "req. cerrado / OC pendiente" se resaltan.
- **Órdenes de cambio** (`/ordenes-cambio`) — Vista unificada: cada OC en un panel expandible que despliega **todos sus tickets relacionados**. Es el reemplazo directo del flujo de Excel separados.

### Puesta en marcha del frontend

1. Ajusta la URL de la API en `src/TicketAnalytics.Web/wwwroot/appsettings.json` (`ApiBaseUrl`), por defecto `https://localhost:7100/`.
2. Ejecuta:
   ```
   dotnet run --project src/TicketAnalytics.Web
   ```
   Abre en `https://localhost:7001`.
3. La API debe estar corriendo en paralelo (`https://localhost:7100`). El CORS ya está configurado para el puerto del frontend.

### Correr API + Front juntos

Desde dos terminales:
```
dotnet run --project src/TicketAnalytics.API
dotnet run --project src/TicketAnalytics.Web
```
O configura ambos como proyectos de inicio en Visual Studio (Set Startup Projects → Multiple).

---

## Etapa 2 — Plataforma de seguimiento operativo

Amplía TicketAnalytics de "consulta de tickets" a plataforma de gestión operativa.

### Backend nuevo

**SLA con tiempos** (`/api/sla`) — cálculo real de horas transcurridas, restantes, % consumido y vencimiento, cruzando `attached_sla.time_to_violation`. Endpoints: resumen, próximos a vencer (80%+), vencidos, y SLA por ticket.

**Métricas por analista** (`/api/analistas`) — productividad dinámica según rango: recibidos/cerrados por día (sobre días con actividad real), promedio de horas, OC, y carga de seguimiento. Calcula por UUID de assignee.

**Seguimiento operativo** (`/api/seguimiento`) — el ticket tiene una etapa operativa propia (En análisis, En QA, Pendiente de pase, En espera del proveedor, etc.) independiente del estado en mdb. Cada avance queda en un **historial cronológico auditable** (`SeguimientoActualizacion`) con autor, fecha, tipo y cambio de etapa. El trabajo continúa aunque el ticket cierre en mdb, y puede seguir bajo una OC (`ContinuaBajoOC` + `OrdenCambioRef`).

**Proveedores y JIRA** (`/api/proveedores`, `/api/jira`) — reportar un ticket a un proveedor con caso externo y seguimiento propio; detectar reportes que llevan días sin respuesta; vincular casos de JIRA a un ticket.

**Evidencias** — metadatos de documentos/adjuntos por seguimiento (la entidad está lista; el endpoint de subida se puede añadir según dónde vivan los binarios).

### Frontend nuevo (7 pantallas)

- **Monitor SLA** (`/sla`) — KPIs de cumplimiento, vencidos y próximos a vencer, con barras de progreso por ticket.
- **Seguimiento** (`/seguimiento`) — tarjetas por ticket con su etapa, contadores (JIRA/proveedor/evidencias), creación de seguimiento, registro de actualizaciones y **timeline auditable** en diálogo.
- **Analistas** (`/analistas`) — productividad en tres pestañas: tickets, OC y seguimiento operativo.
- **Proveedores** (`/proveedores`) — catálogo y panel de reportes que necesitan seguimiento.

### Nueva migración

`20260714000000_Seguimiento` crea 6 tablas: `TicketSeguimiento`, `SeguimientoActualizacion`, `JiraRelacion`, `Proveedor`, `ReporteProveedor`, `Evidencia`. Se aplica sola al arrancar la API (sobre la base local).

### Flujo típico

1. Un ticket entra por mdb → aparece en Tickets.
2. El analista crea su **seguimiento** y le asigna etapa.
3. Registra **actualizaciones** conforme avanza (cada una al timeline).
4. Si escala a proveedor, crea un **reporte de proveedor**; si abre JIRA, lo **vincula**.
5. Si el ticket cierra en mdb pero el trabajo sigue, el seguimiento **continúa bajo la OC**.
6. El VP ve todo en Dashboard, SLA, Analistas y Seguimiento.
