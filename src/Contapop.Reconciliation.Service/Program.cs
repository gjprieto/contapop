using Contapop.Reconciliation.Service;
using Contapop.Reconciliation.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<ReconciliationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("reconciliation")));
builder.Services.AddHostedService<ReconciliationRecoveryWorker>();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>().Database.MigrateAsync();
app.UseExceptionHandler();
app.MapGet("/health", () => Results.Ok());
app.Run();

public partial class Program;
