using Microsoft.JSInterop;

namespace TicketAnalytics.Web.Services;

/// <summary>
/// Descarga de archivos sin depender de funciones globales de window.
/// Importa el módulo ES wwwroot/js/downloads.js bajo demanda, así que no
/// importa el orden de los &lt;script&gt; ni una caché vieja de app.js.
/// Para binarios usa DotNetStreamReference (sin pasar por base64).
/// </summary>
public sealed class FileDownloadService : IAsyncDisposable
{
    public const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IJSRuntime _js;
    private readonly ILogger<FileDownloadService> _logger;
    private IJSObjectReference? _modulo;

    public FileDownloadService(IJSRuntime js, ILogger<FileDownloadService> logger)
    {
        _js = js;
        _logger = logger;
    }

    private async Task<IJSObjectReference> ModuloAsync()
        => _modulo ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/downloads.js");

    /// <summary>Descarga bytes como archivo binario (Excel, PDF).</summary>
    public async Task DescargarBinarioAsync(string nombreArchivo, string contentType, byte[] contenido)
    {
        var modulo = await ModuloAsync();
        using var stream = new MemoryStream(contenido);
        using var streamRef = new DotNetStreamReference(stream, leaveOpen: false);
        await modulo.InvokeVoidAsync("downloadFromStream", nombreArchivo, contentType, streamRef);
        _logger.LogInformation("Descarga generada: {Archivo} ({Bytes} bytes)", nombreArchivo, contenido.Length);
    }

    /// <summary>Descarga un Excel (.xlsx).</summary>
    public Task DescargarExcelAsync(string nombreArchivo, byte[] contenido)
        => DescargarBinarioAsync(nombreArchivo, ExcelContentType, contenido);

    /// <summary>Descarga contenido de texto (CSV, TXT).</summary>
    public async Task DescargarTextoAsync(string nombreArchivo, string contentType, string contenido)
    {
        var modulo = await ModuloAsync();
        await modulo.InvokeVoidAsync("downloadText", nombreArchivo, contentType, contenido);
    }

    public async ValueTask DisposeAsync()
    {
        if (_modulo is not null)
        {
            try { await _modulo.DisposeAsync(); }
            catch (JSDisconnectedException) { /* el circuito ya se cerró: nada que liberar */ }
        }
    }
}
