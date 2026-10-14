using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketAnalytics.Application.Common.Interfaces;
using TicketAnalytics.Infrastructure.Persistence;
using TicketAnalytics.Infrastructure.Persistence.Analytics;
using TicketAnalytics.Infrastructure.Persistence.Sdm;
using TicketAnalytics.Infrastructure.Repositories;
using TicketAnalytics.Infrastructure.Services;
using TicketAnalytics.Infrastructure.Services.Parametros;

namespace TicketAnalytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Parámetros de negocio, catálogos de MDB y grupo de analistas: singletons con caché.
        services.AddSingleton<IParametrosProvider, ParametrosProvider>();
        services.AddSingleton<CatalogoSdm>();
        services.AddSingleton<IGrupoAnalistasProvider, GrupoAnalistasProvider>();
        services.AddSingleton<ConsultaLentaInterceptor>();

        services.AddDbContext<SdmReadContext>((sp, opt) =>
            opt.UseSqlServer(config.GetConnectionString("SdmConnection"), sql =>
                {
                    sql.EnableRetryOnFailure(3);
                    sql.CommandTimeout(120);
                })
               .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
               .AddInterceptors(sp.GetRequiredService<ConsultaLentaInterceptor>()));

        services.AddDbContext<AnalyticsContext>((sp, opt) =>
            opt.UseSqlServer(config.GetConnectionString("AnalyticsConnection"), sql =>
            {
                sql.EnableRetryOnFailure(3);
                sql.CommandTimeout(60);
            })
            .AddInterceptors(sp.GetRequiredService<ConsultaLentaInterceptor>()));

        services.AddScoped<IAnalyticsDbContext>(sp => sp.GetRequiredService<AnalyticsContext>());

        // Consulta (lectura mdb)
        services.AddScoped<ConsultaOrdenesCambio>();
        services.AddScoped<TicketQueryService>();
        services.AddScoped<ITicketQueryService>(sp => sp.GetRequiredService<TicketQueryService>());
        services.AddScoped<IOrdenCambioDashboardService, OrdenCambioDashboardService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<ISlaService, SlaService>();
        services.AddScoped<IAnalistaService, AnalistaService>();

        // Seguimiento operativo (base local)
        services.AddScoped<ISeguimientoService, SeguimientoService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<IJiraService, JiraService>();
        services.AddScoped<IComplejidadService, ComplejidadService>();
        services.AddScoped<IReporteVPService, ReporteVPService>();
        services.AddScoped<IGestionCasoService, GestionCasoService>();
        services.AddScoped<IIndicadoresGestionService, IndicadoresGestionService>();
        services.AddScoped<ISistemaCatalogoService, SistemaCatalogoService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        // Diario TI desactivado (2026-10-12): ver Program.cs (MapTrabajoDiarioEndpoints).
        // services.AddScoped<ITrabajoDiarioService, TrabajoDiarioService>();

        return services;
    }
}
