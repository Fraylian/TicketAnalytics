using Microsoft.EntityFrameworkCore;
using TicketAnalytics.Application.Analistas.DTOs;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Application.Tickets.DTOs;
using TicketAnalytics.Domain.Common;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

namespace TicketAnalytics.Infrastructure.Repositories;

/// <summary>Metricas de productividad por analista, calculadas dinamicamente
/// segun el rango de fechas. Combina tickets/OC de mdb con seguimiento local.</summary>
public class AnalistaService : IAnalistaService
{
    private readonly SdmReadContext _sdm;
    private readonly AnalyticsContext _analytics;
    public AnalistaService(SdmReadContext sdm, AnalyticsContext analytics) { _sdm = sdm; _analytics = analytics; }

    public async Task<IReadOnlyList<MetricaAnalistaDto>> MetricasAsync(TicketFiltro f, CancellationToken ct = default)
    {
        var alcance = await _analytics.CategoriasAlcance.AsNoTracking().Where(c => c.Activo).Select(c => c.Persid).ToListAsync(ct);
        if (f.Categorias is { Count: > 0 }) alcance = alcance.Where(p => f.Categorias.Contains(p)).ToList();
        int desde = f.FechaDesde.HasValue ? EpochRD.InicioDelDia(f.FechaDesde.Value) : EpochRD.ToEpoch(new DateTime(2026, 1, 1));
        int hasta = f.FechaHasta.HasValue ? EpochRD.FinDelDiaExclusivo(f.FechaHasta.Value) : EpochRD.ToEpoch(DateTime.Now);

        // --- Tickets recibidos (por open_date) ---
        var recibidos = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.open_date >= desde && cr.open_date < hasta && cr.assignee != null)
            .Select(cr => new { cr.assignee, cr.open_date, cr.active_flag, cr.close_date })
            .ToListAsync(ct);

        // --- Tickets cerrados (por close_date) ---
        var cerrados = await _sdm.CallReqs.AsNoTracking()
            .Where(cr => alcance.Contains(cr.category!) && cr.close_date >= desde && cr.close_date < hasta && cr.close_date > 0 && cr.assignee != null)
            .Select(cr => new { cr.assignee, cr.open_date, cr.close_date })
            .ToListAsync(ct);

        // --- OC del área: filtrar por las categorías de Orden de Cambio (chgcat) ---
        var codigosOc = await _sdm.ChgCats.AsNoTracking()
            .Where(cc => cc.sym != null && cc.sym.Contains("Orden de Cambio"))
            .Select(cc => cc.code).ToListAsync(ct);

        var ocRecibidas = await _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && codigosOc.Contains(c.category)
                        && c.open_date >= desde && c.open_date < hasta && c.assignee != null)
            .Select(c => new { c.assignee, c.open_date }).ToListAsync(ct);
        var ocCerradas = await _sdm.Changes.AsNoTracking()
            .Where(c => c.category != null && codigosOc.Contains(c.category)
                        && c.close_date >= desde && c.close_date < hasta && c.close_date > 0 && c.assignee != null)
            .Select(c => new { c.assignee, c.close_date }).ToListAsync(ct);

        // Universo de analistas (por bytes)
        var uuidsTodos = recibidos.Select(r => r.assignee!).Concat(cerrados.Select(c => c.assignee!))
            .Concat(ocRecibidas.Select(o => o.assignee!)).Concat(ocCerradas.Select(o => o.assignee!))
            .Select(b => Convert.ToHexString(b)).Distinct().ToList();

        // LIMITAR al grupo del área: solo los 6 analistas (por su userid en ca_contact).
        var userIdsGrupo = TicketAnalytics.Domain.Common.GrupoAnalistas.UserIds;
        var uuidsGrupo = await _sdm.CaContacts.AsNoTracking()
            .Where(c => c.userid != null && userIdsGrupo.Contains(c.userid))
            .Select(c => c.contact_uuid).ToListAsync(ct);
        var hexGrupo = uuidsGrupo.Select(b => Convert.ToHexString(b)).ToHashSet();

        // Si logramos resolver el grupo, filtramos a ellos; si no (userid vacío), dejamos todos.
        var uuids = hexGrupo.Count > 0 ? uuidsTodos.Where(h => hexGrupo.Contains(h)).ToList() : uuidsTodos;

        // Nombres
        var todosBytes = uuids.Select(h => Convert.FromHexString(h)).ToList();
        var contactos = await _sdm.CaContacts.AsNoTracking().Where(c => todosBytes.Contains(c.contact_uuid)).ToListAsync(ct);
        string Nombre(string hex)
        {
            var b = Convert.FromHexString(hex);
            var c = contactos.FirstOrDefault(x => x.contact_uuid.SequenceEqual(b));
            return c != null ? $"{c.first_name} {c.last_name}".Trim() : "(desconocido)";
        }

        // Seguimiento local por tecnico
        var seguimientos = await _analytics.TicketSeguimientos.AsNoTracking()
            .Select(s => new { s.TecnicoUuid, s.TecnicoResponsable, s.EtapaActual, s.ProximoSeguimiento }).ToListAsync(ct);
        var actualizaciones = await _analytics.SeguimientoActualizaciones.AsNoTracking()
            .Where(a => a.CreadoEn >= (f.FechaDesde ?? DateTime.MinValue))
            .Select(a => a.Autor).ToListAsync(ct);

        DateOnly Dia(int? epoch) => DateOnly.FromDateTime(EpochRD.ToDateTime(epoch) ?? DateTime.MinValue);

        var result = new List<MetricaAnalistaDto>();
        foreach (var hex in uuids)
        {
            var bytes = Convert.FromHexString(hex);
            var rec = recibidos.Where(r => r.assignee!.SequenceEqual(bytes)).ToList();
            var cer = cerrados.Where(c => c.assignee!.SequenceEqual(bytes)).ToList();
            var ocRec = ocRecibidas.Where(o => o.assignee!.SequenceEqual(bytes)).ToList();
            var ocCer = ocCerradas.Where(o => o.assignee!.SequenceEqual(bytes)).ToList();

            var diasApertura = rec.Select(r => Dia(r.open_date)).Distinct().Count();
            var diasCierre = cer.Select(c => Dia(c.close_date)).Distinct().Count();
            var horasResol = cer.Where(c => c.close_date > 0)
                .Select(c => (EpochRD.ToDateTime(c.close_date)!.Value - EpochRD.ToDateTime(c.open_date)!.Value).TotalHours).ToList();

            // TecnicoUuid no lo llena ningún flujo actual; la gestión guarda el nombre en TecnicoResponsable.
            // Se usa el UUID si existe y, si no, el nombre del analista (antes estas métricas siempre daban 0).
            var nombreAnalista = Nombre(hex);
            var segsTecnico = seguimientos.Where(s =>
                (s.TecnicoUuid != null && s.TecnicoUuid.SequenceEqual(bytes))
                || (s.TecnicoUuid == null && string.Equals(s.TecnicoResponsable, nombreAnalista, StringComparison.OrdinalIgnoreCase))).ToList();

            result.Add(new MetricaAnalistaDto
            {
                Analista = Nombre(hex),
                AnalistaUuid = "0x" + hex,
                TotalRecibidos = rec.Count,
                TotalCerrados = cer.Count,
                TotalActivos = rec.Count(r => r.active_flag == 1),
                DiasConApertura = diasApertura,
                DiasConCierre = diasCierre,
                PromedioRecibidosDia = diasApertura > 0 ? Math.Round((decimal)rec.Count / diasApertura, 2) : 0,
                PromedioCerradosDia = diasCierre > 0 ? Math.Round((decimal)cer.Count / diasCierre, 2) : 0,
                PromedioHorasResolucion = horasResol.Count > 0 ? Math.Round(horasResol.Average(), 1) : 0,
                OcRecibidas = ocRec.Count,
                OcCerradas = ocCer.Count,
                OcDiasApertura = ocRec.Select(o => Dia(o.open_date)).Distinct().Count(),
                OcDiasCierre = ocCer.Select(o => Dia(o.close_date)).Distinct().Count(),
                OcPromedioRecibidasDia = ocRec.Select(o => Dia(o.open_date)).Distinct().Count() is var d1 && d1 > 0 ? Math.Round((decimal)ocRec.Count / d1, 2) : 0,
                OcPromedioCerradasDia = ocCer.Select(o => Dia(o.close_date)).Distinct().Count() is var d2 && d2 > 0 ? Math.Round((decimal)ocCer.Count / d2, 2) : 0,
                CasosPendientesSeguimiento = segsTecnico.Count(s => s.ProximoSeguimiento != null && s.ProximoSeguimiento <= DateTime.Now),
                CasosEnEsperaProveedor = segsTecnico.Count(s => (int)s.EtapaActual == 3), // EnEsperaProveedor
                ActualizacionesRealizadas = actualizaciones.Count(a => a == Nombre(hex))
            });
        }

        return result.OrderByDescending(x => x.TotalRecibidos).ToList();
    }

    public async Task<MetricaAnalistaDto?> MetricaDeAnalistaAsync(string uuidHex, TicketFiltro f, CancellationToken ct = default)
    {
        var todas = await MetricasAsync(f, ct);
        var hex = uuidHex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? uuidHex : "0x" + uuidHex;
        return todas.FirstOrDefault(m => string.Equals(m.AnalistaUuid, hex, StringComparison.OrdinalIgnoreCase));
    }
}
