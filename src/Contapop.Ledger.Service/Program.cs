using Contapop.Ledger.Service.Api;
using Contapop.Ledger.Service.Application.ProjectReplication;
using Contapop.Ledger.Service.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Contapop.Ledger.Service.Application.Commands;
using Contapop.Ledger.Service.Infrastructure.Persistence.Interceptors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<DomainEventOutboxInterceptor>();
builder.Services.AddDbContext<LedgerDbContext>((serviceProvider, options) =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ledger"))
        .AddInterceptors(serviceProvider.GetRequiredService<DomainEventOutboxInterceptor>()));
builder.Services.AddScoped<ProjectReplicationConsumer>();
builder.Services.AddScoped<AccountCommandHandler>();
builder.Services.AddScoped<TransactionCommandHandler>();
builder.Services.AddScoped<TransactionFileImporter>();
builder.Services.AddScoped<ReconciliationClaimCommandHandler>();
builder.Services.AddAuthentication().AddJwtBearer("InternalJwt", options =>
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["InternalJwt:SigningKey"] ?? throw new InvalidOperationException("Internal JWT signing key is not configured."))),
    });
builder.Services.AddAuthorization(options => options.AddPolicy("account-owner", policy =>
{
    policy.AddAuthenticationSchemes("InternalJwt");
    policy.RequireRole("account-owner");
}));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
    await database.Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseCloudEvents();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok());
app.MapSubscribeHandler();
app.MapProjectReplicationEndpoints();
app.MapAccountEndpoints();
app.MapTransactionEndpoints();
app.MapReconciliationClaimEndpoints();

app.Run();

public partial class Program;
