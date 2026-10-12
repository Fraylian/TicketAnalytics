using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketAnalytics.Application.Casos.DTOs;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Domain.Entities;
using TicketAnalytics.Domain.Enums;
using TicketAnalytics.Infrastructure.Persistence.Analytics;

namespace TicketAnalytics.Infrastructure.Services;

/// <summary>
/// Guarda TODA la gestión de un caso en una sola transacción: seguimiento,
/// avance, proveedor, JIRA y configuración del reporte VP.
/// Antes esto eran cuatro llamadas HTTP independientes y si fallaba una
/// intermedia el caso quedaba con la gestión a medias.
/// </summary>
public class GestionCasoService : IGestionCasoService
{
    private readonly AnalyticsContext _db;
    private readonly ILogger<GestionCasoService> _logger;

    public GestionCasoService(AnalyticsContext db, ILogger<GestionCasoService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GestionCasoResultado> RegistrarAsync(GestionCasoRequest req, CancellationToken ct = default)
    {
        Validar(req);

        var tipo = req.TipoOrigen == "chg" ? "chg" : "cr";
        var res = new GestionCasoResultado { RefNum = req.RefNum };

        // El contexto usa EnableRetryOnFailure, y esa estrategia PROHÍBE abrir una
        // transacción a mano. Hay que pedirle a la propia estrategia que ejecute el
        // bloque completo (así puede reintentarlo entero si la conexión falla).
        var estrategia = _db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () => await EjecutarAsync(req, tipo, res, ct));
    }

    private async Task<GestionCasoResultado> EjecutarAsync(
        GestionCasoRequest req, string tipo, GestionCasoResultado res, CancellationToken ct)
    {
        // Cada reintento parte de cero: limpiamos el rastreo y los avisos previos.
        _db.ChangeTracker.Clear();
        res.Mensajes.Clear();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1) Seguimiento (identidad real: RefNum + TipoOrigen)
            var seg = await _db.TicketSeguimientos
                .FirstOrDefaultAsync(s => s.RefNum == req.RefNum && s.TipoOrigen == tipo, ct);

            if (seg is null)
            {
                seg = new TicketSeguimiento
                {
                    RefNum = req.RefNum.Trim(),
                    TipoOrigen = tipo,
                    Categoria = req.Categoria,
                    TipoCaso = req.TipoCaso,
                    EtapaActual = EtapaSeguimiento.PendienteAnalisis,
                    Activo = true,
                    CreadoEn = DateTime.UtcNow,
                    ActualizadoEn = DateTime.UtcNow,
                    CreadoPor = req.Usuario
                };
                _db.TicketSeguimientos.Add(seg);
                await _db.SaveChangesAsync(ct);      // necesitamos el Id para los hijos
                res.SeguimientoCreado = true;
            }

            var etapaAnterior = seg.EtapaActual;

            if (!string.IsNullOrWhiteSpace(req.TecnicoResponsable)) seg.TecnicoResponsable = req.TecnicoResponsable;
            if (!string.IsNullOrWhiteSpace(req.ProximosPasos)) seg.ProximosPasos = req.ProximosPasos;
            if (req.ProximoSeguimiento.HasValue) seg.ProximoSeguimiento = req.ProximoSeguimiento;

            // --- Datos estructurados de la gestión ---
            if (req.Sistema.HasValue && Enum.IsDefined(typeof(SistemaAfectado), req.Sistema.Value))
                seg.Sistema = (SistemaAfectado)req.Sistema.Value;
            if (req.TipoSolucion.HasValue && Enum.IsDefined(typeof(TipoSolucion), req.TipoSolucion.Value))
                seg.TipoSolucion = (TipoSolucion)req.TipoSolucion.Value;

            // Regla del punto 7: el subtipo SOLO se guarda en la combinación válida;
            // en cualquier otro caso se fuerza a NoAplica para no dejar datos huérfanos.
            if (TipoActualizacionDatosInfo.Aplica(seg.Sistema, seg.TipoSolucion)
                && req.TipoActualizacionDatos.HasValue
                && Enum.IsDefined(typeof(TipoActualizacionDatos), req.TipoActualizacionDatos.Value))
                seg.TipoActualizacionDatos = (TipoActualizacionDatos)req.TipoActualizacionDatos.Value;
            else
                seg.TipoActualizacionDatos = TipoActualizacionDatos.NoAplica;

            if (req.AreaDestino is not null) seg.AreaDestino = Recortar(req.AreaDestino, 120);
            if (req.CausaRaiz is not null) seg.CausaRaiz = Recortar(req.CausaRaiz, 1000);
            if (req.TicketCausaRaiz is not null) seg.TicketCausaRaiz = Recortar(req.TicketCausaRaiz, 40);
            if (req.FechaReporteProveedor.HasValue) seg.FechaReporteProveedor = req.FechaReporteProveedor;
            if (req.TicketProveedor is not null) seg.TicketProveedor = Recortar(req.TicketProveedor, 40);

            if (req.NuevaEtapa.HasValue && Enum.IsDefined(typeof(EtapaSeguimiento), req.NuevaEtapa.Value))
                seg.EtapaActual = (EtapaSeguimiento)req.NuevaEtapa.Value;

            // 2) Estado de reporte a proveedor (Sí / No / Pendiente)
            seg.ReportadoProveedor = (EstadoReporteProveedorTicket)req.ReportadoProveedor;

            // 3) Avance
            // Avance: los "Comentarios" del formulario se registran como avance del historial.
            var detalleAvance = req.Avance?.Detalle ?? req.Comentarios;
            if (!string.IsNullOrWhiteSpace(detalleAvance))
            {
                _db.SeguimientoActualizaciones.Add(new SeguimientoActualizacion
                {
                    SeguimientoId = seg.SeguimientoId,
                    TipoActualizacion = TipoAvanceTexto(req.Avance?.Tipo ?? 0),
                    Contenido = detalleAvance!.Trim(),
                    EtapaAnterior = etapaAnterior,
                    EtapaNueva = seg.EtapaActual,
                    Autor = req.Usuario ?? "sistema",
                    CreadoEn = DateTime.UtcNow
                });
                res.AvanceRegistrado = true;
            }

            // 4) Proveedor: solo se crea el reporte cuando de verdad va al proveedor.
            //    Con "Pendiente" NO se crea un reporte falso, solo queda la marca.
            // Si el caso YA tiene un reporte a proveedor, no se crea otro: se avisa.
            var yaReportadoProveedor = !res.SeguimientoCreado
                && await _db.ReportesProveedor.AnyAsync(r => r.SeguimientoId == seg.SeguimientoId, ct);

            if (req.ReportadoProveedor == (int)EstadoReporteProveedorTicket.Si && yaReportadoProveedor)
            {
                res.Mensajes.Add("Este caso ya tenía un reporte al proveedor; no se creó uno nuevo.");
                res.ProveedorYaExistia = true;
            }
            else if (req.ReportadoProveedor == (int)EstadoReporteProveedorTicket.Si
                && req.Proveedor is { ProveedorId: > 0 } p
                && await _db.Proveedores.AnyAsync(x => x.ProveedorId == p.ProveedorId, ct))
            {
                // Estado fuera de rango => Abierto, en lugar de rechazar la gestión completa.
                if (!Enum.IsDefined(typeof(EstadoReporteProveedor), p.Estado)) p.Estado = 0;

                _db.ReportesProveedor.Add(new ReporteProveedor
                {
                    SeguimientoId = seg.SeguimientoId,
                    ProveedorId = p.ProveedorId,
                    RefNumSistema = seg.RefNum,
                    TipoVinculo = tipo,
                    NumeroCasoExterno = p.NumeroCasoExterno,
                    Titulo = p.Titulo,
                    Descripcion = p.Descripcion,
                    ContextoEnviado = p.ContextoEnviado,
                    RespuestasRecibidas = p.RespuestasRecibidas,
                    ResultadoSolucion = p.ResultadoSolucion,
                    Urgencia = string.IsNullOrWhiteSpace(p.Urgencia) ? "Medio" : p.Urgencia!,
                    Estado = (EstadoReporteProveedor)p.Estado,
                    CumplioSla = p.CumplioSla,
                    FechaReporte = p.FechaReporte ?? DateTime.UtcNow,
                    ResponsableInterno = p.ResponsableInterno ?? req.Usuario,
                    UltimoSeguimiento = DateTime.UtcNow
                });
                res.ProveedorRegistrado = true;
            }
            else if (req.ReportadoProveedor == (int)EstadoReporteProveedorTicket.Pendiente)
            {
                res.Mensajes.Add("Marcado como pendiente de escalar al proveedor.");
            }
            else if (req.ReportadoProveedor == (int)EstadoReporteProveedorTicket.Si && !yaReportadoProveedor)
            {
                // Quedó marcado como reportado, pero sin proveedor válido no creamos
                // el reporte. El resto de la gestión sí se guarda.
                res.Mensajes.Add("Se guardó la marca de reportado, pero faltó seleccionar un proveedor válido.");
            }

            // 5) JIRA
            if (req.Jira is not null && !string.IsNullOrWhiteSpace(req.Jira.ClaveJira))
            {
                var clave = req.Jira.ClaveJira.Trim().ToUpperInvariant();
                var yaExiste = await _db.JiraRelaciones
                    .AnyAsync(j => j.SeguimientoId == seg.SeguimientoId && j.ClaveJira == clave, ct);

                if (!yaExiste)
                {
                    var jira = new JiraRelacion
                    {
                        SeguimientoId = seg.SeguimientoId,
                        ClaveJira = clave,
                        Titulo = req.Jira.Titulo
                    };
                    if (req.Jira.Estado.HasValue && Enum.IsDefined(typeof(EstadoJira), req.Jira.Estado.Value))
                        jira.Estado = (EstadoJira)req.Jira.Estado.Value;
                    _db.JiraRelaciones.Add(jira);
                    res.JiraRegistrado = true;
                }
                else
                {
                    res.Mensajes.Add($"El JIRA {clave} ya estaba vinculado a este caso.");
                    res.JiraYaExistia = true;
                }
            }

            // 6) Reporte VP
            if (req.ReporteVP is not null)
            {
                var vp = req.ReporteVP;
                if (vp.Incluir)
                {
                    // Normalizamos en vez de rechazar: el objetivo es no perder la gestión.
                    if (!EstadoReporteVPInfo.EsValido(vp.Estado)) vp.Estado = (int)EstadoReporteVP.Pendiente;
                    if (vp.CategoriaId.HasValue &&
                        !await _db.CategoriasReporteVP.AnyAsync(c => c.CategoriaId == vp.CategoriaId, ct))
                    {
                        vp.CategoriaId = null;
                        res.Mensajes.Add("La categoría del reporte VP no existe; el caso quedó sin categoría.");
                    }

                    seg.VaReporteVP = true;
                    seg.PeriodoReporteVP = ParsePeriodo(vp.Periodo);
                    seg.CategoriaReporteVPId = vp.CategoriaId;
                    seg.EstadoReporteVP = (EstadoReporteVP)vp.Estado;
                    seg.NotaReporteVP = vp.Nota?.Length > 2000 ? vp.Nota[..2000] : vp.Nota;
                }
                else
                {
                    seg.VaReporteVP = false;
                }
                res.ReporteVPActualizado = true;
            }

            seg.ActualizadoEn = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            res.SeguimientoId = seg.SeguimientoId;
            _logger.LogInformation(
                "Gestión registrada para {Ref} ({Tipo}): avance={Avance}, proveedor={Prov}, jira={Jira}, vp={Vp}",
                seg.RefNum, tipo, res.AvanceRegistrado, res.ProveedorRegistrado, res.JiraRegistrado, res.ReporteVPActualizado);

            return res;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Falló la gestión del caso {Ref}; se revirtió toda la operación.", req.RefNum);
            throw;
        }
    }

    /// <summary>
    /// Validación PERMISIVA: se puede registrar una gestión parcial (solo un avance,
    /// solo el proveedor, solo el JIRA...). Únicamente se exige lo imprescindible para
    /// identificar el caso; los valores fuera de rango se normalizan en vez de rechazarse,
    /// para no bloquear al usuario por un dato secundario.
    /// </summary>
    private static void Validar(GestionCasoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.RefNum))
            throw new ArgumentException("El número de caso es obligatorio.");
        if (req.RefNum.Trim().Length > 20)
            throw new ArgumentException($"El número de caso '{req.RefNum}' excede 20 caracteres.");

        // Tipo de origen: si viene algo raro, asumimos ticket en vez de fallar.
        if (req.TipoOrigen is not ("cr" or "chg")) req.TipoOrigen = "cr";

        // Estado de proveedor fuera de rango => Pendiente (valor neutro).
        if (!Enum.IsDefined(typeof(EstadoReporteProveedorTicket), req.ReportadoProveedor))
            req.ReportadoProveedor = (int)EstadoReporteProveedorTicket.Pendiente;

        // Etapa inválida => simplemente no se cambia la etapa actual.
        if (req.NuevaEtapa.HasValue && !Enum.IsDefined(typeof(EtapaSeguimiento), req.NuevaEtapa.Value))
            req.NuevaEtapa = null;

        // Horas fuera de rango => se ignoran, no bloquean el guardado.
        if (req.Avance is not null && req.Avance.Horas is < 0 or > 24)
            req.Avance.Horas = null;
    }

    private static string TipoAvanceTexto(int tipo) => tipo switch
    {
        1 => "Observacion",
        2 => "Decision",
        3 => "Bloqueo",
        4 => "Reunion",
        5 => "Validacion",
        6 => "Prueba",
        7 => "Resultado",
        _ => "Avance"
    };

    private static DateOnly ParsePeriodo(string? periodo)
    {
        if (!string.IsNullOrWhiteSpace(periodo) && DateOnly.TryParse($"{periodo}-01", out var d))
            return new DateOnly(d.Year, d.Month, 1);
        var hoy = DateTime.Today;
        return new DateOnly(hoy.Year, hoy.Month, 1);
    }

    private static string? Recortar(string? s, int max)
        => string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim()[..max] : s.Trim());
}
