using Haondt.Identity.StorageKey;
using Serilog;
using Serilog.Formatting.Compact;
using Talos.Api.Extensions;
using Talos.Discord.Extensions;
using Talos.Docker.Extensions;
using Talos.Domain.Extensions;
using Talos.ImageUpdate.Shared.Extensions;
using Talos.Integration.Command.Extensions;

StorageKeyConvert.DefaultSerializerSettings.TypeNameStrategy = TypeNameStrategy.SimpleTypeConverter;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.Conditional(
        evt => builder.Environment.IsDevelopment(),
        wt => wt.Seq("http://localhost:5341/"))
    .CreateLogger();

if (builder.Environment.IsDevelopment())
    builder.Configuration.SetBasePath(AppContext.BaseDirectory);

builder.Configuration.AddEnvironmentVariables();
builder.Services.AddTalosDiscordServices(builder.Configuration)
    .AddTalosDockerServices(builder.Configuration)
    .AddTalosImageUpdateServices(builder.Configuration)
    .AddTalosIntegrationServices(builder.Configuration)
    .AddTalosServices(builder.Configuration)
    .AddTalosApiServices(builder.Configuration)
    .AddSerilog();
builder.Services.AddHealthChecks();

builder.Logging.ClearProviders()
    .AddSerilog();

var app = builder.Build();

app.AddTalosApiEndpoints();
app.MapHealthChecks("/hc");


app.Run();
