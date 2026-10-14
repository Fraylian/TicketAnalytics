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
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>
/// Consulta de tickets y órdenes de cambio desde MDB (solo lectura) enriquecidos con datos de la
/// base local. Reglas que sigue toda consulta a MDB de esta clase:
///  - Siempre acotada por categoría del alcance y por rango de fechas (open_date &gt;= fecha base).
///  - Filtros que dependen de otras tablas de MDB (SLA, OC abierta, complejidad) son EXISTS
///    correlacionados, nunca "traer la tabla a memoria y armar un IN gigante".
///  - Catálogos (categorías, estados, contactos, grupo) desde CatalogoSdm (caché en memoria).
///  - Parámetros de negocio (fecha base, plazos, umbrales) desde la base local, no del código.
/// </summary>
public class TicketQueryService : ITicketQueryService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    private readonly ILogger<TicketQueryService> _logger;
    private readonly IParametrosProvider _parametros;
    private readonly CatalogoSdm _catalogo;
    private readonly ConsultaOrdenesCambio _oc;

    public TicketQueryService(SdmReadContext sdm, AnalyticsContext analytics, ILogger<TicketQueryService> logger,
                              IParametrosProvider parametros, CatalogoSdm catalogo, ConsultaOrdenesCambio oc)
    {
        _sdm = sdm;
        _analytics = analytics;
        _logger = logger;
        _parametros = parametros;
        _catalogo = catalogo;
        _oc = oc;
    }

    /// <summary>Tamaño máximo de página. Antes un valor mayor caía EN SILENCIO a 25 filas.</summary>
    private const int PaginaMaxima = 200;

    // ============ BÚSQUEDA DE TICKETS ============
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

        // En lotes para no materializar decenas de miles de filas de golpe.
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
    /// Consulta de tickets con TODOS los filtros aplicados antes de contar/paginar.
    /// Base: call_req por categoría del alcance (call_req_x7) y rango de apertura (call_req_x0).
    /// </summary>
    private async Task<(IQueryable<CallReq> Query, int Pagina, int Tamano)> ConstruirConsultaTicketsAsync(
        TicketFiltro f, CancellationToken ct)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var alcance = await ResolverAlcanceAsync(f, ct);
        var (desde, hasta) = EpochRD.Rango(f.FechaDesde, f.FechaHasta, p.FechaBase);

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

        // Asignatario por NOMBRE: se resuelve a UUID (primero contra el grupo en caché) y se filtra
        // con "assignee = @u1 OR ..." (call_req_x4). Antes se traían TODOS los tickets del rango a
        // memoria para comparar los binarios en C#.
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre))
        {
            var uuids = await _catalogo.UuidsPorNombreAsync(f.AsignatarioNombre, contiene: false, ct);
            q = ConsultasSdm.FiltrarPorUuid(q, cr => cr.assignee, uuids);
        }
        if (HexToBytes(f.GrupoUuid) is { } grupoB) q = q.Where(cr => cr.group_id == grupoB);

        // --- Complejidad de la OC (vive en la base local) ---
        // EXISTS sobre chg por clave primaria; la lista de chg_ref_num viaja como UN parámetro JSON.
        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC))
        {
            if (!Enum.TryParse<NivelComplejidad>(f.ComplejidadOC, true, out var nivelFiltro))
            {
                _logger.LogWarning("Complejidad de OC no reconocida: {Valor}", f.ComplejidadOC);
                q = q.Where(cr => false);
            }
            else
            {
                var refs = await _analytics.OrdenCambioComplejidades.AsNoTracking()
                    .Where(o => o.Complejidad == nivelFiltro).Select(o => o.ChgRefNum).ToListAsync(ct);
                q = refs.Count == 0
                    ? q.Where(cr => false)
                    : q.Where(cr => cr.change != null && _sdm.Changes.Any(c => c.id == cr.change && refs.Contains(c.chg_ref_num)));
            }
        }

        // --- Ticket cerrado pero su OC sigue abierta (problema b) ---
        // Antes: SELECT id de TODAS las OC abiertas del banco + IN gigante. Ahora: EXISTS por PK de chg.
        if (f.SoloReqCerradoCambioPendiente == true)
            q = q.Where(cr => cr.active_flag == 0 && cr.change != null
                              && _sdm.Changes.Any(c => c.id == cr.change && (c.close_date == null || c.close_date == 0)));

        // --- Vista por defecto: solo lo que requiere atención (abierto / sin cerrar) ---
        if (f.SoloAbiertos == true)
            q = q.Where(cr => cr.active_flag == 1 || cr.close_date == null || cr.close_date == 0);

        // --- Filtros de la gestión local (base Analytics): se resuelven a RefNum y van antes del Count ---
        var filtraPorGestion = f.ReportadoProveedor.HasValue || f.PendienteEscalarProveedor == true || f.Sistema.HasValue
                            || f.TipoSolucion.HasValue || f.TipoActualizacionDatos.HasValue || f.ConCausaRaiz.HasValue
                            || f.ReportadoVP.HasValue;
        if (filtraPorGestion)
        {
            var gq = _analytics.TicketSeguimientos.AsNoTracking().Where(s => s.TipoOrigen == "cr");

            if (f.ReportadoProveedor.HasValue && Enum.IsDefined(typeof(EstadoReporteProveedorTicket), f.ReportadoProveedor.Value))
            {
                var est = (EstadoReporteProveedorTicket)f.ReportadoProveedor.Value;
                gq = gq.Where(s => s.ReportadoProveedor == est);
            }
            if (f.PendienteEscalarProveedor == true) gq = gq.Where(s => s.ReportadoProveedor != EstadoReporteProveedorTicket.Si);
            if (f.Sistema is > 0)
            {
                var sis = f.Sistema.Value;
                gq = gq.Where(s => s.SistemaId == sis);
            }
            if (f.TipoSolucion.HasValue && Enum.IsDefined(typeof(TipoSolucion), f.TipoSolucion.Value))
            {
                var ts = (TipoSolucion)f.TipoSolucion.Value;
                gq = gq.Where(s => s.TipoSolucion == ts);
            }
            if (f.TipoActualizacionDatos.HasValue && Enum.IsDefined(typeof(TipoActualizacionDatos), f.TipoActualizacionDatos.Value))
            {
                var ta = (TipoActualizacionDatos)f.TipoActualizacionDatos.Value;
                gq = gq.Where(s => s.TipoActualizacionDatos == ta);
            }
            if (f.ConCausaRaiz == true) gq = gq.Where(s => s.CausaRaiz != null && s.CausaRaiz != "");
            if (f.ConCausaRaiz == false) gq = gq.Where(s => s.CausaRaiz == null || s.CausaRaiz == "");
            if (f.ReportadoVP.HasValue) gq = gq.Where(s => s.VaReporteVP == f.ReportadoVP.Value);

            // Lista acotada por los casos gestionados localmente; viaja como un parámetro JSON.
            var refs = await gq.Select(s => s.RefNum).Distinct().ToListAsync(ct);
            q = q.Where(cr => refs.Contains(cr.ref_num));
        }

        // --- Estado de SLA (problema a) ---
        // Antes: SELECT de TODO attached_sla del banco (sin fecha ni alcance) y luego NOT IN / IN
        // con miles de ids; con volumen real la consulta "N/A" ni siquiera terminaba (timeout).
        // Ahora: EXISTS correlacionado por attached_sla_x0 (ticket_id, ticket_type), solo para los
        // tickets que ya pasaron el filtro de alcance y fecha. Mismas reglas que CalcularSla.
        if (!string.IsNullOrWhiteSpace(f.EstadoSla))
        {
            var sla = _sdm.AttachedSlas.Where(s => s.ticket_type == "cr" && s.del == 0
                                                   && s.time_to_violation != null && s.time_to_violation != 0);
            q = f.EstadoSla.Trim().ToUpperInvariant() switch
            {
                "N/A" => q.Where(cr => !sla.Any(s => s.ticket_id == cr.id)),
                "SI" => q.Where(cr => cr.close_date > 0 && sla.Any(s => s.ticket_id == cr.id && cr.close_date <= s.time_to_violation)),
                "NO" => q.Where(cr => sla.Any(s => s.ticket_id == cr.id
                                         && ((cr.close_date > 0 && cr.close_date > s.time_to_violation)
                                             || ((cr.close_date == null || cr.close_date == 0) && s.sla_viol_status == 1)))),
                "EN CURSO" => q.Where(cr => (cr.close_date == null || cr.close_date == 0)
                                            && sla.Any(s => s.ticket_id == cr.id && (s.sla_viol_status == null || s.sla_viol_status != 1))),
                _ => q.Where(cr => false)
            };
        }

        // --- Abiertos que vencen entre hoy y los próximos Ticket.DiasVencePronto días ---
        // Misma regla que EstadoVencimientoTicket (vence_hoy + vence_pronto) sobre el PRIMER SLA del
        // ticket, como en el enriquecimiento. Subconsulta TOP 1 por attached_sla_x0.
        if (f.ProximoAVencer == true)
        {
            int ini = ConsultaOrdenesCambio.Reloj.Limite(DateTime.Today);
            int fin = ConsultaOrdenesCambio.Reloj.Limite(DateTime.Today.AddDays(p.TicketDiasVencePronto + 1));
            q = q.Where(cr => (cr.close_date == null || cr.close_date == 0)
                              && _sdm.AttachedSlas.Where(s => s.ticket_id == cr.id && s.ticket_type == "cr" && s.del == 0)
                                     .OrderBy(s => s.id).Select(s => s.time_to_violation).FirstOrDefault() >= ini
                              && _sdm.AttachedSlas.Where(s => s.ticket_id == cr.id && s.ticket_type == "cr" && s.del == 0)
                                     .OrderBy(s => s.id).Select(s => s.time_to_violation).FirstOrDefault() < fin);
        }

        var pagina = f.Page < 1 ? 1 : f.Page;
        var tamano = Math.Clamp(f.PageSize, 1, PaginaMaxima);
        return (q, pagina, tamano);
    }

    private static IQueryable<CallReq> OrdenarTickets(IQueryable<CallReq> q, TicketFiltro f)
    {
        var orden = (f.OrderBy ?? "fechaapertura").ToLowerInvariant();
        return (orden, f.OrderDesc) switch
        {
            ("noticket", true) => q.OrderByDescending(cr => cr.ref_num),
            ("noticket", false) => q.OrderBy(cr => cr.ref_num),
            ("fechaapertura", true) => q.OrderByDescending(cr => cr.open_date).ThenByDescending(cr => cr.id),
            _ => q.OrderBy(cr => cr.open_date).ThenBy(cr => cr.id)
        };
    }

    public async Task<TicketEnriquecidoDto?> ObtenerPorRefNumAsync(string refNum, CancellationToken ct = default)
    {
        // call_req_x3 (ref_num, único).
        var cr = await _sdm.CallReqs.AsNoTracking().FirstOrDefaultAsync(x => x.ref_num == refNum, ct);
        if (cr is null) return null;
        return (await EnriquecerAsync(new() { cr }, ct)).FirstOrDefault();
    }

    // ============ ORDEN DE CAMBIO + SUS TICKETS ============
    public async Task<OrdenCambioConTicketsDto?> ObtenerOrdenCambioConTicketsAsync(string chgRefNum, CancellationToken ct = default)
    {
        // chg_x3 (chg_ref_num, único) y call_req_x17 (change).
        var chg = await _sdm.Changes.AsNoTracking().FirstOrDefaultAsync(c => c.chg_ref_num == chgRefNum, ct);
        if (chg is null) return null;
        var tickets = await _sdm.CallReqs.AsNoTracking().Where(cr => cr.change == chg.id).ToListAsync(ct);
        return (await ConstruirOcConTicketsAsync(new List<Chg> { chg }, tickets, ct)).FirstOrDefault();
    }

    public async Task<PagedResult<OrdenCambioConTicketsDto>> BuscarOrdenesCambioAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var codigosCat = await ResolverCategoriasOcAsync(f.Categorias, ct);
        var pagina = f.Page < 1 ? 1 : f.Page;
        var tamano = Math.Clamp(f.PageSize, 1, PaginaMaxima);
        if (codigosCat.Count == 0)
            return new PagedResult<OrdenCambioConTicketsDto> { Items = Array.Empty<OrdenCambioConTicketsDto>(), TotalCount = 0, Page = pagina, PageSize = tamano };

        var (desde, hasta) = EpochRD.Rango(f.FechaDesde, f.FechaHasta, p.FechaBase);
        var query = _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && codigosCat.Contains(c.category)
                        && c.open_date >= desde && (hasta == null || c.open_date < hasta));

        // Complejidad ANTES de contar y paginar (antes se filtraba después y el total no cuadraba).
        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC) && Enum.TryParse<NivelComplejidad>(f.ComplejidadOC, true, out var nivel))
        {
            var refs = await _analytics.OrdenCambioComplejidades.AsNoTracking()
                .Where(o => o.Complejidad == nivel).Select(o => o.ChgRefNum).ToListAsync(ct);
            query = query.Where(c => refs.Contains(c.chg_ref_num));
        }

        var total = await query.CountAsync(ct);
        var changes = await query.OrderByDescending(c => c.open_date).ThenByDescending(c => c.id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        var ids = changes.Select(c => c.id).ToList();
        var ticketsDeEstas = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => cr.change != null && ids.Contains(cr.change.Value)).ToListAsync(ct);

        var items = await ConstruirOcConTicketsAsync(changes, ticketsDeEstas, ct);
        return new PagedResult<OrdenCambioConTicketsDto> { Items = items, TotalCount = total, Page = pagina, PageSize = tamano };
    }

    /// <summary>Códigos de chgcat de las OC del área (desde la caché de catálogos).</summary>
    public async Task<List<string>> ResolverCategoriasOcAsync(List<string>? categoriasFiltro, CancellationToken ct = default)
    {
        var cats = await _catalogo.CategoriasOcAreaAsync(ct);
        return cats.Where(c => categoriasFiltro is not { Count: > 0 }
                               || categoriasFiltro.Any(f => string.Equals((c.Value ?? "").Trim(), f.Trim(), StringComparison.OrdinalIgnoreCase)))
                   .Select(c => c.Key).Distinct().ToList();
    }

    /// <summary>
    /// Tablero de OC por técnico (problema c). Antes: traía TODAS las OC de la ventana sin paginar,
    /// con sus tickets, y agrupaba por técnico en memoria. Ahora: un GROUP BY assignee en SQL sobre
    /// las OC del grupo, con el vencimiento calculado en la consulta. Ya no devuelve la lista
    /// completa de OC (la pantalla no la usa; el detalle se pide paginado a /buscar-avanzado).
    /// </summary>
    public async Task<TableroOcDto> TableroOrdenesCambioAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var oc = _oc;
        var r = await oc.RelojAsync(ct);
        var cats = await oc.CategoriasAsync(f.Categorias, ct);
        if (cats.Count == 0) return new TableroOcDto();

        var (desde, hasta) = ConsultaOrdenesCambio.Ventana(r, f.FechaDesde, f.FechaHasta);
        var grupo = await _catalogo.GrupoAsync(ct);
        var uuids = grupo.Select(g => g.Uuid).ToList();
        if (!string.IsNullOrWhiteSpace(f.AsignatarioNombre))
            uuids = grupo.Where(g => string.Equals(g.Nombre, f.AsignatarioNombre, StringComparison.OrdinalIgnoreCase)).Select(g => g.Uuid).ToList();
        if (uuids.Count == 0) return new TableroOcDto();

        var q = await oc.BaseAsync(r, cats, desde, hasta, c => ConsultasSdm.FiltrarPorUuid(c, x => x.assignee, uuids), ct);
        if (!string.IsNullOrWhiteSpace(f.ComplejidadOC))
        {
            var nivel = f.ComplejidadOC.Trim().ToLowerInvariant() switch { "alta" => 3, "media" => 2, "baja" => 1, "sinasignar" => 0, _ => int.MinValue };
            q = q.Where(o => o.Nivel == nivel);
        }

        var mesRef = f.FechaDesde ?? DateTime.Today;
        var inicioMes = new DateTime(mesRef.Year, mesRef.Month, 1);
        int m0 = ConsultaOrdenesCambio.Reloj.Limite(inicioMes), m1 = ConsultaOrdenesCambio.Reloj.Limite(inicioMes.AddMonths(1));
        int hoy = r.HoyE, manana = r.DiaE(1), pronto = r.DiaE(r.P.OcDiasVencePronto + 1);

        var filas = await q.GroupBy(o => o.Assignee).Select(g => new
        {
            Uuid = g.Key,
            Total = g.Count(),
            Completadas = g.Count(o => o.CloseDate != null && o.CloseDate != 0),
            SinFecha = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc == null),
            Vencidas = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc < hoy),
            VenceHoy = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= hoy && o.Venc < manana),
            VencenPronto = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= manana && o.Venc < pronto),
            EnCurso = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= pronto),
            VencenEsteMes = g.Count(o => (o.CloseDate == null || o.CloseDate == 0) && o.Venc >= m0 && o.Venc < m1)
        }).ToListAsync(ct);

        string Nombre(byte[]? u) => grupo.FirstOrDefault(g => u != null && g.Uuid.SequenceEqual(u))?.Nombre ?? "Sin asignar";

        var tablero = new TableroOcDto
        {
            Total = filas.Sum(x => x.Total),
            Vencidas = filas.Sum(x => x.Vencidas),
            VenceHoy = filas.Sum(x => x.VenceHoy),
            VencenPronto = filas.Sum(x => x.VencenPronto),
            EnCurso = filas.Sum(x => x.EnCurso),
            Completadas = filas.Sum(x => x.Completadas),
            SinFecha = filas.Sum(x => x.SinFecha),
            VencenEsteMes = filas.Sum(x => x.VencenEsteMes),
            PorTecnico = filas.Select(x => new ResumenOcPorTecnicoDto
            {
                Tecnico = Nombre(x.Uuid), Total = x.Total, Vencidas = x.Vencidas, VenceHoy = x.VenceHoy,
                VencenPronto = x.VencenPronto, EnCurso = x.EnCurso, Completadas = x.Completadas,
                SinFecha = x.SinFecha, VencenEsteMes = x.VencenEsteMes
            })
            .OrderByDescending(t => t.VencenEsteMes).ThenByDescending(t => t.Vencidas).ThenByDescending(t => t.Total)
            .ToList()
        };
        return tablero;
    }

    /// <summary>
    /// Bandeja: tickets ABIERTOS de los analistas del grupo con su vencimiento de SLA.
    /// Antes: BuscarTodosAsync de todo el rango (abiertos y cerrados) + filtros en memoria.
    /// Ahora: SQL trae solo los abiertos del grupo (call_req_x4 assignee/active_flag) con el
    /// time_to_violation por subconsulta TOP 1 (attached_sla_x0). Es la lista que se muestra.
    /// </summary>
    public async Task<BandejaAnalistasDto> BandejaTicketsPorAnalistaAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var p = await _parametros.ObtenerAsync(ct);
        var alcance = await ResolverAlcanceAsync(new TicketFiltro { Categorias = f.Categorias }, ct);
        var (desde, hasta) = EpochRD.Rango(f.FechaDesde, f.FechaHasta, p.FechaBase);
        var grupo = await _catalogo.GrupoAsync(ct);

        var miembros = string.IsNullOrWhiteSpace(f.AsignatarioNombre)
            ? grupo
            : grupo.Where(g => string.Equals(g.Nombre, f.AsignatarioNombre, StringComparison.OrdinalIgnoreCase)).ToList();
        if (miembros.Count == 0 || alcance.Count == 0) return new BandejaAnalistasDto();

        var q = _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && (hasta == null || cr.open_date < hasta)
                         && (cr.close_date == null || cr.close_date == 0));
        q = ConsultasSdm.FiltrarPorUuid(q, cr => cr.assignee, miembros.Select(m => m.Uuid).ToList());

        var filas = await q.Select(cr => new
        {
            cr.ref_num, cr.description, cr.category, cr.status, cr.open_date, cr.assignee,
            Ttv = _sdm.AttachedSlas.Where(s => s.ticket_id == cr.id && s.ticket_type == "cr" && s.del == 0)
                                   .OrderBy(s => s.id).Select(s => s.time_to_violation).FirstOrDefault()
        }).ToListAsync(ct);

        var catMap = await _catalogo.SymCategoriasTicketAsync(ct);
        var estMap = await _catalogo.EstadosTicketAsync(ct);

        var tickets = filas.Select(x =>
        {
            var venc = EpochRD.ToDateTime(x.Ttv);
            var (estado, dias) = EstadoVencimientoTicket(venc, cerrado: false, p.TicketDiasVencePronto);
            return new
            {
                Analista = miembros.First(m => x.assignee != null && m.Uuid.SequenceEqual(x.assignee)).Nombre,
                Dto = new TicketBandejaDto
                {
                    NoTicket = x.ref_num, Titulo = Limpiar(x.description),
                    Categoria = x.category != null && catMap.TryGetValue(x.category, out var c) ? c : null,
                    Estado = x.status != null && estMap.TryGetValue(x.status, out var e) ? e : x.status,
                    FechaApertura = EpochRD.ToDateTime(x.open_date), FechaVencimiento = venc,
                    DiasParaVencer = dias, EstadoVencimiento = estado
                }
            };
        }).ToList();

        int Orden(string e) => e switch { "vencido" => 0, "vence_hoy" => 1, "vence_pronto" => 2, "en_curso" => 3, _ => 4 };

        var bandeja = new BandejaAnalistasDto
        {
            TotalAbiertos = tickets.Count,
            Vencidos = tickets.Count(t => t.Dto.EstadoVencimiento == "vencido"),
            VencenHoy = tickets.Count(t => t.Dto.EstadoVencimiento == "vence_hoy"),
            VencenPronto = tickets.Count(t => t.Dto.EstadoVencimiento == "vence_pronto"),
            EnCurso = tickets.Count(t => t.Dto.EstadoVencimiento == "en_curso"),
            SinFecha = tickets.Count(t => t.Dto.EstadoVencimiento == "sin_fecha"),
            Analistas = tickets.GroupBy(t => t.Analista).Select(g => new AnalistaBandejaDto
            {
                Analista = g.Key,
                TotalAbiertos = g.Count(),
                Vencidos = g.Count(t => t.Dto.EstadoVencimiento == "vencido"),
                VencenHoy = g.Count(t => t.Dto.EstadoVencimiento == "vence_hoy"),
                VencenPronto = g.Count(t => t.Dto.EstadoVencimiento == "vence_pronto"),
                EnCurso = g.Count(t => t.Dto.EstadoVencimiento == "en_curso"),
                SinFecha = g.Count(t => t.Dto.EstadoVencimiento == "sin_fecha"),
                Tickets = g.Select(t => t.Dto).OrderBy(t => Orden(t.EstadoVencimiento)).ThenBy(t => t.DiasParaVencer ?? int.MaxValue).ToList()
            })
            .OrderByDescending(a => a.Vencidos).ThenByDescending(a => a.VencenHoy).ThenByDescending(a => a.TotalAbiertos)
            .ToList()
        };
        return bandeja;
    }

    /// <summary>Tickets abiertos pendientes de gestión por persona del grupo (para "Atención por persona").</summary>
    public sealed record PendientesTickets(int Abiertos, int SlaVencido, int SinGestion, int SinAvance);

    public async Task<Dictionary<string, PendientesTickets>> PendientesPorPersonaAsync(IReadOnlyList<MiembroGrupo> grupo, CancellationToken ct = default)
    {
        var res = new Dictionary<string, PendientesTickets>(StringComparer.OrdinalIgnoreCase);
        if (grupo.Count == 0) return res;

        var p = await _parametros.ObtenerAsync(ct);
        var alcance = (await _catalogo.AlcanceAsync(ct)).Select(a => a.Persid).ToList();
        if (alcance.Count == 0) return res;
        int desde = EpochRD.InicioDelDia(p.FechaBase);
        int ahora = ConsultaOrdenesCambio.Reloj.Limite(DateTime.Now);

        var abiertos = _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && (cr.close_date == null || cr.close_date == 0));
        abiertos = ConsultasSdm.FiltrarPorUuid(abiertos, cr => cr.assignee, grupo.Select(g => g.Uuid).ToList());

        // Conteos por persona: GROUP BY assignee en SQL.
        var total = await abiertos.GroupBy(cr => cr.assignee).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var vencidos = await abiertos
            .Where(cr => _sdm.AttachedSlas.Any(s => s.ticket_id == cr.id && s.ticket_type == "cr" && s.del == 0
                                                     && s.time_to_violation != null && s.time_to_violation != 0
                                                     && (s.sla_viol_status == 1 || s.time_to_violation < ahora)))
            .GroupBy(cr => cr.assignee).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);

        // "Sin gestión" cruza con la base local: solo viajan los ref_num de los ABIERTOS del grupo.
        var refs = await abiertos.Select(cr => new { cr.ref_num, cr.assignee }).ToListAsync(ct);
        var nums = refs.Select(r => r.ref_num).ToList();
        var limite = DateTime.UtcNow.AddDays(-p.SeguimientoDiasSinAvance);
        var gestion = await _analytics.TicketSeguimientos.AsNoTracking()
            .Where(s => s.TipoOrigen == "cr" && nums.Contains(s.RefNum))
            .Select(s => new { s.RefNum, s.ActualizadoEn, s.Activo }).ToListAsync(ct);
        var porRef = gestion.GroupBy(g => g.RefNum).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ActualizadoEn).First());

        foreach (var m in grupo)
        {
            var mias = refs.Where(r => r.assignee != null && r.assignee.SequenceEqual(m.Uuid)).ToList();
            var sinGestion = mias.Count(r => !porRef.ContainsKey(r.ref_num));
            var sinAvance = mias.Count(r => porRef.TryGetValue(r.ref_num, out var g) && g.Activo && g.ActualizadoEn < limite);
            res[m.UuidHex] = new PendientesTickets(
                total.FirstOrDefault(x => x.Key != null && x.Key.SequenceEqual(m.Uuid))?.N ?? 0,
                vencidos.FirstOrDefault(x => x.Key != null && x.Key.SequenceEqual(m.Uuid))?.N ?? 0,
                sinGestion, sinAvance);
        }
        return res;
    }

    public async Task<IReadOnlyList<AnalistaGrupoDto>> AnalistasDelGrupoAsync(CancellationToken ct = default)
        => (await _catalogo.GrupoAsync(ct))
            .Select(m => new AnalistaGrupoDto { UserId = m.UserId, Nombre = m.Nombre, Uuid = m.UuidHex })
            .OrderBy(a => a.Nombre).ToList();

    /// <summary>Grupo de analistas resuelto (para otros servicios de Infrastructure).</summary>
    public Task<IReadOnlyList<MiembroGrupo>> GrupoAsync(CancellationToken ct = default) => _catalogo.GrupoAsync(ct);

    // ---- Construye OCs con sus tickets (solo las que se van a mostrar) ----
    public async Task<List<OrdenCambioConTicketsDto>> ConstruirOcConTicketsAsync(List<Chg> changes, List<CallReq> tickets, CancellationToken ct)
    {
        if (changes.Count == 0) return new();
        var p = await _parametros.ObtenerAsync(ct);

        var chgEstMap = await _catalogo.EstadosOcAsync(ct);
        var estMap = await _catalogo.EstadosTicketAsync(ct);
        var catMap = await SymCategoriasAsync(tickets.Select(t => t.category), ct);
        var chgCatMap = await SymCategoriasOcAsync(changes.Select(c => c.category), ct);
        var contactos = await _catalogo.ContactosAsync(
            changes.SelectMany(c => new[] { c.assignee, c.group_id })
                   .Concat(tickets.SelectMany(t => new[] { t.assignee, t.group_id, t.customer })), ct);
        string? Nombre(byte[]? u) => u == null ? null : contactos.TryGetValue(Convert.ToHexString(u), out var n) ? n.Completo : null;
        string? Grupo(byte[]? u) => u == null ? null : contactos.TryGetValue(Convert.ToHexString(u), out var n) ? n.Apellido : null;

        // SLA de los tickets de ESTAS OC (attached_sla_x0).
        var crIds = tickets.Select(t => t.id).ToList();
        var slas = await SlaPorTicketAsync(crIds, ct);

        // Complejidad (base local) solo de estas OC.
        var chgRefs = changes.Select(c => c.chg_ref_num).ToList();
        var complejidades = await _analytics.OrdenCambioComplejidades.AsNoTracking()
            .Where(o => chgRefs.Contains(o.ChgRefNum)).ToDictionaryAsync(o => o.ChgRefNum, o => o.Complejidad, ct);

        var result = new List<OrdenCambioConTicketsDto>();
        foreach (var chg in changes)
        {
            var susTickets = tickets.Where(t => t.change == chg.id).Select(cr =>
            {
                var catSym = cr.category != null && catMap.TryGetValue(cr.category, out var cs) ? cs : null;
                slas.TryGetValue(cr.id, out var sla);
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
                    ResueltoDentroSLA = CalcularSla(cr.close_date, sla?.Ttv, sla?.Viol),
                    CerradoConCambioPendiente = cr.active_flag == 0 && (chg.close_date == null || chg.close_date == 0)
                };
            }).ToList();

            NivelComplejidad? nivel = complejidades.TryGetValue(chg.chg_ref_num, out var n) ? n : null;
            string? complejidad = nivel?.ToString();

            // Vencimiento = apertura + días según la complejidad (parámetros "OC.DiasComplejidad.*").
            var apertura = EpochRD.ToDateTime(chg.open_date);
            var cierre = EpochRD.ToDateTime(chg.close_date);
            var cerrada = cierre.HasValue;
            DateTime? vencimiento = apertura?.AddDays(p.DiasOc(nivel));

            int? diasRestantes = vencimiento.HasValue ? (int)Math.Floor((vencimiento.Value.Date - DateTime.Today).TotalDays) : null;
            double? pctConsumido = null;
            if (apertura.HasValue && vencimiento.HasValue && vencimiento > apertura)
            {
                var total = (vencimiento.Value - apertura.Value).TotalSeconds;
                var transcurrido = ((cierre ?? DateTime.Now) - apertura.Value).TotalSeconds;
                pctConsumido = Math.Clamp(transcurrido / total * 100.0, 0, 150);
            }

            string estadoVenc =
                  cerrada ? "completada"
                : !vencimiento.HasValue ? "sin_fecha"
                : diasRestantes < 0 ? "vencida"
                : diasRestantes == 0 ? "vence_hoy"
                : diasRestantes <= p.OcDiasVencePronto ? "vence_pronto"
                : "en_curso";

            var catLegible = chg.category != null && chgCatMap.TryGetValue(chg.category, out var cl) ? (cl ?? chg.category) : chg.category;

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
                OCResueltoDentroSLA = (cierre.HasValue && vencimiento.HasValue) ? (cierre.Value <= vencimiento.Value ? "SI" : "NO") : "N/A",
                Tickets = susTickets
            });
        }
        return result;
    }

    // ---- Enriquecimiento de una página de tickets ----
    // Antes: 8 viajes a MDB por página (categorías, estados, contactos, OC, estados de OC,
    // contactos de OC, SLA, complejidad). Ahora: catálogos y contactos desde la caché; a MDB
    // solo van las OC de la página (PK) y sus SLA (attached_sla_x0).
    private async Task<List<TicketEnriquecidoDto>> EnriquecerAsync(List<CallReq> tickets, CancellationToken ct)
    {
        if (tickets.Count == 0) return new();
        var p = await _parametros.ObtenerAsync(ct);

        var changeIds = tickets.Where(t => t.change is > 0).Select(t => t.change!.Value).Distinct().ToList();
        var crIds = tickets.Select(t => t.id).ToList();

        var catMap = await SymCategoriasAsync(tickets.Select(t => t.category), ct);
        var estMap = await _catalogo.EstadosTicketAsync(ct);
        var chgEstMap = await _catalogo.EstadosOcAsync(ct);
        var changes = changeIds.Count == 0
            ? new List<Chg>()
            : await _sdm.Changes.AsNoTracking().Where(c => changeIds.Contains(c.id)).ToListAsync(ct);
        var slas = await SlaPorTicketAsync(crIds, ct);
        var contactos = await _catalogo.ContactosAsync(
            tickets.SelectMany(t => new[] { t.assignee, t.group_id, t.customer }).Concat(changes.Select(c => c.assignee)), ct);
        string? Nombre(byte[]? u) => u == null ? null : contactos.TryGetValue(Convert.ToHexString(u), out var n) ? n.Completo : null;
        string? Grupo(byte[]? u) => u == null ? null : contactos.TryGetValue(Convert.ToHexString(u), out var n) ? n.Apellido : null;

        var chgRefs = changes.Select(c => c.chg_ref_num).ToList();
        var complejidades = chgRefs.Count == 0
            ? new Dictionary<string, NivelComplejidad>()
            : await _analytics.OrdenCambioComplejidades.AsNoTracking().Where(o => chgRefs.Contains(o.ChgRefNum))
                .ToDictionaryAsync(o => o.ChgRefNum, o => o.Complejidad, ct);

        var result = new List<TicketEnriquecidoDto>();
        foreach (var cr in tickets)
        {
            var catSym = cr.category != null && catMap.TryGetValue(cr.category, out var cs) ? cs : null;
            var tipoCaso = catSym?.Contains(".Problema/Error.") == true ? "Incidente" : catSym?.Contains(".Solicitud.") == true ? "Solicitud" : "Otro";
            var chg = cr.change is > 0 ? changes.FirstOrDefault(c => c.id == cr.change!.Value) : null;
            slas.TryGetValue(cr.id, out var sla);
            string? complejidad = chg != null && complejidades.TryGetValue(chg.chg_ref_num, out var nivel) ? nivel.ToString() : null;

            // Incumplimiento previsto = fecha de vencimiento del SLA (attached_sla).
            var vencTicket = EpochRD.ToDateTime(sla?.Ttv);
            var cerradoTicket = EpochRD.ToDateTime(cr.close_date).HasValue;
            var (estadoVencTicket, diasVencer) = EstadoVencimientoTicket(vencTicket, cerradoTicket, p.TicketDiasVencePronto);

            result.Add(new TicketEnriquecidoDto
            {
                TipoCaso = tipoCaso,
                NoTicket = cr.ref_num,
                Descripcion = Limpiar(cr.description),
                Categoria = catSym,
                Estado = cr.status != null && estMap.TryGetValue(cr.status, out var es) ? es : cr.status,
                UsuarioFinalAfectado = Nombre(cr.customer),
                Asignatario = Nombre(cr.assignee),
                Grupo = Grupo(cr.group_id),
                FechaApertura = EpochRD.ToDateTime(cr.open_date),
                FechaCierreSolicitado = EpochRD.ToDateTime(cr.close_date),
                ResueltoDentroSLA = CalcularSla(cr.close_date, sla?.Ttv, sla?.Viol),
                SlaViolationFlag = cr.sla_violation == 1,
                FechaVencimiento = vencTicket,
                DiasParaVencer = diasVencer,
                EstadoVencimiento = estadoVencTicket,
                NoOC = chg?.chg_ref_num,
                EstadoOC = chg?.status != null && chgEstMap.TryGetValue(chg.status, out var ce) ? ce : chg?.status,
                AsignatarioOC = Nombre(chg?.assignee),
                FechaAperturaOC = chg != null ? EpochRD.ToDateTime(chg.open_date) : null,
                FechaCierreSolicitadoOC = chg != null ? EpochRD.ToDateTime(chg.close_date) : null,
                ComplejidadOC = complejidad,
                OCResueltoDentroSLA = chg != null ? (chg.sla_violation == 1 ? "NO" : "SI") : null,
                ReqCerradoConCambioPendiente = cr.active_flag == 0 && chg != null && (chg.close_date == null || chg.close_date == 0)
            });
        }
        return result;
    }

    private sealed record SlaTicket(int Id, int? Ttv, int? Viol);

    /// <summary>Primer attached_sla (por id) de cada ticket. Solo los ids recibidos (una página).</summary>
    private async Task<Dictionary<int, SlaTicket>> SlaPorTicketAsync(List<int> crIds, CancellationToken ct)
    {
        if (crIds.Count == 0) return new();
        var filas = await _sdm.AttachedSlas.AsNoTracking()
            .Where(s => s.ticket_type == "cr" && s.del == 0 && crIds.Contains(s.ticket_id))
            .Select(s => new { s.id, s.ticket_id, s.time_to_violation, s.sla_viol_status })
            .ToListAsync(ct);
        return filas.GroupBy(s => s.ticket_id)
            .ToDictionary(g => g.Key, g => { var s = g.OrderBy(x => x.id).First(); return new SlaTicket(s.id, s.time_to_violation, s.sla_viol_status); });
    }

    /// <summary>Nombre legible (prob_ctg.sym) de categorías de ticket: caché + consulta solo de las que falten.</summary>
    private async Task<IReadOnlyDictionary<string, string?>> SymCategoriasAsync(IEnumerable<string?> persids, CancellationToken ct)
    {
        var mapa = new Dictionary<string, string?>(await _catalogo.SymCategoriasTicketAsync(ct));
        var faltan = persids.Where(x => x != null && !mapa.ContainsKey(x)).Select(x => x!).Distinct().ToList();
        if (faltan.Count > 0)
            foreach (var x in await _sdm.ProbCtgs.AsNoTracking().Where(pc => faltan.Contains(pc.persid)).Select(pc => new { pc.persid, pc.sym }).ToListAsync(ct))
                mapa[x.persid] = x.sym;
        return mapa;
    }

    /// <summary>Nombre legible (chgcat.sym) de categorías de OC: caché + consulta solo de las que falten.</summary>
    private async Task<IReadOnlyDictionary<string, string?>> SymCategoriasOcAsync(IEnumerable<string?> codes, CancellationToken ct)
    {
        var mapa = new Dictionary<string, string?>(await _catalogo.CategoriasOcAreaAsync(ct));
        var faltan = codes.Where(x => x != null && !mapa.ContainsKey(x)).Select(x => x!).Distinct().ToList();
        if (faltan.Count > 0)
            foreach (var x in await _sdm.ChgCats.AsNoTracking().Where(c => faltan.Contains(c.code)).Select(c => new { c.code, c.sym }).ToListAsync(ct))
                mapa[x.code] = x.sym;
        return mapa;
    }

    /// <summary>Estado de vencimiento de un ticket a partir de su SLA (regla única para toda la app).</summary>
    public static (string Estado, int? Dias) EstadoVencimientoTicket(DateTime? venc, bool cerrado, int diasVencePronto)
    {
        int? dias = venc.HasValue ? (int)Math.Floor((venc.Value.Date - DateTime.Today).TotalDays) : null;
        var estado = cerrado ? "cerrado"
            : !venc.HasValue ? "sin_fecha"
            : dias < 0 ? "vencido"
            : dias == 0 ? "vence_hoy"
            : dias <= diasVencePronto ? "vence_pronto"
            : "en_curso";
        return (estado, dias);
    }

    /// <summary>Alcance (persid) desde la caché, reducido por tipo de caso y por el filtro de categorías.</summary>
    public async Task<List<string>> ResolverAlcancePublicoAsync(List<string>? categoriasFiltro, CancellationToken ct = default)
    {
        var alcance = (await _catalogo.AlcanceAsync(ct)).Select(a => a.Persid).ToList();
        if (categoriasFiltro is { Count: > 0 }) alcance = alcance.Where(p => categoriasFiltro.Contains(p)).ToList();
        return alcance;
    }

    private async Task<List<string>> ResolverAlcanceAsync(TicketFiltro f, CancellationToken ct)
    {
        var categorias = await _catalogo.AlcanceAsync(ct);
        if (categorias.Count == 0)
        {
            _logger.LogWarning("SIN CATEGORIAS EN ALCANCE en la base local. La búsqueda devolverá CERO tickets. " +
                               "Agrega categorías con POST /api/admin/categorias o revisa el seed.");
            return new();
        }

        // Tipo de caso (Incidente / Solicitud): el catálogo local clasifica cada categoría.
        var alcance = categorias
            .Where(c => string.IsNullOrWhiteSpace(f.TipoCaso) || string.Equals(c.TipoCaso, f.TipoCaso, StringComparison.Ordinal))
            .Select(c => c.Persid).ToList();
        if (alcance.Count == 0)
            _logger.LogWarning("Ninguna categoría en alcance es de tipo {Tipo}. Cero resultados.", f.TipoCaso);

        if (f.Categorias is { Count: > 0 })
        {
            var filtrado = alcance.Where(p => f.Categorias.Contains(p)).ToList();
            if (filtrado.Count == 0)
                _logger.LogWarning("El filtro de categorías {Filtro} no coincide con ninguna categoría en alcance {Alcance}. Cero resultados.",
                    string.Join(",", f.Categorias), string.Join(",", alcance));
            return filtrado;
        }
        return alcance;
    }

    private static string CalcularSla(int? closeDate, int? ttv, int? violStatus)
    {
        if (ttv is null or 0) return "N/A";
        if (closeDate is > 0) return closeDate <= ttv ? "SI" : "NO";
        return violStatus == 1 ? "NO" : "EN CURSO";
    }

    private static string? Limpiar(string? s) => s?.Replace("\r", " ").Replace("\n", " ").Replace(";", " ").Trim();

    /// <summary>Convierte hex a bytes. Devuelve null si el formato es inválido (en vez de un 500).</summary>
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
