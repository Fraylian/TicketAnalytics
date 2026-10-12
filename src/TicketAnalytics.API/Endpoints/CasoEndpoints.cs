using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.API.Endpoints;

/// <summary>
/// Vista consolidada de un caso: el ticket de mdb + su orden de cambio + el
/// seguimiento de AMBOS. Un ticket y su OC son el mismo trabajo en dos etapas,
/// asi que aqui se ven juntos.
/// </summary>
public static class CasoEndpoints
{
    public static void MapCasoEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/casos").WithTags("Casos (vista consolidada)");

        // Flujo ATÓMICO: todo el formulario "Registrar gestión" en una transacción.
        g.MapPost("/{refNum}/gestion", async (string refNum, TicketAnalytics.Application.Casos.DTOs.GestionCasoRequest req,
            IGestionCasoService svc, CancellationToken ct) =>
        {
            req.RefNum = string.IsNullOrWhiteSpace(req.RefNum) ? refNum : req.RefNum;
            try { return Results.Ok(await svc.RegistrarAsync(req, ct)); }
            catch (ArgumentException ex)
            {
                return Results.Problem(title: "Datos inválidos", detail: ex.Message, statusCode: 400);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                // Causa típica: la base local no tiene las columnas nuevas todavía.
                var raiz = ex.InnerException?.Message ?? ex.Message;
                return Results.Problem(
                    title: "No se pudo guardar en la base",
                    detail: $"La base local rechazó el guardado: {raiz}",
                    statusCode: 400);
            }
            catch (Exception ex)
            {
                // Nunca dejamos al usuario sin explicación de por qué falló.
                return Results.Problem(
                    title: "Error al registrar la gestión",
                    detail: ex.InnerException?.Message ?? ex.Message,
                    statusCode: 500);
            }
        }).WithSummary("Registra seguimiento, avance, proveedor, JIRA y reporte VP en una sola operación.");

        g.MapGet("/{refNum}", async (string refNum, ITicketQueryService tickets, ISeguimientoService seg,
            IProveedorService prov, IJiraService jira, ISlaService sla, AnalyticsContext db, CancellationToken ct) =>
        {
            // 1. Datos principales desde mdb
            var ticket = await tickets.ObtenerPorRefNumAsync(refNum, ct);
            if (ticket is null) return Results.NotFound(new { mensaje = $"El ticket {refNum} no existe en mdb." });

            // 2. Seguimiento del ticket (base local). Puede no existir aun.
            var segTicket = await seg.ObtenerPorRefNumAsync(refNum, ct);

            // 3. Si tiene OC, traer tambien el seguimiento de la OC
            object? segOc = null;
            if (ticket.TieneOrdenCambio && !string.IsNullOrWhiteSpace(ticket.NoOC))
            {
                var s = await db.TicketSeguimientos.AsNoTracking()
                    .Include(x => x.Actualizaciones).Include(x => x.Jiras).Include(x => x.ReportesProveedor)
                    .FirstOrDefaultAsync(x => x.RefNum == ticket.NoOC && x.TipoOrigen == "chg", ct);
                if (s != null)
                    segOc = new
                    {
                        s.SeguimientoId, s.RefNum, s.TipoOrigen,
                        EtapaActual = (int)s.EtapaActual,
                        s.TecnicoResponsable, s.ProximosPasos, s.ProximoSeguimiento, s.Activo,
                        Actualizaciones = s.Actualizaciones.Count,
                        Jiras = s.Jiras.Select(j => new { j.ClaveJira, j.Titulo, j.Url }),
                        Proveedores = s.ReportesProveedor.Select(r => new { r.NumeroCasoExterno, r.FechaReporte, Estado = (int)r.Estado })
                    };
            }

            // 4. SLA con tiempos
            var slaInfo = await sla.SlaDeTicketAsync(refNum, ct);

            // 5. Relaciones del ticket
            var jiras = await jira.DeSeguimientoAsync(refNum, ct);
            var reportes = await prov.ReportesDeSeguimientoAsync(refNum, ct);
            var historial = await seg.HistorialAsync(refNum, ct);

            return Results.Ok(new
            {
                ticket,
                sla = slaInfo,
                seguimiento = segTicket,           // null si aun no tiene: no es error
                seguimientoOrdenCambio = segOc,    // el trabajo que continua bajo la OC
                jiras,
                reportesProveedor = reportes,
                historial
            });
        }).WithSummary("Caso completo: ticket de mdb + OC + seguimiento de ambos + JIRA + proveedores + timeline.");

        // Enlaza un ticket con su OC para que el seguimiento continue ahi
        g.MapPost("/{refNum}/continuar-bajo-oc/{chgRefNum}", async (string refNum, string chgRefNum,
            AnalyticsContext db, CancellationToken ct) =>
        {
            var s = await db.TicketSeguimientos.FirstOrDefaultAsync(x => x.RefNum == refNum && x.TipoOrigen == "cr", ct);
            if (s is null) return Results.NotFound(new { mensaje = $"No existe seguimiento para el ticket {refNum}." });

            s.OrdenCambioRef = chgRefNum;
            s.ContinuaBajoOC = true;
            s.ActualizadoEn = DateTime.UtcNow;
            db.SeguimientoActualizaciones.Add(new Domain.Entities.SeguimientoActualizacion
            {
                SeguimientoId = s.SeguimientoId,
                TipoActualizacion = "Decisión",
                Contenido = $"El trabajo continúa bajo la orden de cambio {chgRefNum}.",
                Autor = "sistema",
                CreadoEn = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { mensaje = $"El ticket {refNum} ahora continúa bajo la OC {chgRefNum}." });
        }).WithSummary("Marca que el trabajo de un ticket continúa bajo una orden de cambio.");
    }
}
