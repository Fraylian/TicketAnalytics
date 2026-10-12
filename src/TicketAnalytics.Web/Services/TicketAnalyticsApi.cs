using System.Net.Http.Json;
using TicketAnalytics.Web.Models;

namespace TicketAnalytics.Web.Services;

/// <summary>Cliente HTTP tipado que consume la API de TicketAnalytics.</summary>
public class TicketAnalyticsApi
{
    private readonly HttpClient _http;
    public TicketAnalyticsApi(HttpClient http) => _http = http;

    // ===== Tickets =====
    public async Task<PagedResult<TicketEnriquecidoDto>> BuscarTicketsAsync(TicketFiltro f)
        => await Post<PagedResult<TicketEnriquecidoDto>>("api/tickets/buscar", f) ?? new();
    public async Task<BandejaAnalistasDto> BandejaAnalistasAsync(TicketFiltro f)
        => await Post<BandejaAnalistasDto>("api/tickets/bandeja-analistas", f) ?? new();
    public async Task<TicketEnriquecidoDto?> ObtenerTicketAsync(string refNum)
        => await _http.GetFromJsonAsync<TicketEnriquecidoDto>($"api/tickets/{refNum}");

    // ===== Ordenes de cambio =====
    public async Task<PagedResult<OrdenCambioConTicketsDto>> BuscarOrdenesCambioAsync(TicketFiltro f)
        => await Post<PagedResult<OrdenCambioConTicketsDto>>("api/ordenes-cambio/buscar", f) ?? new();
    public async Task<TableroOcDto> TableroOrdenesCambioAsync(TicketFiltro f)
        => await Post<TableroOcDto>("api/ordenes-cambio/tablero", f) ?? new();
    public async Task<OrdenCambioConTicketsDto?> ObtenerOrdenCambioAsync(string chgRefNum)
        => await _http.GetFromJsonAsync<OrdenCambioConTicketsDto>($"api/ordenes-cambio/{chgRefNum}");

    // ===== Dashboard =====
    public async Task<List<VolumenPorEstadoDto>> VolumenAsync(TicketFiltro f)
        => await Post<List<VolumenPorEstadoDto>>("api/dashboard/volumen", f) ?? new();
    public async Task<List<CumplimientoSlaDto>> SlaCategoriaAsync(TicketFiltro f)
        => await Post<List<CumplimientoSlaDto>>("api/dashboard/sla", f) ?? new();
    public async Task<List<ProductividadDto>> ProductividadAsync(TicketFiltro f)
        => await Post<List<ProductividadDto>>("api/dashboard/productividad", f) ?? new();
    public async Task<List<TendenciaDto>> TendenciasAsync(TicketFiltro f, string granularidad)
        => await Post<List<TendenciaDto>>($"api/dashboard/tendencias/{granularidad}", f) ?? new();
    public async Task<List<CruceOrdenCambioDto>> CruceOcAsync(TicketFiltro f)
        => await Post<List<CruceOrdenCambioDto>>("api/dashboard/cruce-oc", f) ?? new();

    // ===== SLA con tiempos =====
    public async Task<ResumenSlaDto> SlaResumenAsync(TicketFiltro f)
        => await Post<ResumenSlaDto>("api/sla/resumen", f) ?? new();
    public async Task<List<SlaTicketDto>> SlaProximosAVencerAsync(TicketFiltro f)
        => await Post<List<SlaTicketDto>>("api/sla/proximos-vencer", f) ?? new();
    public async Task<List<SlaTicketDto>> SlaVencidosAsync(TicketFiltro f)
        => await Post<List<SlaTicketDto>>("api/sla/vencidos", f) ?? new();
    public async Task<SlaTicketDto?> SlaDeTicketAsync(string refNum)
        => await _http.GetFromJsonAsync<SlaTicketDto>($"api/sla/{refNum}");

    // ===== Metricas por analista =====
    public async Task<List<MetricaAnalistaDto>> MetricasAnalistaAsync(TicketFiltro f)
        => await Post<List<MetricaAnalistaDto>>("api/analistas/metricas", f) ?? new();

    // ===== Seguimiento =====
    public async Task<SeguimientoDto?> ObtenerSeguimientoAsync(string refNum)
    {
        var resp = await _http.GetAsync($"api/seguimiento/{refNum}");
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<SeguimientoDto>() : null;
    }
    public async Task<SeguimientoDto?> CrearSeguimientoAsync(CrearSeguimientoRequest req)
        => await Post<SeguimientoDto>("api/seguimiento", req);
    public async Task<ActualizacionDto?> AgregarActualizacionAsync(string refNum, AgregarActualizacionRequest req)
        => await Post<ActualizacionDto>($"api/seguimiento/{refNum}/actualizaciones", req);
    public async Task<List<ActualizacionDto>> HistorialAsync(string refNum)
        => await _http.GetFromJsonAsync<List<ActualizacionDto>>($"api/seguimiento/{refNum}/historial") ?? new();
    public async Task<PagedResult<SeguimientoDto>> BuscarSeguimientosAsync(TicketFiltro f)
        => await Post<PagedResult<SeguimientoDto>>("api/seguimiento/buscar", f) ?? new();

    // ===== Proveedores / JIRA =====
    public async Task<List<ProveedorDto>> ProveedoresAsync()
        => await _http.GetFromJsonAsync<List<ProveedorDto>>("api/proveedores") ?? new();
    public async Task<ProveedorDto?> CrearProveedorAsync(string nombre, string? contacto, string? email)
        => await Post<ProveedorDto>("api/proveedores", new { Nombre = nombre, Contacto = contacto, Email = email });
    public async Task<ReporteProveedorDto?> ReportarProveedorAsync(string refNum, ReporteProveedorDto req)
        => await Post<ReporteProveedorDto>($"api/proveedores/reportar/{refNum}", req);
    public async Task<List<ReporteProveedorDto>> ReportesDeSeguimientoAsync(string refNum)
        => await _http.GetFromJsonAsync<List<ReporteProveedorDto>>($"api/proveedores/reportes/{refNum}") ?? new();
    public async Task<List<ReporteProveedorDto>> ReportesNecesitanSeguimientoAsync()
        => await _http.GetFromJsonAsync<List<ReporteProveedorDto>>("api/proveedores/necesitan-seguimiento") ?? new();
    public async Task<JiraDto?> VincularJiraAsync(string refNum, string clave, string? titulo, string? url, string? autor)
        => await Post<JiraDto>($"api/jira/vincular/{refNum}", new { ClaveJira = clave, Titulo = titulo, Url = url, Autor = autor });
    public async Task<List<JiraDto>> JirasDeSeguimientoAsync(string refNum)
        => await _http.GetFromJsonAsync<List<JiraDto>>($"api/jira/{refNum}") ?? new();

    // ===== Trabajo diario =====
    public async Task<List<TecnicoDto>> TecnicosAsync()
        => await _http.GetFromJsonAsync<List<TecnicoDto>>("api/trabajo-diario/tecnicos") ?? new();
    public async Task<int> SincronizarTecnicosAsync()
    {
        var r = await _http.PostAsync("api/trabajo-diario/tecnicos/sincronizar", null);
        r.EnsureSuccessStatusCode();
        var res = await r.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        return res != null && res.TryGetValue("agregados", out var n) ? n : 0;
    }
    public async Task<TecnicoDto?> CrearTecnicoTdAsync(string nombre)
        => await Post<TecnicoDto>("api/trabajo-diario/tecnicos", new { Nombre = nombre });
    public async Task<PagedResult<ActividadDto>> BuscarActividadesAsync(ActividadFiltro f)
        => await Post<PagedResult<ActividadDto>>("api/trabajo-diario/buscar", f) ?? new();
    public async Task<ActividadDto?> GuardarActividadAsync(GuardarActividadRequest req)
        => await Post<ActividadDto>("api/trabajo-diario", req);
    public async Task EliminarActividadAsync(long id)
    {
        var r = await _http.DeleteAsync($"api/trabajo-diario/{id}");
        r.EnsureSuccessStatusCode();
    }
    public async Task<ResumenJornadaDto> ResumenJornadaAsync(ActividadFiltro f)
        => await Post<ResumenJornadaDto>("api/trabajo-diario/resumen", f) ?? new();
    public async Task<List<OpcionTicketDto>> BuscarTicketsMdbAsync(string termino)
        => await _http.GetFromJsonAsync<List<OpcionTicketDto>>($"api/trabajo-diario/buscar-tickets?termino={Uri.EscapeDataString(termino)}") ?? new();
    public async Task<List<ActividadDto>> ActividadesDeTicketAsync(string refNum)
        => await _http.GetFromJsonAsync<List<ActividadDto>>($"api/trabajo-diario/ticket/{refNum}/actividades") ?? new();

    private async Task<T?> Post<T>(string url, object body, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(url, body, ct);
        await GarantizarExitoAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    private async Task<T?> Get<T>(string url, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(url, ct);
        await GarantizarExitoAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    /// <summary>Convierte una respuesta de error en ApiException con mensaje legible,
    /// leyendo ProblemDetails del body cuando la API lo envía.</summary>
    private static async Task GarantizarExitoAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;

        string? detalle = null;
        try
        {
            var problema = await resp.Content.ReadFromJsonAsync<ProblemDetailsDto>(cancellationToken: ct);
            detalle = problema?.Detail ?? problema?.Title;
        }
        catch
        {
            // El body no era ProblemDetails: usamos el texto plano si lo hay.
            try { detalle = await resp.Content.ReadAsStringAsync(ct); } catch { /* sin body legible */ }
        }

        // Si el servidor explicó QUÉ está mal (400/404 con detalle), mostramos ese texto:
        // es mucho más útil que un mensaje genérico. El genérico queda como respaldo.
        var esExplicable = resp.StatusCode is System.Net.HttpStatusCode.BadRequest
                                            or System.Net.HttpStatusCode.NotFound
                                            or System.Net.HttpStatusCode.Conflict;

        var amigable = (esExplicable && !string.IsNullOrWhiteSpace(detalle))
            ? detalle!
            : resp.StatusCode switch
            {
                System.Net.HttpStatusCode.NotFound => "No encontramos la información solicitada.",
                System.Net.HttpStatusCode.BadRequest => "Los datos enviados no son válidos.",
                System.Net.HttpStatusCode.Unauthorized => "No tienes permiso para esta operación.",
                System.Net.HttpStatusCode.RequestTimeout => "La consulta tardó demasiado. Intenta de nuevo.",
                _ when (int)resp.StatusCode >= 500 => "El servidor no pudo completar la operación.",
                _ => "No pudimos completar la operación."
            };

        throw new ApiException(amigable, detalle, resp.StatusCode);
    }

    // ===== Órdenes de cambio: módulo avanzado =====
    public async Task<PagedResult<OrdenCambioConTicketsDto>> BuscarOcAvanzadoAsync(OrdenCambioFiltro f)
        => await Post<PagedResult<OrdenCambioConTicketsDto>>("api/ordenes-cambio/buscar-avanzado", f) ?? new();
    public async Task<OrdenCambioDashboardDto> OcDashboardAsync(OrdenCambioFiltro f)
        => await Post<OrdenCambioDashboardDto>("api/ordenes-cambio/dashboard", f) ?? new();
    public async Task<ControlVencimientosDto> OcControlVencimientosAsync(OrdenCambioFiltro f)
        => await Post<ControlVencimientosDto>("api/ordenes-cambio/control-vencimientos", f) ?? new();
    public async Task<List<string>> OcAnalistasAsync(OrdenCambioFiltro f)
        => await Post<List<string>>("api/ordenes-cambio/analistas", f) ?? new();

    // ===== Finalizar / reabrir gestión =====
    public async Task FinalizarGestionAsync(long seguimientoId, string motivo, string? usuario, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/seguimiento/{seguimientoId}/finalizar",
                                               new { Motivo = motivo, Usuario = usuario }, ct);
        await GarantizarExitoAsync(resp, ct);
    }

    public async Task ReabrirGestionAsync(long seguimientoId, string? usuario, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/seguimiento/{seguimientoId}/reabrir",
                                               new { Motivo = "", Usuario = usuario }, ct);
        await GarantizarExitoAsync(resp, ct);
    }

    // ===== Catálogos de gestión (Sistema, Tipo Solución, Tipo Actualización) =====
    public async Task<CatalogosGestionDto> CatalogosGestionAsync(CancellationToken ct = default)
        => await Get<CatalogosGestionDto>("api/catalogos-gestion/todos", ct) ?? new();

    // ===== Indicadores de clasificación =====
    public async Task<IndicadoresGestionDto> IndicadoresGestionAsync(DateTime? desde, DateTime? hasta, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (desde.HasValue) q.Add($"desde={desde.Value:yyyy-MM-dd}");
        if (hasta.HasValue) q.Add($"hasta={hasta.Value:yyyy-MM-dd}");
        var url = "api/dashboard/indicadores-gestion" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        return await Get<IndicadoresGestionDto>(url, ct) ?? new();
    }

    // ===== Casos pendientes de escalar a proveedor =====
    public async Task<List<PendienteEscalarDto>> PendientesEscalarAsync(CancellationToken ct = default)
        => await Get<List<PendienteEscalarDto>>("api/proveedores/pendientes-escalar", ct) ?? new();

    // ===== Reporte mensual VP =====
    public async Task<ReporteVPOverviewDto> ReporteVPOverviewAsync(ReporteVPFiltro f, CancellationToken ct = default)
        => await Post<ReporteVPOverviewDto>("api/reporte-vp/overview", f, ct) ?? new();
    public async Task<List<CasoReporteVPDto>> ReporteVPCasosAsync(ReporteVPFiltro f, CancellationToken ct = default)
        => await Post<List<CasoReporteVPDto>>("api/reporte-vp/casos", f, ct) ?? new();
    public async Task<List<CategoriaVPDto>> ReporteVPCategoriasAsync(CancellationToken ct = default)
        => await Get<List<CategoriaVPDto>>("api/reporte-vp/categorias", ct) ?? new();
    public async Task<List<string>> ReporteVPPeriodosAsync(CancellationToken ct = default)
        => await Get<List<string>>("api/reporte-vp/periodos", ct) ?? new();
    public async Task<CasoReporteVPDto?> ReporteVPGuardarCasoAsync(GuardarCasoVPRequest req, CancellationToken ct = default)
        => await Post<CasoReporteVPDto>("api/reporte-vp/casos/guardar", req, ct);
    public async Task ReporteVPQuitarCasoAsync(long seguimientoId, CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync($"api/reporte-vp/casos/{seguimientoId}", ct);
        await GarantizarExitoAsync(resp, ct);
    }
    public async Task ReporteVPMoverPeriodoAsync(long seguimientoId, string periodo, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"api/reporte-vp/casos/{seguimientoId}/mover", new { Periodo = periodo }, ct);
        await GarantizarExitoAsync(resp, ct);
    }
    public async Task<byte[]> ReporteVPExcelBytesAsync(ReporteVPFiltro f, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/reporte-vp/excel", f, ct);
        await GarantizarExitoAsync(resp, ct);
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    // ===== Gestión atómica del caso =====
    public async Task<GestionCasoResultado?> RegistrarGestionAsync(string refNum, GestionCasoRequest req, CancellationToken ct = default)
        => await Post<GestionCasoResultado>($"api/casos/{refNum}/gestion", req, ct);

    // ===== Matriz de proveedores =====
    public async Task<List<ReporteProveedorMatrizDto>> MatrizBuscarAsync(ReporteProveedorFiltro f)
        => await Post<List<ReporteProveedorMatrizDto>>("api/proveedores/matriz/buscar", f) ?? new();

    /// <summary>Pide el Excel real de la matriz al backend y devuelve los bytes.
    /// La descarga la hace FileDownloadService (módulo JS, sin base64).</summary>
    public async Task<byte[]> MatrizExcelBytesAsync(ReporteProveedorFiltro f, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("api/proveedores/matriz/excel", f, ct);
        await GarantizarExitoAsync(resp, ct);
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }
    public async Task<MatrizProveedorResumenDto> MatrizResumenAsync(ReporteProveedorFiltro f)
        => await Post<MatrizProveedorResumenDto>("api/proveedores/matriz/resumen", f) ?? new();
    public async Task<ReporteProveedorMatrizDto?> MatrizGuardarAsync(GuardarReporteProveedorRequest req)
        => await Post<ReporteProveedorMatrizDto>("api/proveedores/matriz/guardar", req);
    public async Task MatrizEliminarAsync(long reporteId)
        => await _http.DeleteAsync($"api/proveedores/matriz/{reporteId}");
    public async Task<List<string>> MatrizResponsablesAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/proveedores/matriz/responsables") ?? new();

    // ===== Catálogo de proveedores (CRUD) + grupo =====
    public async Task<List<ProveedorCatalogoDto>> CatalogoProveedoresAsync()
        => await _http.GetFromJsonAsync<List<ProveedorCatalogoDto>>("api/proveedores/catalogo") ?? new();
    public async Task<ProveedorCatalogoDto?> CatalogoGuardarAsync(GuardarProveedorRequest req)
        => await Post<ProveedorCatalogoDto>("api/proveedores/catalogo/guardar", req);
    public async Task CatalogoEliminarAsync(int proveedorId)
        => await _http.DeleteAsync($"api/proveedores/catalogo/{proveedorId}");
    public async Task<List<string>> GrupoAnalistasAsync()
        => await _http.GetFromJsonAsync<List<string>>("api/proveedores/grupo-analistas") ?? new();
    public async Task<List<ReporteProveedorMatrizDto>> MatrizPorTicketAsync(string refNum)
        => await _http.GetFromJsonAsync<List<ReporteProveedorMatrizDto>>($"api/proveedores/matriz/por-ticket/{refNum}") ?? new();
}
