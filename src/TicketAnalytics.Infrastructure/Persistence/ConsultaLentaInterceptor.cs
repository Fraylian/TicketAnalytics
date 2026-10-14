using System.Data.Common;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using TicketAnalytics.Application.Common.Interfaces;

namespace TicketAnalytics.Infrastructure.Persistence;

/// <summary>
/// Vigila TODAS las consultas de EF (MDB y base local):
///  - Si una tarda más que "Consultas.UmbralLentaMs" (parámetro), la registra como Warning con
///    su SQL y sus parámetros, para poder reproducirla y medirla en el servidor.
///  - Si una falla (timeout, bloqueo, conexión), la registra como Error con el mismo detalle.
///  - Si se cancela (el usuario cambió el filtro), lo deja en Debug: es el comportamiento esperado.
/// Es singleton: lee el umbral del último valor de parámetros cargado (sin consultar la base).
/// </summary>
public sealed class ConsultaLentaInterceptor : DbCommandInterceptor
{
    private const int MaxSql = 6000;
    private readonly ILogger<ConsultaLentaInterceptor> _logger;
    private readonly IParametrosProvider _parametros;

    public ConsultaLentaInterceptor(ILogger<ConsultaLentaInterceptor> logger, IParametrosProvider parametros)
    {
        _logger = logger;
        _parametros = parametros;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData e, DbDataReader result)
    { Revisar(command, e); return result; }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData e, DbDataReader result, CancellationToken ct = default)
    { Revisar(command, e); return ValueTask.FromResult(result); }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData e, object? result)
    { Revisar(command, e); return result; }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData e, object? result, CancellationToken ct = default)
    { Revisar(command, e); return ValueTask.FromResult(result); }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData e, int result)
    { Revisar(command, e); return result; }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData e, int result, CancellationToken ct = default)
    { Revisar(command, e); return ValueTask.FromResult(result); }

    public override void CommandFailed(DbCommand command, CommandErrorEventData e) => Fallo(command, e);

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData e, CancellationToken ct = default)
    { Fallo(command, e); return Task.CompletedTask; }

    public override void CommandCanceled(DbCommand command, CommandEndEventData e)
        => _logger.LogDebug("Consulta cancelada por el cliente tras {Ms} ms ({Base}).", (long)e.Duration.TotalMilliseconds, Base(e));

    public override Task CommandCanceledAsync(DbCommand command, CommandEndEventData e, CancellationToken ct = default)
    { CommandCanceled(command, e); return Task.CompletedTask; }

    private void Revisar(DbCommand command, CommandExecutedEventData e)
    {
        var ms = (long)e.Duration.TotalMilliseconds;
        if (ms < _parametros.Ultimos.ConsultasUmbralLentaMs) return;
        _logger.LogWarning("CONSULTA LENTA ({Ms} ms, umbral {Umbral} ms) en {Base}.\nParámetros: {Parametros}\nSQL:\n{Sql}",
            ms, _parametros.Ultimos.ConsultasUmbralLentaMs, Base(e), Parametros(command), Recortar(command.CommandText));
    }

    private void Fallo(DbCommand command, CommandErrorEventData e)
    {
        // La cancelación por el usuario también llega como error en algunos proveedores: no es un fallo.
        if (e.Exception is OperationCanceledException) return;
        _logger.LogError(e.Exception, "CONSULTA FALLIDA tras {Ms} ms en {Base}: {Mensaje}\nParámetros: {Parametros}\nSQL:\n{Sql}",
            (long)e.Duration.TotalMilliseconds, Base(e), e.Exception.Message, Parametros(command), Recortar(command.CommandText));
    }

    private static string Base(CommandEventData e) => e.Context?.GetType().Name switch
    {
        "SdmReadContext" => "MDB (solo lectura)",
        "AnalyticsContext" => "base local",
        var n => n ?? "desconocida"
    };

    private static string Parametros(DbCommand c)
    {
        if (c.Parameters.Count == 0) return "(ninguno)";
        var sb = new StringBuilder();
        foreach (DbParameter p in c.Parameters)
        {
            var valor = p.Value switch
            {
                null or DBNull => "NULL",
                byte[] b => "0x" + Convert.ToHexString(b),
                string s when s.Length > 300 => s[..300] + "…",
                var v => v.ToString()
            };
            sb.Append(p.ParameterName).Append('=').Append(valor).Append("; ");
        }
        return sb.ToString();
    }

    private static string Recortar(string sql) => sql.Length > MaxSql ? sql[..MaxSql] + "\n…(recortado)" : sql;
}
