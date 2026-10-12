using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Common.Models;
using TicketAnalytics.Application.TrabajoDiario.DTOs;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Services;

public class TrabajoDiarioService : ITrabajoDiarioService
{
    private readonly AnalyticsContext _db;
    private readonly SdmReadContext _sdm;
    public TrabajoDiarioService(AnalyticsContext db, SdmReadContext sdm) { _db = db; _sdm = sdm; }

    public static string TipoTexto(TipoActividad t) => t switch
    {
        TipoActividad.OrdenCambio => "Orden de Cambio",
        TipoActividad.Incidente => "Incidente",
        TipoActividad.Solicitud => "Solicitud",
        TipoActividad.Proyecto => "Proyecto",
        TipoActividad.ApoyoQA => "Apoyo a QA",
        TipoActividad.PreparacionAmbiente => "Preparación de Ambiente",
        _ => t.ToString()
    };
    public static string EstadoTexto(EstadoActividad e) => e switch
    {
        EstadoActividad.Pendiente => "Pendiente",
        EstadoActividad.EnProceso => "En proceso",
        EstadoActividad.Completado => "Completado",
        EstadoActividad.Bloqueado => "Bloqueado",
        EstadoActividad.Cancelado => "Cancelado",
        _ => e.ToString()
    };

    // ============ TECNICOS ============
    public async Task<IReadOnlyList<TecnicoDto>> ListarTecnicosAsync(CancellationToken ct = default)
        => await _db.Tecnicos.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.Nombre)
            .Select(t => new TecnicoDto { TecnicoId = t.TecnicoId, Nombre = t.Nombre, Activo = t.Activo, EsAnalistaMdb = t.ContactUuid != null })
            .ToListAsync(ct);

    /// <summary>Llena el catalogo de tecnicos desde los analistas reales de mdb
    /// (los que tienen tickets asignados en las categorias en alcance).</summary>
    public async Task<int> SincronizarTecnicosDesdeMdbAsync(CancellationToken ct = default)
    {
        var alcance = await _db.CategoriasAlcance.AsNoTracking().Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
        if (!alcance.Any()) return 0;

        // UUIDs de assignees reales
        var uuids = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.assignee != null)
            .Select(cr => cr.assignee!).Distinct().ToListAsync(ct);

        var contactos = await _sdm.CaContacts.AsNoTracking()
            .Where(c => uuids.Contains(c.contact_uuid))
            .Select(c => new { c.contact_uuid, c.first_name, c.last_name }).ToListAsync(ct);

        var existentes = await _db.Tecnicos.ToListAsync(ct);
        int agregados = 0;
        foreach (var c in contactos)
        {
            var nombre = $"{c.first_name} {c.last_name}".Trim();
            if (string.IsNullOrWhiteSpace(nombre)) continue;
            if (existentes.Any(e => e.ContactUuid != null && e.ContactUuid.SequenceEqual(c.contact_uuid))) continue;
            if (existentes.Any(e => e.Nombre == nombre)) { // enlazar por nombre
                var t = existentes.First(e => e.Nombre == nombre);
                if (t.ContactUuid == null) t.ContactUuid = c.contact_uuid;
                continue;
            }
            _db.Tecnicos.Add(new Tecnico { Nombre = nombre, ContactUuid = c.contact_uuid, Activo = true });
            agregados++;
        }
        await _db.SaveChangesAsync(ct);
        return agregados;
    }

    public async Task<TecnicoDto> CrearTecnicoAsync(string nombre, CancellationToken ct = default)
    {
        var existe = await _db.Tecnicos.FirstOrDefaultAsync(t => t.Nombre == nombre, ct);
        if (existe != null) return new TecnicoDto { TecnicoId = existe.TecnicoId, Nombre = existe.Nombre, Activo = existe.Activo };
        var t = new Tecnico { Nombre = nombre, Activo = true };
        _db.Tecnicos.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TecnicoDto { TecnicoId = t.TecnicoId, Nombre = t.Nombre, Activo = true };
    }

    // ============ ACTIVIDADES (CRUD) ============
    public async Task<PagedResult<ActividadDto>> BuscarAsync(ActividadFiltro f, CancellationToken ct = default)
    {
        var q = _db.ActividadesDiarias.AsNoTracking().Include(a => a.Tecnico).AsQueryable();

        if (f.FechaDesde.HasValue) q = q.Where(a => a.Fecha >= f.FechaDesde.Value);
        if (f.FechaHasta.HasValue) q = q.Where(a => a.Fecha < f.FechaHasta.Value.AddDays(1));
        if (f.TecnicoId.HasValue) q = q.Where(a => a.TecnicoId == f.TecnicoId.Value);
        if (f.Tipo.HasValue) q = q.Where(a => (int)a.Tipo == f.Tipo.Value);
        if (f.Estado.HasValue) q = q.Where(a => (int)a.Estado == f.Estado.Value);
        if (!string.IsNullOrWhiteSpace(f.Proyecto)) q = q.Where(a => a.Proyecto != null && a.Proyecto.Contains(f.Proyecto));
        if (!string.IsNullOrWhiteSpace(f.NoSolicitud)) q = q.Where(a => a.NoSolicitud != null && a.NoSolicitud.Contains(f.NoSolicitud));
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            q = q.Where(a => a.Actividad.Contains(t) || (a.Proyecto != null && a.Proyecto.Contains(t))
                          || (a.NoSolicitud != null && a.NoSolicitud.Contains(t)) || (a.Tecnico != null && a.Tecnico.Nombre.Contains(t)));
        }

        var total = await q.CountAsync(ct);

        q = (f.OrderBy?.ToLowerInvariant(), f.OrderDesc) switch
        {
            ("tecnico", true) => q.OrderByDescending(a => a.Tecnico!.Nombre),
            ("tecnico", false) => q.OrderBy(a => a.Tecnico!.Nombre),
            ("horas", true) => q.OrderByDescending(a => a.Horas),
            ("horas", false) => q.OrderBy(a => a.Horas),
            ("estado", true) => q.OrderByDescending(a => a.Estado),
            ("estado", false) => q.OrderBy(a => a.Estado),
            ("fecha", false) => q.OrderBy(a => a.Fecha).ThenByDescending(a => a.ActividadId),
            _ => q.OrderByDescending(a => a.Fecha).ThenByDescending(a => a.ActividadId)
        };

        var pagina = f.Page < 1 ? 1 : f.Page;
        var tam = f.PageSize is < 1 or > 200 ? 10 : f.PageSize;
        var items = await q.Skip((pagina - 1) * tam).Take(tam).Select(a => Map(a)).ToListAsync(ct);

        return new PagedResult<ActividadDto> { Items = items, TotalCount = total, Page = pagina, PageSize = tam };
    }

    public async Task<ActividadDto> GuardarAsync(GuardarActividadRequest req, CancellationToken ct = default)
    {
        // Validaciones de negocio
        if (req.Horas <= 0 || req.Horas > 24)
            throw new InvalidOperationException("Las horas deben ser mayores a 0 y no exceder 24 por registro.");
        if (string.IsNullOrWhiteSpace(req.Actividad))
            throw new InvalidOperationException("La actividad es obligatoria.");
        if (req.TecnicoId <= 0 || !await _db.Tecnicos.AnyAsync(t => t.TecnicoId == req.TecnicoId, ct))
            throw new InvalidOperationException("Debe seleccionar un técnico válido.");

        ActividadDiaria a;
        if (req.ActividadId.HasValue)
        {
            a = await _db.ActividadesDiarias.FirstOrDefaultAsync(x => x.ActividadId == req.ActividadId.Value, ct)
                ?? throw new InvalidOperationException("La actividad a editar no existe.");
            a.ModificadoPor = req.Usuario;
            a.ModificadoEn = DateTime.UtcNow;
        }
        else
        {
            a = new ActividadDiaria { CreadoPor = req.Usuario, CreadoEn = DateTime.UtcNow };
            _db.ActividadesDiarias.Add(a);
        }

        a.Fecha = req.Fecha;
        a.TecnicoId = req.TecnicoId;
        a.Actividad = req.Actividad.Trim();
        a.Tipo = (TipoActividad)req.Tipo;
        a.NoSolicitud = req.NoSolicitud?.Trim();
        a.RefNumVinculado = req.RefNumVinculado?.Trim();
        a.TipoVinculo = req.TipoVinculo;
        a.Proyecto = req.Proyecto?.Trim();
        a.Horas = req.Horas;
        a.Estado = (EstadoActividad)req.Estado;
        a.Comentario = req.Comentario?.Trim();

        await _db.SaveChangesAsync(ct);
        await _db.Entry(a).Reference(x => x.Tecnico).LoadAsync(ct);
        return Map(a);
    }

    public async Task<bool> EliminarAsync(long actividadId, string? usuario, CancellationToken ct = default)
    {
        var a = await _db.ActividadesDiarias.FirstOrDefaultAsync(x => x.ActividadId == actividadId, ct);
        if (a is null) return false;
        _db.ActividadesDiarias.Remove(a);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ============ RESUMEN DE JORNADA (KPIs + graficos) ============
    public async Task<ResumenJornadaDto> ResumenAsync(ActividadFiltro f, CancellationToken ct = default)
    {
        var q = _db.ActividadesDiarias.AsNoTracking().Include(a => a.Tecnico).AsQueryable();
        if (f.FechaDesde.HasValue) q = q.Where(a => a.Fecha >= f.FechaDesde.Value);
        if (f.FechaHasta.HasValue) q = q.Where(a => a.Fecha < f.FechaHasta.Value.AddDays(1));
        if (f.TecnicoId.HasValue) q = q.Where(a => a.TecnicoId == f.TecnicoId.Value);
        if (f.Tipo.HasValue) q = q.Where(a => (int)a.Tipo == f.Tipo.Value);
        if (f.Estado.HasValue) q = q.Where(a => (int)a.Estado == f.Estado.Value);
        if (!string.IsNullOrWhiteSpace(f.Proyecto)) q = q.Where(a => a.Proyecto != null && a.Proyecto.Contains(f.Proyecto));
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            q = q.Where(a => a.Actividad.Contains(t) || (a.Proyecto != null && a.Proyecto.Contains(t))
                          || (a.NoSolicitud != null && a.NoSolicitud.Contains(t)) || (a.Tecnico != null && a.Tecnico.Nombre.Contains(t)));
        }

        var datos = await q.Select(a => new
        {
            a.Horas, a.Estado, a.Tipo, a.Fecha, Tecnico = a.Tecnico!.Nombre,
            a.Proyecto, a.RefNumVinculado
        }).ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var inicioSemana = hoy.AddDays(-((int)DateTime.Today.DayOfWeek == 0 ? 6 : (int)DateTime.Today.DayOfWeek - 1));
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);

        var r = new ResumenJornadaDto
        {
            TotalActividades = datos.Count,
            TotalHoras = datos.Sum(x => x.Horas),
            Completadas = datos.Count(x => x.Estado == EstadoActividad.Completado),
            Pendientes = datos.Count(x => x.Estado == EstadoActividad.Pendiente),
            EnProceso = datos.Count(x => x.Estado == EstadoActividad.EnProceso),
            Bloqueadas = datos.Count(x => x.Estado == EstadoActividad.Bloqueado),
            Canceladas = datos.Count(x => x.Estado == EstadoActividad.Cancelado),
            HorasHoy = datos.Where(x => x.Fecha == hoy).Sum(x => x.Horas),
            HorasSemana = datos.Where(x => x.Fecha >= inicioSemana).Sum(x => x.Horas),
            HorasMes = datos.Where(x => x.Fecha >= inicioMes).Sum(x => x.Horas),
            OrdenesCambioTrabajadas = datos.Count(x => x.Tipo == TipoActividad.OrdenCambio),
            IncidentesTrabajados = datos.Count(x => x.Tipo == TipoActividad.Incidente)
        };
        r.PromedioHorasPorActividad = datos.Count > 0 ? Math.Round(r.TotalHoras / datos.Count, 2) : 0;
        r.PctCompletadas = datos.Count > 0 ? Math.Round(100.0 * r.Completadas / datos.Count, 1) : 0;

        // Por técnico (con métricas ampliadas)
        var totalHoras = r.TotalHoras;
        r.PorTecnico = datos.GroupBy(x => x.Tecnico).Select(g =>
        {
            var horas = g.Sum(x => x.Horas);
            var total = g.Count();
            var comp = g.Count(x => x.Estado == EstadoActividad.Completado);
            var proyPrincipal = g.Where(x => !string.IsNullOrEmpty(x.Proyecto))
                .GroupBy(x => x.Proyecto).OrderByDescending(pg => pg.Sum(x => x.Horas)).FirstOrDefault()?.Key;
            return new CargaTecnicoDto
            {
                Tecnico = g.Key,
                Horas = horas,
                TotalActividades = total,
                Completadas = comp,
                PendientesEnProceso = g.Count(x => x.Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso),
                Bloqueadas = g.Count(x => x.Estado == EstadoActividad.Bloqueado),
                Activas = g.Count(x => x.Estado != EstadoActividad.Cancelado),
                PctDelTotalHoras = totalHoras > 0 ? Math.Round((double)(horas / totalHoras) * 100, 1) : 0,
                PctFinalizacion = total > 0 ? Math.Round(100.0 * comp / total, 1) : 0,
                PromedioHorasPorActividad = total > 0 ? Math.Round(horas / total, 2) : 0,
                ProyectoPrincipal = proyPrincipal,
                UltimaActividad = g.Max(x => x.Fecha)
            };
        }).OrderByDescending(x => x.Horas).ToList();

        // Nivel de carga: relativo al máximo de horas
        var maxHoras = r.PorTecnico.Any() ? r.PorTecnico.Max(t => t.Horas) : 0;
        foreach (var t in r.PorTecnico)
            t.NivelCarga = maxHoras == 0 ? "baja"
                : (double)(t.Horas / maxHoras) >= 0.66 ? "alta"
                : (double)(t.Horas / maxHoras) >= 0.33 ? "media" : "baja";

        r.PromedioHorasPorTecnico = r.PorTecnico.Any() ? Math.Round(r.TotalHoras / r.PorTecnico.Count, 2) : 0;
        r.TecnicoMasHoras = r.PorTecnico.FirstOrDefault()?.Tecnico;
        r.TecnicoMasActividades = r.PorTecnico.OrderByDescending(t => t.TotalActividades).FirstOrDefault()?.Tecnico;

        r.PorTipo = datos.GroupBy(x => x.Tipo).Select(g => new ConteoDto
        { Etiqueta = TipoTexto(g.Key), Cantidad = g.Count(), Horas = g.Sum(x => x.Horas) })
            .OrderByDescending(x => x.Cantidad).ToList();

        r.PorEstado = datos.GroupBy(x => x.Estado).Select(g => new ConteoDto
        { Etiqueta = EstadoTexto(g.Key), Cantidad = g.Count(), Horas = g.Sum(x => x.Horas) }).ToList();

        r.HorasPorDia = datos.GroupBy(x => x.Fecha).Select(g => new HorasDiaDto { Dia = g.Key, Horas = g.Sum(x => x.Horas) })
            .OrderBy(x => x.Dia).TakeLast(14).ToList();

        r.ActividadesPorTecnico = datos.GroupBy(x => x.Tecnico).Select(g => new ConteoDto { Etiqueta = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad).ToList();

        // Por semana ISO y por mes
        r.HorasPorSemana = datos.GroupBy(x => SemanaClave(x.Fecha)).Select(g => new HorasPeriodoDto { Periodo = g.Key, Horas = g.Sum(x => x.Horas) })
            .OrderBy(x => x.Periodo).TakeLast(8).ToList();
        r.HorasPorMes = datos.GroupBy(x => x.Fecha.ToString("yyyy-MM")).Select(g => new HorasPeriodoDto { Periodo = g.Key, Horas = g.Sum(x => x.Horas) })
            .OrderBy(x => x.Periodo).TakeLast(12).ToList();

        // Por proyecto (top 10 esfuerzo)
        var conProyecto = datos.Where(x => !string.IsNullOrEmpty(x.Proyecto)).ToList();
        r.HorasPorProyecto = conProyecto.GroupBy(x => x.Proyecto!).Select(g => new ConteoDto { Etiqueta = g.Key, Horas = g.Sum(x => x.Horas), Cantidad = g.Count() })
            .OrderByDescending(x => x.Horas).Take(10).ToList();
        r.ActividadesPorProyecto = conProyecto.GroupBy(x => x.Proyecto!).Select(g => new ConteoDto { Etiqueta = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad).Take(10).ToList();
        r.ProyectoMasDedicacion = r.HorasPorProyecto.FirstOrDefault()?.Etiqueta;

        // Completadas vs pendientes por técnico
        r.CompletadasVsPendientes = datos.GroupBy(x => x.Tecnico).Select(g => new CompletadasVsPendientesDto
        {
            Tecnico = g.Key,
            Completadas = g.Count(x => x.Estado == EstadoActividad.Completado),
            Pendientes = g.Count(x => x.Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso or EstadoActividad.Bloqueado)
        }).OrderByDescending(x => x.Completadas + x.Pendientes).Take(10).ToList();

        // Tickets atendidos por técnico (actividades enlazadas a ticket/OC real)
        r.TicketsPorTecnico = datos.Where(x => !string.IsNullOrEmpty(x.RefNumVinculado))
            .GroupBy(x => x.Tecnico).Select(g => new ConteoDto { Etiqueta = g.Key, Cantidad = g.Select(x => x.RefNumVinculado).Distinct().Count() })
            .OrderByDescending(x => x.Cantidad).ToList();

        // Comparación con período anterior (misma ventana, justo antes)
        if (f.FechaDesde.HasValue && f.FechaHasta.HasValue)
        {
            var dias = f.FechaHasta.Value.DayNumber - f.FechaDesde.Value.DayNumber + 1;
            var antHasta = f.FechaDesde.Value.AddDays(-1);
            var antDesde = antHasta.AddDays(-(dias - 1));
            var qa = _db.ActividadesDiarias.AsNoTracking().Where(a => a.Fecha >= antDesde && a.Fecha <= antHasta);
            if (f.TecnicoId.HasValue) qa = qa.Where(a => a.TecnicoId == f.TecnicoId.Value);
            var datosAnt = await qa.Select(a => new { a.Horas }).ToListAsync(ct);
            r.TotalActividadesAnterior = datosAnt.Count;
            r.TotalHorasAnterior = datosAnt.Sum(x => x.Horas);
        }

        return r;
    }

    private static string SemanaClave(DateOnly d)
    {
        var dt = d.ToDateTime(TimeOnly.MinValue);
        var jueves = dt.AddDays(3 - ((int)dt.DayOfWeek + 6) % 7);
        var semana = System.Globalization.ISOWeek.GetWeekOfYear(jueves);
        return $"{jueves:yyyy}-S{semana:00}";
    }

    // ============ ENLACE CON MDB ============
    public async Task<IReadOnlyList<OpcionTicketDto>> BuscarTicketsAsync(string termino, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(termino) || termino.Length < 3) return Array.Empty<OpcionTicketDto>();
        var t = termino.Trim();

        var tickets = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => cr.ref_num.Contains(t))
            .OrderByDescending(cr => cr.open_date).Take(10)
            .Select(cr => new OpcionTicketDto { RefNum = cr.ref_num, TipoVinculo = "cr", Descripcion = cr.summary })
            .ToListAsync(ct);

        var ocs = await _sdm.Changes.AsNoTracking()
            .Where(chg => chg.chg_ref_num!.Contains(t))
            .OrderByDescending(chg => chg.open_date).Take(10)
            .Select(chg => new OpcionTicketDto { RefNum = chg.chg_ref_num!, TipoVinculo = "chg", Descripcion = chg.summary })
            .ToListAsync(ct);

        return tickets.Concat(ocs).Take(15).ToList();
    }

    public async Task<IReadOnlyList<ActividadDto>> ActividadesDeTicketAsync(string refNum, CancellationToken ct = default)
        => await _db.ActividadesDiarias.AsNoTracking().Include(a => a.Tecnico)
            .Where(a => a.RefNumVinculado == refNum || a.NoSolicitud == refNum)
            .OrderByDescending(a => a.Fecha).Select(a => Map(a)).ToListAsync(ct);

    private static ActividadDto Map(ActividadDiaria a) => new()
    {
        ActividadId = a.ActividadId, Fecha = a.Fecha, TecnicoId = a.TecnicoId,
        TecnicoNombre = a.Tecnico?.Nombre ?? "", Actividad = a.Actividad,
        Tipo = (int)a.Tipo, TipoTexto = TipoTexto(a.Tipo),
        NoSolicitud = a.NoSolicitud, RefNumVinculado = a.RefNumVinculado, TipoVinculo = a.TipoVinculo,
        Proyecto = a.Proyecto, Horas = a.Horas, Estado = (int)a.Estado, EstadoTexto = EstadoTexto(a.Estado),
        Comentario = a.Comentario, CreadoPor = a.CreadoPor, CreadoEn = a.CreadoEn,
        ModificadoPor = a.ModificadoPor, ModificadoEn = a.ModificadoEn
    };
}
