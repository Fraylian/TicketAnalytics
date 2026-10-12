using Microsoft.JSInterop;

namespace TicketAnalytics.Web.Services;

/// <summary>
/// Puente con wwwroot/js/ui.js (módulo ES importado bajo demanda):
/// conteo animado de números, copiar al portapapeles y desplazamiento a secciones.
/// Ninguna de estas acciones es crítica: si el módulo falla, la UI sigue mostrando
/// el valor final y la acción simplemente no se anima.
/// </summary>
public sealed class UiService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private Task<IJSObjectReference>? _modulo;

    public UiService(IJSRuntime js) => _js = js;

    private Task<IJSObjectReference> Modulo()
        => _modulo ??= _js.InvokeAsync<IJSObjectReference>("import", "./js/ui.js").AsTask();

    public async Task AnimarNumeroAsync(Microsoft.AspNetCore.Components.ElementReference el,
                                        double desde, double hasta, int decimales, string textoFinal, int duracionMs = 900)
    {
        try
        {
            var m = await Modulo();
            await m.InvokeVoidAsync("animarNumero", el, desde, hasta, decimales, textoFinal, duracionMs);
        }
        catch (JSException) { /* sin animación: el texto final ya está renderizado */ }
        catch (JSDisconnectedException) { }
        catch (TaskCanceledException) { }
    }

    public async Task<bool> CopiarAsync(string texto)
    {
        try
        {
            var m = await Modulo();
            return await m.InvokeAsync<bool>("copiar", texto);
        }
        catch (JSException) { return false; }
    }

    public async Task IrAAsync(string id)
    {
        try
        {
            var m = await Modulo();
            await m.InvokeVoidAsync("irA", id);
        }
        catch (JSException) { }
    }

    /// <summary>granted | denied | default | no-soportado</summary>
    public async Task<string> PermisoNotificacionesAsync()
    {
        try { return await (await Modulo()).InvokeAsync<string>("permisoNotificaciones"); }
        catch (JSException) { return "no-soportado"; }
    }

    /// <summary>Pide permiso de notificaciones del escritorio. Llamar desde un clic del usuario.</summary>
    public async Task<string> PedirPermisoNotificacionesAsync()
    {
        try { return await (await Modulo()).InvokeAsync<string>("pedirPermisoNotificaciones"); }
        catch (JSException) { return "no-soportado"; }
    }

    /// <summary>Notificación del escritorio; devuelve false si no hay permiso (la alerta queda en la app).</summary>
    public async Task<bool> NotificarAsync(string titulo, string cuerpo, string etiqueta)
    {
        try { return await (await Modulo()).InvokeAsync<bool>("notificar", titulo, cuerpo, etiqueta); }
        catch (JSException) { return false; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_modulo is { IsCompletedSuccessfully: true })
        {
            try { await (await _modulo).DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
