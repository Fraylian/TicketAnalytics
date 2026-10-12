using Microsoft.EntityFrameworkCore;
using TicketAnalytics.API.Endpoints;
using TicketAnalytics.Infrastructure;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddCors(o => o.AddPolicy("Blazor", p =>
    p.WithOrigins(builder.Configuration.GetSection("CorsOrigins").Get<string[]>() ?? Array.Empty<string>())
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Errores: mensaje controlado al cliente, excepcion completa al log
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);

        context.Response.StatusCode = ex is InvalidOperationException
            ? StatusCodes.Status400BadRequest : StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            error = ex?.Message ?? "Error inesperado",
            tipo = ex?.GetType().Name,
            ruta = context.Request.Path.Value,
            status = context.Response.StatusCode
        });
    });
});

// ====== INICIALIZACION DE LA BASE LOCAL (SQL idempotente, sin migraciones EF) ======
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var ctx = scope.ServiceProvider.GetRequiredService<AnalyticsContext>();
        await AnalyticsDbInitializer.InicializarAsync(ctx, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "FALLO LA INICIALIZACION DE LA BASE LOCAL. " +
            "Los endpoints de consulta fallaran porque CategoriaAlcance no existira. " +
            "Revisa AnalyticsConnection y que tengas permiso CREATE DATABASE en el servidor local. " +
            "Diagnostica en /health.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Blazor");

app.MapTicketEndpoints();
app.MapOrdenCambioEndpoints();
app.MapDashboardEndpoints();
app.MapSlaEndpoints();
app.MapAnalistaEndpoints();
app.MapSeguimientoEndpoints();
app.MapProveedorEndpoints();
app.MapCasoEndpoints();
app.MapTrabajoDiarioEndpoints();
app.MapReporteVPEndpoints();
app.MapCatalogoGestionEndpoints();
app.MapAdminEndpoints();

// ====== DIAGNOSTICO COMPLETO ======
app.MapGet("/health", async (AnalyticsContext analytics, SdmReadContext sdm, ILogger<Program> logger) =>
{
    var r = new Dictionary<string, object>();

    // --- Base local ---
    try
    {
        var conecta = await analytics.Database.CanConnectAsync();
        var info = new Dictionary<string, object> { ["conecta"] = conecta };
        if (conecta)
        {
            var conn = analytics.Database.GetDbConnection();
            info["servidor"] = await Escalar(analytics, "SELECT @@SERVERNAME");
            info["baseDatos"] = await Escalar(analytics, "SELECT DB_NAME()");
            info["login"] = await Escalar(analytics, "SELECT ORIGINAL_LOGIN()");

            var tablas = new[] { "CategoriaAlcance", "TicketSeguimiento", "SeguimientoActualizacion",
                                 "JiraRelacion", "Proveedor", "ReporteProveedor", "Evidencia" };
            var estado = new Dictionary<string, object>();
            foreach (var t in tablas)
            {
                var existe = await Escalar(analytics, $"SELECT CASE WHEN OBJECT_ID('dbo.{t}','U') IS NULL THEN 0 ELSE 1 END");
                estado[t] = existe?.ToString() == "1"
                    ? (object)new { existe = true, registros = await Escalar(analytics, $"SELECT COUNT(*) FROM dbo.{t}") }
                    : new { existe = false };
            }
            info["tablas"] = estado;
        }
        r["baseLocal"] = info;
    }
    catch (Exception ex) { r["baseLocal"] = new { error = ex.Message, tipo = ex.GetType().Name }; }

    // --- MDB (solo lectura) ---
    try
    {
        var conecta = await sdm.Database.CanConnectAsync();
        var info = new Dictionary<string, object> { ["conecta"] = conecta, ["modo"] = "solo lectura" };
        if (conecta)
        {
            info["servidor"] = await Escalar(sdm, "SELECT @@SERVERNAME");
            info["baseDatos"] = await Escalar(sdm, "SELECT DB_NAME()");
            info["login"] = await Escalar(sdm, "SELECT ORIGINAL_LOGIN()");
            info["call_req_total"] = await Escalar(sdm, "SELECT COUNT(*) FROM dbo.call_req");
            info["chg_total"] = await Escalar(sdm, "SELECT COUNT(*) FROM dbo.chg");
            info["prob_ctg_total"] = await Escalar(sdm, "SELECT COUNT(*) FROM dbo.prob_ctg");
        }
        r["mdb"] = info;
    }
    catch (Exception ex) { r["mdb"] = new { error = ex.Message, tipo = ex.GetType().Name }; }

    return Results.Ok(r);
}).WithTags("Diagnóstico").WithSummary("Valida conexiones, tablas y conteos reales de ambas bases.");

app.MapGet("/", () => Results.Ok(new { servicio = "TicketAnalytics API", version = "2.1", diagnostico = "/health", swagger = "/swagger" }));

app.Run();

// Helper para ejecutar escalares de diagnostico
static async Task<object?> Escalar(DbContext ctx, string sql)
{
    await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
    cmd.CommandText = sql;
    if (cmd.Connection!.State != System.Data.ConnectionState.Open) await cmd.Connection.OpenAsync();
    return await cmd.ExecuteScalarAsync();
}
