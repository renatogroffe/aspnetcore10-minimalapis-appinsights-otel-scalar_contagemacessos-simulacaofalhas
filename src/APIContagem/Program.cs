using APIContagem;
using APIContagem.Models;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

/*var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(serviceName: OpenTelemetryExtensions.ServiceName,
        serviceVersion: OpenTelemetryExtensions.ServiceVersion);

builder.Services.AddOpenTelemetry()
    .WithTracing((traceBuilder) =>
    {
        traceBuilder
            .AddSource(OpenTelemetryExtensions.ServiceName)
            .SetResourceBuilder(resourceBuilder)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = builder.Configuration.GetConnectionString("AppInsights");
            });
    });
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(resourceBuilder);
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.ParseStateValues = true;
    options.AttachLogsToActivityEvent();
    options.AddAzureMonitorLogExporter(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("AppInsights");
    });
});*/

builder.Services.AddSingleton<Contador>();

builder.Services.AddOpenApi();
builder.Services.AddCors();

builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
});

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "API de Contagem de Acessos";
    options.Theme = ScalarTheme.BluePlanet;
    options.DarkMode = true;
});

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

Lock ContagemLock = new();
var simularFalhas = Convert.ToBoolean(app.Configuration["SimularFalhas"]);

app.MapGet("/contador", (Contador contador) =>
{
    int valorAtualContador;
    using (ContagemLock.EnterScope())
    {
        contador.Incrementar();
        valorAtualContador = contador.ValorAtual;
    }
    app.Logger.LogInformation($"Contador - Valor atual: {valorAtualContador}");

    if (valorAtualContador % 4 == 0 && simularFalhas)
    {
        app.Logger.LogWarning("Simulacao de falha: contador atingiu um multiplo de 4!");
        throw new Exception("Contador atingiu um valor invalido!");
    }

    var resultadoContador = new ResultadoContador()
    {
        ValorAtual = contador.ValorAtual,
        Local = contador.Local,
        Kernel = contador.Kernel,
        Framework = contador.Framework,
        Mensagem = app.Configuration["Saudacao"]
    };

    return Results.Ok(resultadoContador);
})
.Produces<ResultadoContador>();

app.Run();