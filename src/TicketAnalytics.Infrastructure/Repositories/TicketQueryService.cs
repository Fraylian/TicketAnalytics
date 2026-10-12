using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Persistence.Sdm.Entities;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Consulta de tickets y ordenes de cambio desde MDB (solo lectura) enriquecidos
/// con datos de la base local. Todas las consultas usan AsNoTracking.
/// </summary>
public class TicketQueryService : ITicketQueryService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    private readonly ILogger<TicketQueryService> _logger;

    public TicketQueryService(SdmReadContext sdm, AnalyticsContext analytics, ILogger<TicketQueryService> logger)
    {
        _sdm = sdm;
        _analytics = analytics;
        _logger = logger;
    }

    // ============ BUSQUEDA DE TICKETS ============
    public async Task<PagedResult<TicketEnriquecidoDto>> BuscarAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (q, pagina, tamano) = await ConstruirConsultaTicketsAsync(f, ct);

        // El Count se hace sobre la consulta YA filtrada por completo: TotalCount correcto.
        var total = await q.CountAsync(ct);
        q = OrdenarTickets(q, f);

        var page = await q.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        var dtos = await EnriquecerAsync(page, ct);

        return new PagedResult<TicketEnriquecidoDto> { Items = dtos, TotalCount = total, Page = pagina, PageSize = tamano };
    }

    /// <summary>Todos los tickets que cumplen el filtro, SIN paginar. Para exportaciones.</summary>
    public async Task<List<TicketEnriquecidoDto>> BuscarTodosAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var (q, _, _) = await ConstruirConsultaTicketsAsync(f, ct);
        q = OrdenarTickets(q, f);

        // Traemos en lotes para no materializar decenas de miles de filas de golpe.
        var resultado = new List<TicketEnriquecidoDto>();
        const int lote = 500;
        int saltar = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var bloque = await q.Skip(saltar).Take(lote).ToListAsync(ct);
            if (bloque.Count == 0) break;
            resultado.AddRange(await EnriquecerAsync(bloque, ct));
            if (bloque.Count < lote) break;
            saltar += lote;
            if (saltar >= 50_000) { _logger.LogWarning("Exportación truncada a 50.000 tickets."); break; }
        }
        return resultado;
    }

    /// <summary>
    /// Arma la consulta de tickets con TODOS los filtros aplicados antes de contar/paginar.
    /// Los filtros que dependen de otras tablas (SLA, complejidad de OC, OC pendiente) se
    /// resuelven primero a un conjunto de ids y se incorporan a la consulta principal,
    /// en vez de filtrarse después del enriquecimiento (que rompía TotalCount).
    /// </summary>
    private async Task<(IQueryable<CallReq> Query, int Pagina, int Tamano)> ConstruirConsultaTicketsAsync(
        TicketFiltro f, CancellationToken ct)
    {
        var alcance = await ResolverAlcanceAsync(f, ct);
        var (desde, hasta) = EpochRD.Rango(f.FechaDesde, f.FechaHasta, new DateTime(2026, 1, 1));

        var q = _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta));

        if (f.Estados is { Count: > 0 }) q = q.Where(cr => f.Estados.Contains(cr.status!));
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            q = q.Where(cr => cr.ref_num.Contains(t) || (cr.summary != null && cr.summary.Contains(t)) || (cr.description != null && cr.description.Contains(t)));
        }
        if (f.ConOrdenCambio == true) q = q.Where(cr => cr.change != null && cr.change > 0);
        if (f.ConOrdenCambio == false) q = q.Where(cr => cr.change == null || cr.change == 0);
        if (HexToBytes(f.AsignatarioUuid) is { } asigB) q = q.Where(cr => cr.assignee == asigB);

        // Filtro por NOMBRE del asignatario. Los tickets guardan el UUID binario del
        // contacto, así que resolvemos primero qué contactos coinciden y luego filtramos
        // por sus UUID; se hace aquí para que entre en el Count y en la paginación.
        // El match es por tokens porque el nombre puede venir como "Nombre Apellido"
        // o "Apellido Nombre" según la pantalla desde la que se pida.
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre))
        {
            var tokens = f.AsignatarioNombre
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 2)
                .ToList();

            var uuids = new List<byte[]>();
            if (tokens.Count > 0)
            {
                // Acotamos en SQL con el token más largo y afinamos en memoria.
                var ancla = tokens.OrderByDescending(t => t.Length).First();
                var candidatos = await _sdm.CaContacts.AsNoTracking()
                    .Where(x => (x.first_name ?? "").Contains(ancla)
                             || (x.last_name ?? "").Contains(ancla)
                             || (x.userid ?? "").Contains(ancla))
                    .Select(x => new { x.contact_uuid, x.first_name, x.last_name, x.userid })
                    .ToListAsync(ct);

                uuids = candidatos
                    .Where(x =>
                    {
                        var completo = $"{x.first_name} {x.last_name} {x.userid}";
                        // Con un solo token basta ese; con varios exigimos al menos dos
                        // coincidencias para no traer homónimos parciales.
                        var aciertos = tokens.Count(t => completo.Contains(t, StringComparison.OrdinalIgnoreCase));
                        return tokens.Count == 1 ? aciertos >= 1 : aciertos >= 2;
                    })
                    .Select(x => x.contact_uuid)
                    .ToList();
            }

            if (uuids.Count == 0)
            {
                q = q.Where(cr => false);          // nadie coincide => sin resultados
            }
            else
            {
                var idsAsignados = await q.Where(cr => cr.assignee != null)
                    .Select(cr => new { cr.id, cr.assignee })
                    .ToListAsync(ct);

                // La comparación de byte[] se hace en memoria: SQL Server no traduce
                // bien un IN sobre varbinary desde una lista de arreglos.
                var idsOk = idsAsignados
                    .Where(x => uuids.Any(u => u.SequenceEqual(x.assignee!)))
                    .Select(x => x.id)
                    .ToList();

                q = q.Where(cr => idsOk.Contains(cr.id));
            }
        }
        if (HexToBytes(f.GrupoUuid) is { } grupoB) q = q.Where(cr => cr.group_id == grupoB);

        // --- Filtro por complejidad de la OC (vive en Analytics) ---
        // Resolvemos los chg_ref_num con esa complejidad, los traducimos a chg.id
        // y filtramos los tickets cuyo 'change' apunte a ellos.
        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC))
        {
            // El enum se parsea ANTES de la consulta: EF no puede traducir .ToString() a SQL.
            if (!Enum.TryParse<TicketAnalytics.Domain.Enums.NivelComplejidad>(f.ComplejidadOC, true, out var nivelFiltro))
            {
                _logger.LogWarning("Complejidad de OC no reconocida: {Valor}", f.ComplejidadOC);
                q = q.Where(cr => false);   // filtro inválido => sin resultados, no resultados erróneos
            }
            else
            {
                var refsComplejidad = await _analytics.OrdenCambioComplejidades.AsNoTracking()
                    .Where(o => o.Complejidad == nivelFiltro)
                    .Select(o => o.ChgRefNum).ToListAsync(ct);

                var idsChg = refsComplejidad.Count == 0
                    ? new List<int>()
                    : await _sdm.Changes.AsNoTracking()
                        .Where(c => refsComplejidad.Contains(c.chg_ref_num))
                        .Select(c => c.id).ToListAsync(ct);

                q = q.Where(cr => cr.change != null && idsChg.Contains(cr.change.Value));
            }
        }

        // --- Ticket cerrado pero su OC sigue abierta ---
        if (f.SoloReqCerradoCambioPendiente == true)
        {
            var idsChgAbiertas = await _sdm.Changes.AsNoTracking()
                .Where(c => c.close_date == null || c.close_date == 0)
                .Select(c => c.id).ToListAsync(ct);

            q = q.Where(cr => cr.active_flag == 0 && cr.change != null && idsChgAbiertas.Contains(cr.change.Value));
        }

        // --- Vista por defecto: solo lo que requiere atención (abierto / sin cerrar) ---
        if (f.SoloAbiertos == true)
            q = q.Where(cr => cr.active_flag == 1 || cr.close_date == null || cr.close_date == 0);

        // --- Filtros que dependen de la gestión local (base Analytics) ---
        // Se resuelven a un conjunto de RefNum y se aplican ANTES del Count.
        var filtraPorGestion = f.ReportadoProveedor.HasValue
                            || f.PendienteEscalarProveedor == true
                            || f.Sistema.HasValue
                            || f.TipoSolucion.HasValue
                            || f.TipoActualizacionDatos.HasValue
                            || f.ConCausaRaiz.HasValue
                            || f.ReportadoVP.HasValue;

        if (filtraPorGestion)
        {
            var gq = _analytics.TicketSeguimientos.AsNoTracking().Where(s => s.TipoOrigen == "cr");

            if (f.ReportadoProveedor.HasValue
                && Enum.IsDefined(typeof(EstadoReporteProveedorTicket), f.ReportadoProveedor.Value))
            {
                var est = (EstadoReporteProveedorTicket)f.ReportadoProveedor.Value;
                gq = gq.Where(s => s.ReportadoProveedor == est);
            }

            // Atajo del punto 3: los que todavía requieren escalamiento (No o Pendiente)
            if (f.PendienteEscalarProveedor == true)
                gq = gq.Where(s => s.ReportadoProveedor != EstadoReporteProveedorTicket.Si);

            if (f.Sistema.HasValue && Enum.IsDefined(typeof(SistemaAfectado), f.Sistema.Value))
            {
                var sis = (SistemaAfectado)f.Sistema.Value;
                gq = gq.Where(s => s.Sistema == sis);
            }
            if (f.TipoSolucion.HasValue && Enum.IsDefined(typeof(TipoSolucion), f.TipoSolucion.Value))
            {
                var ts = (TipoSolucion)f.TipoSolucion.Value;
                gq = gq.Where(s => s.TipoSolucion == ts);
            }
            if (f.TipoActualizacionDatos.HasValue
                && Enum.IsDefined(typeof(TipoActualizacionDatos), f.TipoActualizacionDatos.Value))
            {
                var ta = (TipoActualizacionDatos)f.TipoActualizacionDatos.Value;
                gq = gq.Where(s => s.TipoActualizacionDatos == ta);
            }
            if (f.ConCausaRaiz == true) gq = gq.Where(s => s.CausaRaiz != null && s.CausaRaiz != "");
            if (f.ConCausaRaiz == false) gq = gq.Where(s => s.CausaRaiz == null || s.CausaRaiz == "");
            if (f.ReportadoVP.HasValue) gq = gq.Where(s => s.VaReporteVP == f.ReportadoVP.Value);

            var refs = await gq.Select(s => s.RefNum).Distinct().ToListAsync(ct);
            q = q.Where(cr => refs.Contains(cr.ref_num));
        }

        // --- Estado de SLA (SI / NO / EN CURSO / N/A) ---
        // Se calcula con attached_sla; resolvemos los ticket_id que califican y filtramos por id.
        if (!string.IsNullOrWhiteSpace(f.EstadoSla))
        {
            var estado = f.EstadoSla.Trim().ToUpperInvariant();
            var slas = await _sdm.AttachedSlas.AsNoTracking()
                .Where(s => s.ticket_type == "cr" && s.del == 0 && s.time_to_violation != null && s.time_to_violation != 0)
                .Select(s => new { s.ticket_id, s.time_to_violation, s.sla_viol_status })
                .ToListAsync(ct);

            var mapaSla = slas.GroupBy(s => s.ticket_id).ToDictionary(g => g.Key, g => g.First());

            if (estado == "N/A")
            {
                // Sin SLA medible: los que NO están en attached_sla con ttv válido.
                var conSla = mapaSla.Keys.ToList();
                q = q.Where(cr => !conSla.Contains(cr.id));
            }
            else
            {
                // Necesitamos close_date de cada ticket para clasificar; lo hacemos por id.
                var candidatos = await q.Select(cr => new { cr.id, cr.close_date }).ToListAsync(ct);
                var idsOk = candidatos
                    .Where(c => mapaSla.TryGetValue(c.id, out var s)
                                && CalcularSla(c.close_date, s.time_to_violation, s.sla_viol_status)
                                   .Equals(estado, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.id).ToList();

                q = q.Where(cr => idsOk.Contains(cr.id));
            }
        }

        var pagina = f.Page < 1 ? 1 : f.Page;
        var tamano = f.PageSize is < 1 or > 500 ? 25 : f.PageSize;
        return (q, pagina, tamano);
    }

    private static IQueryable<CallReq> OrdenarTickets(IQueryable<CallReq> q, TicketFiltro f)
    {
        var orden = (f.OrderBy ?? "fechaapertura").ToLowerInvariant();
        return (orden, f.OrderDesc) switch
        {
            ("noticket", true) => q.OrderByDescending(cr => cr.ref_num),
            ("noticket", false) => q.OrderBy(cr => cr.ref_num),
            ("fechaapertura", true) => q.OrderByDescending(cr => cr.open_date),
            _ => q.OrderBy(cr => cr.open_date)
        };
    }

    public async Task<TicketEnriquecidoDto?> ObtenerPorRefNumAsync(string refNum, CancellationToken ct = default)
    {
        var cr = await _sdm.CallReqs.AsNoTracking().FirstOrDefaultAsync(x => x.ref_num == refNum, ct);
        if (cr is null) return null;
        return (await EnriquecerAsync(new() { cr }, ct)).FirstOrDefault();
    }

    // ============ ORDEN DE CAMBIO + SUS TICKETS (consulta unificada) ============
    public async Task<OrdenCambioConTicketsDto?> ObtenerOrdenCambioConTicketsAsync(string chgRefNum, CancellationToken ct = default)
    {
        var chg = await _sdm.Changes.AsNoTracking().FirstOrDefaultAsync(c => c.chg_ref_num == chgRefNum, ct);
        if (chg is null) return null;

        // Todos los tickets cuyo 'change' apunta a esta OC (relacion nativa de MDB)
        var tickets = await _sdm.CallReqs.AsNoTracking().Where(cr => cr.change == chg.id).ToListAsync(ct);

        var dto = await ConstruirOcConTicketsAsync(new List<Chg> { chg }, tickets, ct);
        return dto.FirstOrDefault();
    }

    public async Task<PagedResult<OrdenCambioConTicketsDto>> BuscarOrdenesCambioAsync(TicketFiltro f, CancellationToken ct = default)
    {
        // Categorías de OC del área (chgcat.sym). Si hay filtro de categorías lo respeta.
        var codigosCat = await ResolverCategoriasOcAsync(f.Categorias, ct);
        if (codigosCat.Count == 0)
            return new PagedResult<OrdenCambioConTicketsDto> { Items = new List<OrdenCambioConTicketsDto>(), TotalCount = 0, Page = f.Page, PageSize = f.PageSize };

        int desde = f.FechaDesde.HasValue ? EpochRD.InicioDelDia(f.FechaDesde.Value) : EpochRD.ToEpoch(new DateTime(2026, 1, 1));
        int? hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : (int?)null;

        // OC directo de chg por su propia categoría (como los queries de DWH),
        // sin depender de que tengan tickets. Filtra por fecha de apertura.
        var query = _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && codigosCat.Contains(c.category)
                        && c.open_date >= desde && (hasta == null || c.open_date < hasta));

        var total = await query.CountAsync(ct);
        var changes = await query.OrderByDescending(c => c.open_date)
            .Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToListAsync(ct);

        // Tickets asociados a estas OC (para la lista de tickets relacionados, si tienen)
        var changeIds = changes.Select(c => c.id).ToList();
        var ticketsDeEstas = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => cr.change != null && changeIds.Contains(cr.change.Value)).ToListAsync(ct);

        List<OrdenCambioConTicketsDto> items = await ConstruirOcConTicketsAsync(changes, ticketsDeEstas, ct);

        // Filtro por complejidad (se resuelve tras enriquecer)
        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC))
            items = items.Where(d => string.Equals(d.ComplejidadOC, f.ComplejidadOC, StringComparison.OrdinalIgnoreCase)).ToList();

        return new PagedResult<OrdenCambioConTicketsDto> { Items = items, TotalCount = total, Page = f.Page, PageSize = f.PageSize };
    }

    /// <summary>Códigos de chgcat que corresponden a las categorías de Orden de Cambio del área.
    /// Filtra chgcat por sym con "Orden de Cambio"; si viene un filtro de categorías (sym exactos), lo aplica.</summary>
    public async Task<List<string>> ResolverCategoriasOcAsync(List<string>? categoriasFiltro, CancellationToken ct = default)
    {
        var q = _sdm.ChgCats.AsNoTracking().Where(c => c.sym != null && c.sym.Contains("Orden de Cambio"));
        var cats = await q.Select(c => new { c.code, c.sym }).ToListAsync(ct);

        if (categoriasFiltro is { Count: > 0 })
            cats = cats.Where(c => categoriasFiltro.Any(f => string.Equals((c.sym ?? "").Trim(), f.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

        return cats.Select(c => c.code).Distinct().ToList();
    }

    public async Task<TableroOcDto> TableroOrdenesCambioAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var codigosCat = await ResolverCategoriasOcAsync(f.Categorias, ct);
        if (codigosCat.Count == 0) return new TableroOcDto();

        // El vencimiento se estima como apertura + hasta 90 días (complejidad Alta).
        // Para no perder OC que vencen en el mes pero abrieron antes, ampliamos la
        // ventana de apertura 100 días hacia atrás respecto al inicio pedido.
        var inicioPedido = f.FechaDesde ?? new DateTime(2026, 1, 1);
        int desde = EpochRD.ToEpoch(inicioPedido.AddDays(-100));
        int? hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : (int?)null;

        // Traemos TODAS las OC del alcance/fecha (sin paginar) para el resumen completo.
        var changes = await _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && codigosCat.Contains(c.category)
                        && c.open_date >= desde && (hasta == null || c.open_date < hasta))
            .ToListAsync(ct);

        var changeIds = changes.Select(c => c.id).ToList();
        var tickets = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => cr.change != null && changeIds.Contains(cr.change.Value)).ToListAsync(ct);

        var ordenes = await ConstruirOcConTicketsAsync(changes, tickets, ct);

        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC))
            ordenes = ordenes.Where(d => string.Equals(d.ComplejidadOC, f.ComplejidadOC, StringComparison.OrdinalIgnoreCase)).ToList();

        // Limitar a las OC asignadas a los 6 analistas del grupo (por nombre).
        var nombresGrupo = (await AnalistasDelGrupoAsync(ct)).Select(a => a.Nombre).ToList();
        if (nombresGrupo.Count > 0)
            ordenes = ordenes.Where(o => o.AsignatarioOC != null
                && nombresGrupo.Any(n => string.Equals(n, o.AsignatarioOC, StringComparison.OrdinalIgnoreCase)
                                      || TicketAnalytics.Domain.Common.GrupoAnalistas.EsDelGrupo(o.AsignatarioOC))).ToList();

        // Filtro por analista puntual (si se seleccionó uno en la pantalla).
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre))
            ordenes = ordenes.Where(o => string.Equals(o.AsignatarioOC, f.AsignatarioNombre, StringComparison.OrdinalIgnoreCase)).ToList();

        // "Vencen este mes": vencimiento dentro del mes consultado (o del mes actual si no hay filtro) y aún abiertas.
        var mesRef = f.FechaDesde ?? DateTime.Today;
        var inicioMes = new DateTime(mesRef.Year, mesRef.Month, 1);
        var finMes = inicioMes.AddMonths(1);
        bool VenceEsteMes(OrdenCambioConTicketsDto o) =>
            !o.FechaCierreOC.HasValue && o.FechaVencimientoOC.HasValue
            && o.FechaVencimientoOC.Value >= inicioMes && o.FechaVencimientoOC.Value < finMes;

        var tablero = new TableroOcDto
        {
            Ordenes = ordenes,
            Total = ordenes.Count,
            Vencidas = ordenes.Count(o => o.EstadoVencimiento == "vencida"),
            VenceHoy = ordenes.Count(o => o.EstadoVencimiento == "vence_hoy"),
            VencenPronto = ordenes.Count(o => o.EstadoVencimiento == "vence_pronto"),
            EnCurso = ordenes.Count(o => o.EstadoVencimiento == "en_curso"),
            Completadas = ordenes.Count(o => o.EstadoVencimiento == "completada"),
            SinFecha = ordenes.Count(o => o.EstadoVencimiento == "sin_fecha"),
            VencenEsteMes = ordenes.Count(VenceEsteMes)
        };

        tablero.PorTecnico = ordenes
            .GroupBy(o => string.IsNullOrWhiteSpace(o.AsignatarioOC) ? "Sin asignar" : o.AsignatarioOC!)
            .Select(g => new ResumenOcPorTecnicoDto
            {
                Tecnico = g.Key,
                Total = g.Count(),
                Vencidas = g.Count(o => o.EstadoVencimiento == "vencida"),
                VenceHoy = g.Count(o => o.EstadoVencimiento == "vence_hoy"),
                VencenPronto = g.Count(o => o.EstadoVencimiento == "vence_pronto"),
                EnCurso = g.Count(o => o.EstadoVencimiento == "en_curso"),
                Completadas = g.Count(o => o.EstadoVencimiento == "completada"),
                SinFecha = g.Count(o => o.EstadoVencimiento == "sin_fecha"),
                VencenEsteMes = g.Count(VenceEsteMes)
            })
            .OrderByDescending(t => t.VencenEsteMes).ThenByDescending(t => t.Vencidas).ThenByDescending(t => t.Total)
            .ToList();

        return tablero;
    }

    public async Task<BandejaAnalistasDto> BandejaTicketsPorAnalistaAsync(TicketFiltro f, CancellationToken ct = default)
    {
        // Traemos los tickets del área (abiertos) con su vencimiento ya calculado.
        var filtro = new TicketFiltro
        {
            FechaDesde = f.FechaDesde ?? new DateTime(2026, 1, 1),
            FechaHasta = f.FechaHasta,
            Categorias = f.Categorias
        };
        // Sin paginar: antes pedía PageSize = 5000 a BuscarAsync, que lo reducía en silencio a 25
        // y la bandeja mostraba solo los abiertos de los primeros 25 tickets.
        var todos = await BuscarTodosAsync(filtro, ct);
        // Solo abiertos (sin fecha de cierre)
        var abiertos = todos.Where(t => !t.FechaCierreSolicitado.HasValue
                                             && t.EstadoVencimiento != "cerrado").ToList();

        // Limitar al grupo de analistas
        var nombresGrupo = (await AnalistasDelGrupoAsync(ct)).Select(a => a.Nombre).ToList();
        if (nombresGrupo.Count > 0)
            abiertos = abiertos.Where(t => t.Asignatario != null &&
                (nombresGrupo.Any(n => string.Equals(n, t.Asignatario, StringComparison.OrdinalIgnoreCase))
                 || TicketAnalytics.Domain.Common.GrupoAnalistas.EsDelGrupo(t.Asignatario))).ToList();

        // Filtro por analista puntual
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre))
            abiertos = abiertos.Where(t => string.Equals(t.Asignatario, f.AsignatarioNombre, StringComparison.OrdinalIgnoreCase)).ToList();

        int Orden(string e) => e switch { "vencido" => 0, "vence_hoy" => 1, "vence_pronto" => 2, "en_curso" => 3, _ => 4 };

        var bandeja = new BandejaAnalistasDto
        {
            TotalAbiertos = abiertos.Count,
            Vencidos = abiertos.Count(t => t.EstadoVencimiento == "vencido"),
            VencenHoy = abiertos.Count(t => t.EstadoVencimiento == "vence_hoy"),
            VencenPronto = abiertos.Count(t => t.EstadoVencimiento == "vence_pronto"),
            EnCurso = abiertos.Count(t => t.EstadoVencimiento == "en_curso"),
            SinFecha = abiertos.Count(t => t.EstadoVencimiento == "sin_fecha")
        };

        bandeja.Analistas = abiertos
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Asignatario) ? "Sin asignar" : t.Asignatario!)
            .Select(g => new AnalistaBandejaDto
            {
                Analista = g.Key,
                TotalAbiertos = g.Count(),
                Vencidos = g.Count(t => t.EstadoVencimiento == "vencido"),
                VencenHoy = g.Count(t => t.EstadoVencimiento == "vence_hoy"),
                VencenPronto = g.Count(t => t.EstadoVencimiento == "vence_pronto"),
                EnCurso = g.Count(t => t.EstadoVencimiento == "en_curso"),
                SinFecha = g.Count(t => t.EstadoVencimiento == "sin_fecha"),
                Tickets = g.OrderBy(t => Orden(t.EstadoVencimiento)).ThenBy(t => t.DiasParaVencer ?? int.MaxValue)
                    .Select(t => new TicketBandejaDto
                    {
                        NoTicket = t.NoTicket, Titulo = t.Descripcion, Categoria = t.Categoria, Estado = t.Estado,
                        FechaApertura = t.FechaApertura, FechaVencimiento = t.FechaVencimiento,
                        DiasParaVencer = t.DiasParaVencer, EstadoVencimiento = t.EstadoVencimiento
                    }).ToList()
            })
            .OrderByDescending(a => a.Vencidos).ThenByDescending(a => a.VencenHoy).ThenByDescending(a => a.TotalAbiertos)
            .ToList();

        return bandeja;
    }

    public async Task<IReadOnlyList<AnalistaGrupoDto>> AnalistasDelGrupoAsync(CancellationToken ct = default)
    {
        var ids = TicketAnalytics.Domain.Common.GrupoAnalistas.UserIds;
        var contactos = await _sdm.CaContacts.AsNoTracking()
            .Where(c => c.userid != null && ids.Contains(c.userid))
            .ToListAsync(ct);

        return contactos.Select(c => new AnalistaGrupoDto
        {
            UserId = c.userid ?? "",
            Nombre = $"{c.first_name} {c.last_name}".Trim(),
            Uuid = c.contact_uuid != null ? "0x" + Convert.ToHexString(c.contact_uuid) : null
        }).OrderBy(a => a.Nombre).ToList();
    }

    // ---- Construye OCs con sus tickets, resolviendo catalogos y complejidad ----
    // Público para que el servicio de dashboard de OC reutilice la misma lógica de
    // enriquecimiento (vencimiento, tickets, SLA) sin duplicarla.
    public async Task<List<OrdenCambioConTicketsDto>> ConstruirOcConTicketsAsync(List<Chg> changes, List<CallReq> tickets, CancellationToken ct)
    {
        if (changes.Count == 0) return new();

        var chgStatCodes = changes.Select(c => c.status).Where(s => s != null).Distinct().ToList();
        var chgCats = changes.Select(c => c.category).Where(s => s != null).Distinct().ToList();
        var chgEstMap = await _sdm.ChgStats.AsNoTracking().Where(s => chgStatCodes.Contains(s.code)).ToDictionaryAsync(s => s.code, s => s.description ?? s.sym, ct);

        // Contactos (asignatarios de OC y de tickets, grupos, customers)
        var uuids = changes.SelectMany(c => new[] { c.assignee, c.group_id })
            .Concat(tickets.SelectMany(t => new[] { t.assignee, t.group_id, t.customer }))
            .Where(b => b != null).Select(b => b!).ToList();
        var contactos = await _sdm.CaContacts.AsNoTracking().Where(c => uuids.Contains(c.contact_uuid)).ToListAsync(ct);
        string? Nombre(byte[]? u) => u == null ? null : ContactoNombre(contactos, u);
        string? Grupo(byte[]? u) => u == null ? null : contactos.FirstOrDefault(x => x.contact_uuid.SequenceEqual(u))?.last_name;

        // Categorias de tickets
        var catPersids = tickets.Select(t => t.category).Where(c => c != null).Distinct().ToList();
        var catMap = await _sdm.ProbCtgs.AsNoTracking().Where(p => catPersids.Contains(p.persid)).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
        var estCodes = tickets.Select(t => t.status).Where(s => s != null).Distinct().ToList();
        var estMap = await _sdm.CrStats.AsNoTracking().Where(s => estCodes.Contains(s.code)).ToDictionaryAsync(s => s.code, s => s.description ?? s.sym, ct);

        // SLA de los tickets
        var crIds = tickets.Select(t => t.id).ToList();
        var slas = await _sdm.AttachedSlas.AsNoTracking().Where(s => s.ticket_type == "cr" && s.del == 0 && crIds.Contains(s.ticket_id)).ToListAsync(ct);

        // Categoría legible de las OC (chg.category = chgcat.code)
        var chgCatMap = await _sdm.ChgCats.AsNoTracking().Where(c => chgCats.Contains(c.code))
            .ToDictionaryAsync(c => c.code, c => c.sym, ct);

        // Complejidad (base local)
        var chgRefs = changes.Select(c => c.chg_ref_num).ToList();
        var complejidades = await _analytics.OrdenCambioComplejidades.AsNoTracking()
            .Where(o => chgRefs.Contains(o.ChgRefNum)).ToDictionaryAsync(o => o.ChgRefNum, o => o.Complejidad, ct);

        var result = new List<OrdenCambioConTicketsDto>();
        foreach (var chg in changes)
        {
            var susTickets = tickets.Where(t => t.change == chg.id).Select(cr =>
            {
                var catSym = cr.category != null && catMap.TryGetValue(cr.category, out var cs) ? cs : null;
                var sla = slas.FirstOrDefault(s => s.ticket_id == cr.id);
                return new TicketRelacionadoDto
                {
                    TipoCaso = catSym?.Contains(".Problema/Error.") == true ? "Incidente" : catSym?.Contains(".Solicitud.") == true ? "Solicitud" : "Otro",
                    NoTicket = cr.ref_num,
                    Categoria = catSym,
                    Estado = cr.status != null && estMap.TryGetValue(cr.status, out var es) ? es : cr.status,
                    Asignatario = Nombre(cr.assignee),
                    UsuarioFinalAfectado = Nombre(cr.customer),
                    FechaApertura = EpochRD.ToDateTime(cr.open_date),
                    FechaCierre = EpochRD.ToDateTime(cr.close_date),
                    ResueltoDentroSLA = CalcularSla(cr.close_date, sla?.time_to_violation, sla?.sla_viol_status),
                    CerradoConCambioPendiente = cr.active_flag == 0 && (chg.close_date == null || chg.close_date == 0)
                };
            }).ToList();

            string? complejidad = complejidades.TryGetValue(chg.chg_ref_num, out var nivel) ? nivel.ToString() : null;

            // --- Vencimiento CALCULADO por complejidad ---
            // La base no guarda fecha de vencimiento de SLA para estas OC (attached_sla.time_to_violation
            // está vacío), así que lo estimamos: fecha de apertura + días de la complejidad.
            //   Alta = 90 días, Media = 60 días, Baja = 30 días.
            // Es un aproximado de trabajo hasta tener la fecha real; en la práctica cierran dentro del mes.
            var apertura = EpochRD.ToDateTime(chg.open_date);
            var cierre = EpochRD.ToDateTime(chg.close_date);
            var cerrada = cierre.HasValue;

            DateTime? vencimiento = apertura.HasValue
                ? apertura.Value.AddDays(DiasSlaComplejidad(complejidad))
                : (DateTime?)null;

            int? diasRestantes = vencimiento.HasValue ? (int)Math.Floor((vencimiento.Value.Date - DateTime.Today).TotalDays) : null;
            double? pctConsumido = null;
            if (apertura.HasValue && vencimiento.HasValue && vencimiento > apertura)
            {
                var total = (vencimiento.Value - apertura.Value).TotalSeconds;
                var transcurrido = ((cierre ?? DateTime.Now) - apertura.Value).TotalSeconds;
                pctConsumido = Math.Clamp(transcurrido / total * 100.0, 0, 150);
            }

            // Apartados de vencimiento:
            //  completada  -> ya cerrada
            //  vencida     -> abierta y ya pasó la fecha estimada de vencimiento
            //  vence_hoy   -> abierta, vence hoy
            //  vence_pronto-> abierta, vence en <= 7 días
            //  en_curso    -> abierta, vence a futuro
            //  sin_fecha   -> sin fecha de apertura (no se puede estimar)
            string estadoVenc =
                  cerrada ? "completada"
                : !vencimiento.HasValue ? "sin_fecha"
                : diasRestantes < 0 ? "vencida"
                : diasRestantes == 0 ? "vence_hoy"
                : diasRestantes <= 7 ? "vence_pronto"
                : "en_curso";

            var catLegible = chg.category != null && chgCatMap.TryGetValue(chg.category, out var cs) ? (cs ?? chg.category) : chg.category;

            result.Add(new OrdenCambioConTicketsDto
            {
                ChgId = chg.id,
                NoOC = chg.chg_ref_num,
                ResumenOC = chg.summary,
                DescripcionOC = Limpiar(chg.description),
                EstadoOC = chg.status != null && chgEstMap.TryGetValue(chg.status, out var ce) ? ce : chg.status,
                CategoriaOC = catLegible,
                AsignatarioOC = Nombre(chg.assignee),
                GrupoOC = Grupo(chg.group_id),
                FechaAperturaOC = apertura,
                FechaCierreOC = cierre,
                FechaVencimientoOC = vencimiento,
                DiasRestantes = diasRestantes,
                PctTiempoConsumido = pctConsumido,
                EstadoVencimiento = estadoVenc,
                SinAnalista = chg.assignee == null,
                TieneTicketCerradoOcPendiente = susTickets.Any(t => t.CerradoConCambioPendiente),
                ComplejidadOC = complejidad,
                OCResueltoDentroSLA = (cierre.HasValue && vencimiento.HasValue)
                                        ? (cierre.Value <= vencimiento.Value ? "SI" : "NO")
                                        : "N/A",
                Tickets = susTickets
            });
        }
        return result;
    }

    // ---- Enriquecimiento de tickets sueltos ----
    private async Task<List<TicketEnriquecidoDto>> EnriquecerAsync(List<CallReq> tickets, CancellationToken ct)
    {
        if (tickets.Count == 0) return new();

        var cats = tickets.Select(t => t.category).Where(c => c != null).Distinct().ToList();
        var estados = tickets.Select(t => t.status).Where(s => s != null).Distinct().ToList();
        var changeIds = tickets.Where(t => t.change is > 0).Select(t => t.change!.Value).Distinct().ToList();
        var crIds = tickets.Select(t => t.id).ToList();
        var uuids = tickets.SelectMany(t => new[] { t.assignee, t.group_id, t.customer }).Where(b => b != null).Select(b => b!).ToList();

        var catMap = await _sdm.ProbCtgs.AsNoTracking().Where(p => cats.Contains(p.persid)).ToDictionaryAsync(p => p.persid, p => p.sym, ct);
        var estMap = await _sdm.CrStats.AsNoTracking().Where(s => estados.Contains(s.code)).ToDictionaryAsync(s => s.code, s => s.description ?? s.sym, ct);
        var contactos = await _sdm.CaContacts.AsNoTracking().Where(c => uuids.Contains(c.contact_uuid)).ToListAsync(ct);
        var changes = await _sdm.Changes.AsNoTracking().Where(c => changeIds.Contains(c.id)).ToListAsync(ct);
        var chgStatCodes = changes.Select(c => c.status).Where(s => s != null).Distinct().ToList();
        var chgEstMap = await _sdm.ChgStats.AsNoTracking().Where(s => chgStatCodes.Contains(s.code)).ToDictionaryAsync(s => s.code, s => s.description ?? s.sym, ct);
        var chgUuids = changes.Select(c => c.assignee).Where(b => b != null).Select(b => b!).ToList();
        var chgContactos = await _sdm.CaContacts.AsNoTracking().Where(c => chgUuids.Contains(c.contact_uuid)).ToListAsync(ct);
        var slas = await _sdm.AttachedSlas.AsNoTracking().Where(s => s.ticket_type == "cr" && s.del == 0 && crIds.Contains(s.ticket_id)).ToListAsync(ct);
        var chgRefs = changes.Select(c => c.chg_ref_num).ToList();
        var complejidades = await _analytics.OrdenCambioComplejidades.AsNoTracking().Where(o => chgRefs.Contains(o.ChgRefNum)).ToDictionaryAsync(o => o.ChgRefNum, o => o.Complejidad, ct);

        var result = new List<TicketEnriquecidoDto>();
        foreach (var cr in tickets)
        {
            var catSym = cr.category != null && catMap.TryGetValue(cr.category, out var cs) ? cs : null;
            var tipoCaso = catSym?.Contains(".Problema/Error.") == true ? "Incidente" : catSym?.Contains(".Solicitud.") == true ? "Solicitud" : "Otro";
            var chg = cr.change is > 0 ? changes.FirstOrDefault(c => c.id == cr.change!.Value) : null;
            var sla = slas.FirstOrDefault(s => s.ticket_id == cr.id);
            string? complejidad = chg != null && complejidades.TryGetValue(chg.chg_ref_num, out var nivel) ? nivel.ToString() : null;

            // Incumplimiento previsto = fecha de vencimiento del SLA (attached_sla)
            var vencTicket = EpochRD.ToDateTime(sla?.time_to_violation);
            var cerradoTicket = EpochRD.ToDateTime(cr.close_date).HasValue;
            int? diasVencer = vencTicket.HasValue ? (int)Math.Floor((vencTicket.Value.Date - DateTime.Today).TotalDays) : null;
            string estadoVencTicket =
                  cerradoTicket ? "cerrado"
                : !vencTicket.HasValue ? "sin_fecha"
                : diasVencer < 0 ? "vencido"
                : diasVencer == 0 ? "vence_hoy"
                : diasVencer <= 2 ? "vence_pronto"
                : "en_curso";

            result.Add(new TicketEnriquecidoDto
            {
                TipoCaso = tipoCaso,
                NoTicket = cr.ref_num,
                Descripcion = Limpiar(cr.description),
                Categoria = catSym,
                Estado = cr.status != null && estMap.TryGetValue(cr.status, out var es) ? es : cr.status,
                UsuarioFinalAfectado = ContactoNombre(contactos, cr.customer),
                Asignatario = ContactoNombre(contactos, cr.assignee),
                Grupo = cr.group_id != null ? contactos.FirstOrDefault(c => c.contact_uuid.SequenceEqual(cr.group_id))?.last_name : null,
                FechaApertura = EpochRD.ToDateTime(cr.open_date),
                FechaCierreSolicitado = EpochRD.ToDateTime(cr.close_date),
                ResueltoDentroSLA = CalcularSla(cr.close_date, sla?.time_to_violation, sla?.sla_viol_status),
                SlaViolationFlag = cr.sla_violation == 1,
                FechaVencimiento = vencTicket,
                DiasParaVencer = diasVencer,
                EstadoVencimiento = estadoVencTicket,
                NoOC = chg?.chg_ref_num,
                EstadoOC = chg?.status != null && chgEstMap.TryGetValue(chg.status, out var ce) ? ce : chg?.status,
                AsignatarioOC = ContactoNombre(chgContactos, chg?.assignee),
                FechaAperturaOC = chg != null ? EpochRD.ToDateTime(chg.open_date) : null,
                FechaCierreSolicitadoOC = chg != null ? EpochRD.ToDateTime(chg.close_date) : null,
                ComplejidadOC = complejidad,
                OCResueltoDentroSLA = chg != null ? (chg.sla_violation == 1 ? "NO" : "SI") : null,
                ReqCerradoConCambioPendiente = cr.active_flag == 0 && chg != null && (chg.close_date == null || chg.close_date == 0)
            });
        }
        return result;
    }

    /// <summary>Versión pública del alcance, para reutilizar desde otros servicios
    /// (dashboard de OC). Devuelve los persids activos, intersectados con el filtro.</summary>
    public async Task<List<string>> ResolverAlcancePublicoAsync(List<string>? categoriasFiltro, CancellationToken ct = default)
    {
        var alcance = await _analytics.CategoriasAlcance.AsNoTracking()
            .Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
        if (categoriasFiltro is { Count: > 0 })
            alcance = alcance.Where(p => categoriasFiltro.Contains(p)).ToList();
        return alcance;
    }

    private async Task<List<string>> ResolverAlcanceAsync(TicketFiltro f, CancellationToken ct)
    {
        var alcance = await _analytics.CategoriasAlcance.AsNoTracking()
            .Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);

        if (alcance.Count == 0)
        {
            _logger.LogWarning("SIN CATEGORIAS EN ALCANCE en la base local. La busqueda devolvera CERO tickets. " +
                "Agrega categorias con POST /api/admin/categorias o revisa el seed.");
            return alcance;
        }

        // Tipo de caso (Incidente / Solicitud): el catálogo local ya clasifica cada
        // categoría, así que el filtro se resuelve reduciendo el alcance. Al aplicarse
        // sobre la consulta base, entra en el Count y en la paginación.
        if (!string.IsNullOrWhiteSpace(f.TipoCaso))
        {
            var delTipo = await _analytics.CategoriasAlcance.AsNoTracking()
                .Where(c => c.Activo && c.TipoCaso == f.TipoCaso)
                .Select(c => c.Persid).ToListAsync(ct);

            alcance = alcance.Where(p => delTipo.Contains(p)).ToList();
            if (alcance.Count == 0)
                _logger.LogWarning("Ninguna categoría en alcance es de tipo {Tipo}. Cero resultados.", f.TipoCaso);
        }

        if (f.Categorias is { Count: > 0 })
        {
            var filtrado = alcance.Where(p => f.Categorias.Contains(p)).ToList();
            if (filtrado.Count == 0)
                _logger.LogWarning("El filtro de categorias {Filtro} no coincide con ninguna categoria en alcance {Alcance}. Cero resultados.",
                    string.Join(",", f.Categorias), string.Join(",", alcance));
            return filtrado;
        }

        _logger.LogInformation("Alcance resuelto: {N} categorias.", alcance.Count);
        return alcance;
    }

    private static string? ContactoNombre(List<CaContact> contactos, byte[]? uuid)
    {
        if (uuid == null) return null;
        var c = contactos.FirstOrDefault(x => x.contact_uuid.SequenceEqual(uuid));
        return c == null ? null : $"{c.first_name} {c.last_name}".Trim();
    }

    private static string CalcularSla(int? closeDate, int? ttv, int? violStatus)
    {
        if (ttv is null or 0) return "N/A";
        if (closeDate is > 0) return closeDate <= ttv ? "SI" : "NO";
        return violStatus == 1 ? "NO" : "EN CURSO";
    }

    /// <summary>Días de SLA por complejidad de OC (aproximado, desde la apertura).
    /// Alta = 90, Media = 60, Baja = 30. Sin complejidad conocida usa 60 (media).</summary>
    private static int DiasSlaComplejidad(string? complejidad) => complejidad?.Trim().ToLowerInvariant() switch
    {
        "alta" => 90,
        "media" => 60,
        "baja" => 30,
        _ => 60
    };

    private static string? Limpiar(string? s) => s?.Replace("\r", " ").Replace("\n", " ").Replace(";", " ").Trim();

    /// <summary>Convierte hex a bytes. Devuelve null si el formato es invalido
    /// (en vez de lanzar excepcion y provocar un 500).</summary>
    private static byte[]? HexToBytes(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        hex = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
        if (hex.Length % 2 != 0) return null;
        try
        {
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
        catch { return null; }
    }
}
