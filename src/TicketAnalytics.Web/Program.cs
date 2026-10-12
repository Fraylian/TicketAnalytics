using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using MudBlazor.Services;
using ApexCharts;
using TicketAnalytics.Web;
using TicketAnalytics.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddApexCharts();

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7100/";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<TicketAnalyticsApi>();
builder.Services.AddScoped<FileDownloadService>();
builder.Services.AddScoped<CatalogoCache>();
builder.Services.AddScoped<UiService>();

var host = builder.Build();

await host.RunAsync();
