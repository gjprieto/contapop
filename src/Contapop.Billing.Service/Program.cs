using System.Text;
using Azure.Storage.Blobs;
using Contapop.Billing.Service.Api;
using Contapop.Billing.Service.Application.Replication;
using Contapop.Billing.Service.Application.Commands;
using Contapop.Billing.Service.Application.Abstractions;
using Contapop.Billing.Service.Application.BackgroundJobs;
using Contapop.Billing.Service.Application.Attachments;
using Contapop.Billing.Service.Infrastructure.Attachments;
using Contapop.Billing.Service.Infrastructure.Reconciliation;
using Contapop.Billing.Service.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("billing")));
builder.Services.AddScoped<ProjectReplicationConsumer>();
builder.Services.AddScoped<TransactionReplicationConsumer>();
builder.Services.AddScoped<CounterpartyCommandHandler>();
builder.Services.AddScoped<InvoiceCommandHandler>();
builder.Services.AddScoped<PaymentCommandHandler>();
builder.Services.AddScoped<InvoiceAttachmentService>();
builder.Services.AddSingleton(new BlobServiceClient(builder.Configuration.GetConnectionString("billing-attachments") ?? throw new InvalidOperationException("Billing attachment storage is not configured.")));
builder.Services.AddScoped<InvoiceAttachmentStorage>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MarkInvoicesOverdueJob>();
builder.Services.AddHostedService<MarkInvoicesOverdueHostedService>();
builder.Services.AddHostedService<InvoiceAttachmentCleanupHostedService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IReconciliationClaimValidator, LedgerReconciliationClaimValidator>(client =>
    client.BaseAddress = new Uri(builder.Configuration["services:ledger-service:http:0"] ?? "http://localhost:5113"));
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
    var database = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
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
app.MapReplicationEndpoints();
app.MapCounterpartyEndpoints();
app.MapInvoiceEndpoints();
app.MapPaymentEndpoints();

app.Run();

public partial class Program;
