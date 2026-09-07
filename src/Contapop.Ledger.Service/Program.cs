using Contapop.Ledger.Service.Api;
using Contapop.Ledger.Service.Application.ProjectReplication;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<LedgerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ledger")));
builder.Services.AddScoped<ProjectReplicationConsumer>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    await database.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseCloudEvents();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok());
app.MapSubscribeHandler();
app.MapProjectReplicationEndpoints();

app.Run();

public partial class Program;
