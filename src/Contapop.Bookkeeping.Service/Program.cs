using System.Text;
using Contapop.Bookkeeping.Service.Api;
using Contapop.Bookkeeping.Service.Application.Replication;
using Contapop.Bookkeeping.Service.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<BookkeepingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("bookkeeping")));
builder.Services.AddScoped<ProjectReplicationConsumer>();
builder.Services.AddScoped<TransactionReplicationConsumer>();
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
    var database = scope.ServiceProvider.GetRequiredService<BookkeepingDbContext>();
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

app.Run();

public partial class Program;
