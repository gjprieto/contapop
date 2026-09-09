using Contapop.Reconciliation.Service;
using Contapop.Reconciliation.Service.Infrastructure.Persistence;
using Contapop.Reconciliation.Service.Reconciliation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<ReconciliationDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("reconciliation")));
builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery().AddStandardResilienceHandler());
builder.Services.AddHttpClient("ledger-service", client => client.BaseAddress = new Uri(builder.Configuration["LedgerService:BaseUrl"] ?? "https+http://ledger-service"));
builder.Services.AddHttpClient("billing-service", client => client.BaseAddress = new Uri(builder.Configuration["BillingService:BaseUrl"] ?? "https+http://billing-service"));
builder.Services.AddScoped<PaymentReconciliationCoordinator>();
builder.Services.AddSingleton<InternalJwtIssuer>();
builder.Services.AddAuthentication().AddJwtBearer("InternalJwt", options => options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["InternalJwt:SigningKey"] ?? throw new InvalidOperationException("Internal JWT signing key is not configured."))),
});
builder.Services.AddAuthorization(options => options.AddPolicy("account-owner", policy => { policy.AddAuthenticationSchemes("InternalJwt"); policy.RequireRole("account-owner"); }));
builder.Services.AddHostedService<ReconciliationRecoveryWorker>();
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>().Database.MigrateAsync();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok());
app.MapPaymentReconciliationEndpoints();
app.Run();

public partial class Program;
