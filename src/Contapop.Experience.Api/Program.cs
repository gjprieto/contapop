using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddServiceDiscovery();
builder.Services.ConfigureHttpClientDefaults(http => http.AddServiceDiscovery());
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("account-owner", policy => policy.RequireRole("account-owner")));
builder.Services.AddHttpClient("identity-service", client => client.BaseAddress = new Uri(
    builder.Configuration["IdentityService:BaseUrl"] ?? "https+http://identity-service"));
builder.Services.AddHttpClient("ledger-service", client => client.BaseAddress = new Uri(
    builder.Configuration["LedgerService:BaseUrl"] ?? "https+http://ledger-service"));
builder.Services.AddHttpClient("billing-service", client => client.BaseAddress = new Uri(
    builder.Configuration["BillingService:BaseUrl"] ?? "https+http://billing-service"));
builder.Services.AddHttpClient("reconciliation-service", client => client.BaseAddress = new Uri(
    builder.Configuration["ReconciliationService:BaseUrl"] ?? "https+http://reconciliation-service"));
builder.Services.AddSingleton<InternalJwtIssuer>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/experience/v1/auth/login", async (
    LoginRequest request,
    IHttpClientFactory clientFactory,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    using var response = await clientFactory.CreateClient("identity-service")
        .PostAsJsonAsync("/api/v1/auth/validate-credentials", request, cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
        return Results.Unauthorized();
    }

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Identity service unavailable");
    }

    var identity = await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>(cancellationToken);
    if (identity is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Identity service returned an invalid response");
    }

    var claims = new ClaimsIdentity(
        [
            new Claim("tenant_id", identity.TenantId.ToString()),
            new Claim("user_id", identity.UserId.ToString()),
            new Claim(ClaimTypes.Role, identity.Role),
        ],
        CookieAuthenticationDefaults.AuthenticationScheme);
    await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claims));
    return Results.NoContent();
})
.AllowAnonymous()
.WithName("ExperienceLogin")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized);

app.MapPost("/experience/v1/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.NoContent();
})
.RequireAuthorization("account-owner")
.WithName("ExperienceLogout")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized);

app.MapGet("/experience/v1/user", async (
    HttpContext httpContext,
    IHttpClientFactory clientFactory,
    InternalJwtIssuer jwtIssuer,
    CancellationToken cancellationToken) =>
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(httpContext.User));
    using var response = await clientFactory.CreateClient("identity-service").SendAsync(request, cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
        return Results.Unauthorized();
    }

    if (!response.IsSuccessStatusCode)
    {
        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Identity service unavailable");
    }

    var user = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(cancellationToken);
    return user is null
        ? Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Identity service returned an invalid response")
        : Results.Ok(user);
})
.RequireAuthorization("account-owner")
.WithName("GetExperienceUser")
.Produces<CurrentUserResponse>()
.Produces(StatusCodes.Status401Unauthorized);

app.MapPatch("/experience/v1/user", async (
    UpdateUserProfileRequest profile,
    HttpContext httpContext,
    IHttpClientFactory clientFactory,
    InternalJwtIssuer jwtIssuer,
    CancellationToken cancellationToken) =>
    await ForwardUserUpdateAsync(
        "/api/v1/users/me/profile",
        profile,
        httpContext,
        clientFactory,
        jwtIssuer,
        cancellationToken))
.RequireAuthorization("account-owner")
.WithName("UpdateExperienceUserProfile")
.Produces<UpdateUserProfileResponse>()
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status409Conflict);

app.MapPatch("/experience/v1/settings", async (
    UpdateUserPreferencesRequest preferences,
    HttpContext httpContext,
    IHttpClientFactory clientFactory,
    InternalJwtIssuer jwtIssuer,
    CancellationToken cancellationToken) =>
    await ForwardUserUpdateAsync(
        "/api/v1/users/me/preferences",
        preferences,
        httpContext,
        clientFactory,
        jwtIssuer,
        cancellationToken))
.RequireAuthorization("account-owner")
.WithName("UpdateExperienceUserPreferences")
.Produces<UpdateUserPreferencesResponse>()
.ProducesValidationProblem()
.ProducesProblem(StatusCodes.Status409Conflict);

var financialOverview = app.MapGroup("/experience/v1/financial-overview")
    .RequireAuthorization("account-owner");
financialOverview.MapGet("/bank-accounts", (HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Get, "/api/v1/bank-accounts" + context.Request.QueryString, null, context, clientFactory, jwtIssuer, cancellationToken));
financialOverview.MapPost("/bank-accounts", (JsonElement body, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Post, "/api/v1/bank-accounts", body, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true));
financialOverview.MapPost("/bank-accounts/{bankAccountId:guid}/archive", (Guid bankAccountId, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Post, $"/api/v1/bank-accounts/{bankAccountId}/archive", null, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true, requiresVersion: true));
financialOverview.MapGet("/payment-cards", (HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Get, "/api/v1/payment-cards" + context.Request.QueryString, null, context, clientFactory, jwtIssuer, cancellationToken));
financialOverview.MapPost("/payment-cards", (JsonElement body, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Post, "/api/v1/payment-cards", body, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true));
financialOverview.MapDelete("/payment-cards/{cardId:guid}", (Guid cardId, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Delete, $"/api/v1/payment-cards/{cardId}", null, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true, requiresVersion: true));

var transactions = app.MapGroup("/experience/v1/transactions")
    .RequireAuthorization("account-owner");
transactions.MapGet("", (HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Get, "/api/v1/transactions" + context.Request.QueryString, null, context, clientFactory, jwtIssuer, cancellationToken));
transactions.MapGet("/{transactionId:guid}", (Guid transactionId, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Get, $"/api/v1/transactions/{transactionId}", null, context, clientFactory, jwtIssuer, cancellationToken));
transactions.MapPost("", (JsonElement body, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Post, "/api/v1/transactions", body, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true));
transactions.MapPatch("/{transactionId:guid}", (Guid transactionId, JsonElement body, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Patch, $"/api/v1/transactions/{transactionId}", body, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true, requiresVersion: true));
transactions.MapPost("/{transactionId:guid}/archive", (Guid transactionId, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerAsync(HttpMethod.Post, $"/api/v1/transactions/{transactionId}/archive", null, context, clientFactory, jwtIssuer, cancellationToken, requiresIdempotencyKey: true, requiresVersion: true));
transactions.MapPost("/import", ([FromForm] IFormFile? file, [FromForm] Guid bankAccountId, [FromForm] string? columnMapping, HttpContext context, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken) =>
    ForwardLedgerImportAsync(file, bankAccountId, columnMapping, context, clientFactory, jwtIssuer, cancellationToken))
    .Accepts<IFormFile>("multipart/form-data")
    .DisableAntiforgery();

var counterparties = app.MapGroup("/experience/v1/counterparties").RequireAuthorization("account-owner");
counterparties.MapGet("", (HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Get, "/api/v1/counterparties" + context.Request.QueryString, null, context, clients, jwt, ct));
counterparties.MapGet("/{counterpartyId:guid}", (Guid counterpartyId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Get, $"/api/v1/counterparties/{counterpartyId}", null, context, clients, jwt, ct));
counterparties.MapPost("", (JsonElement body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Post, "/api/v1/counterparties", body, context, clients, jwt, ct, true));
counterparties.MapPatch("/{counterpartyId:guid}", (Guid counterpartyId, JsonElement body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Patch, $"/api/v1/counterparties/{counterpartyId}", body, context, clients, jwt, ct, true, true));
counterparties.MapPost("/{counterpartyId:guid}/archive", (Guid counterpartyId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Post, $"/api/v1/counterparties/{counterpartyId}/archive", null, context, clients, jwt, ct, true, true));

var invoices = app.MapGroup("/experience/v1/invoices").RequireAuthorization("account-owner");
invoices.MapGet("", (HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Get, "/api/v1/invoices" + context.Request.QueryString, null, context, clients, jwt, ct));
invoices.MapGet("/{invoiceId:guid}", (Guid invoiceId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}", null, context, clients, jwt, ct));
invoices.MapPost("", (JsonElement body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Post, "/api/v1/invoices", body, context, clients, jwt, ct, true));
invoices.MapPost("/{invoiceId:guid}/issue", (Guid invoiceId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/issue", null, context, clients, jwt, ct, true, true));
invoices.MapPost("/{invoiceId:guid}/void", (Guid invoiceId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Post, $"/api/v1/invoices/{invoiceId}/void", null, context, clients, jwt, ct, true, true));
invoices.MapGet("/{invoiceId:guid}/document", (Guid invoiceId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingDocumentAsync(invoiceId, context, clients, jwt, ct));

var payments = app.MapGroup("/experience/v1/payments").RequireAuthorization("account-owner");
payments.MapGet("", (HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Get, "/api/v1/payments" + context.Request.QueryString, null, context, clients, jwt, ct));
payments.MapPost("", (JsonElement body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ForwardBillingAsync(HttpMethod.Post, "/api/v1/payments", body, context, clients, jwt, ct, true));
payments.MapGet("/unreconciled-transactions", (HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => ListPaymentUnreconciledTransactionsAsync(context, clients, jwt, ct));
payments.MapPost("/{paymentId:guid}/reconcile", (Guid paymentId, ReconcilePaymentExperienceRequest body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwt, CancellationToken ct) => StartPaymentReconciliationAsync(paymentId, body, context, clients, jwt, ct));

app.MapGet("/health", () => Results.Ok());
app.Run();

static async Task<IResult> ForwardUserUpdateAsync<TRequest>(
    string path,
    TRequest body,
    HttpContext httpContext,
    IHttpClientFactory clientFactory,
    InternalJwtIssuer jwtIssuer,
    CancellationToken cancellationToken)
{
    if (!httpContext.Request.Headers.TryGetValue("If-Match", out var ifMatch) || string.IsNullOrWhiteSpace(ifMatch))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["If-Match"] = ["A quoted current resource version is required."],
        });
    }

    using var request = new HttpRequestMessage(HttpMethod.Patch, path)
    {
        Content = JsonContent.Create(body),
    };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(httpContext.User));
    request.Headers.TryAddWithoutValidation("If-Match", ifMatch.ToString());
    using var response = await clientFactory.CreateClient("identity-service").SendAsync(request, cancellationToken);

    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
    {
        return Results.Unauthorized();
    }

    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
    {
        return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "The user information has changed. Refresh and try again.");
    }

    if (!response.IsSuccessStatusCode)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest
            || response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return Results.Content(content, response.Content.Headers.ContentType?.MediaType, statusCode: (int)response.StatusCode);
        }

        return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Identity service unavailable");
    }

    var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
    return Results.Json(responseBody);
}

static async Task<IResult> ForwardLedgerAsync(
    HttpMethod method,
    string path,
    object? body,
    HttpContext httpContext,
    IHttpClientFactory clientFactory,
    InternalJwtIssuer jwtIssuer,
    CancellationToken cancellationToken,
    bool requiresIdempotencyKey = false,
    bool requiresVersion = false)
{
    path = path.Replace("date-desc", "date:desc", StringComparison.Ordinal)
        .Replace("date-asc", "date:asc", StringComparison.Ordinal)
        .Replace("amount-desc", "amount:desc", StringComparison.Ordinal)
        .Replace("amount-asc", "amount:asc", StringComparison.Ordinal);

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
    if (requiresIdempotencyKey && string.IsNullOrWhiteSpace(idempotencyKey))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    }

    var ifMatch = httpContext.Request.Headers.IfMatch.ToString();
    if (requiresVersion && string.IsNullOrWhiteSpace(ifMatch))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current resource version is required."] });
    }

    using var request = new HttpRequestMessage(method, path);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(httpContext.User));
    if (requiresIdempotencyKey) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.ToString());
    if (requiresVersion) request.Headers.TryAddWithoutValidation("If-Match", ifMatch.ToString());
    if (body is not null) request.Content = JsonContent.Create(body);
    return await ForwardLedgerResponseAsync(request, clientFactory, cancellationToken);
}

static Task<IResult> ForwardBillingAsync(HttpMethod method, string path, object? body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwtIssuer, CancellationToken ct, bool requiresIdempotencyKey = false, bool requiresVersion = false) =>
    ForwardServiceAsync("billing-service", method, path, body, context, clients, jwtIssuer, ct, requiresIdempotencyKey, requiresVersion);

static async Task<IResult> ForwardServiceAsync(string service, HttpMethod method, string path, object? body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwtIssuer, CancellationToken ct, bool requiresIdempotencyKey, bool requiresVersion)
{
    var key = context.Request.Headers["Idempotency-Key"].ToString();
    if (requiresIdempotencyKey && !Guid.TryParse(key, out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    var version = context.Request.Headers.IfMatch.ToString();
    if (requiresVersion && string.IsNullOrWhiteSpace(version)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["If-Match"] = ["A quoted current resource version is required."] });
    using var request = new HttpRequestMessage(method, path);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(context.User));
    if (requiresIdempotencyKey) request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
    if (requiresVersion) request.Headers.TryAddWithoutValidation("If-Match", version);
    if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await clients.CreateClient(service).SendAsync(request, ct);
    var content = await response.Content.ReadAsStringAsync(ct);
    return response.IsSuccessStatusCode || response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.UnprocessableEntity
        ? Results.Content(content, response.Content.Headers.ContentType?.MediaType ?? "application/json", statusCode: (int)response.StatusCode)
        : Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: $"{service} unavailable");
}

static async Task<IResult> ForwardBillingDocumentAsync(Guid invoiceId, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwtIssuer, CancellationToken ct)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/invoices/{invoiceId}/document");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(context.User));
    using var response = await clients.CreateClient("billing-service").SendAsync(request, ct);
    if (!response.IsSuccessStatusCode) return response.StatusCode == System.Net.HttpStatusCode.NotFound ? Results.NotFound() : Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Billing service unavailable");
    var bytes = await response.Content.ReadAsByteArrayAsync(ct);
    return Results.File(bytes, response.Content.Headers.ContentType?.MediaType ?? "application/pdf", response.Content.Headers.ContentDisposition?.FileNameStar ?? $"invoice-{invoiceId}.pdf");
}

static async Task<IResult> ListPaymentUnreconciledTransactionsAsync(HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwtIssuer, CancellationToken ct)
{
    var reconciled = new HashSet<string?>();
    var page = 1;
    while (true)
    {
        using var paymentsRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/payments?page={page}&pageSize=100");
        paymentsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(context.User));
        using var paymentsResponse = await clients.CreateClient("billing-service").SendAsync(paymentsRequest, ct);
        if (!paymentsResponse.IsSuccessStatusCode) return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Billing service unavailable");
        var payments = await paymentsResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
        foreach (var payment in payments.GetProperty("items").EnumerateArray())
        {
            if (payment.TryGetProperty("reconciledTransactionId", out var id) && id.ValueKind == JsonValueKind.String) reconciled.Add(id.GetString());
        }
        if (page * payments.GetProperty("pageSize").GetInt32() >= payments.GetProperty("totalCount").GetInt32()) break;
        page++;
    }
    using var ledgerRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/transactions/unreconciled" + context.Request.QueryString);
    ledgerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(context.User));
    using var ledgerResponse = await clients.CreateClient("ledger-service").SendAsync(ledgerRequest, ct);
    if (!ledgerResponse.IsSuccessStatusCode) return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Ledger service unavailable");
    var transactions = await ledgerResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
    var items = transactions.GetProperty("items").EnumerateArray().Where(transaction => !reconciled.Contains(transaction.GetProperty("transactionId").GetString())).ToArray();
    return Results.Json(new { items, page = transactions.GetProperty("page").GetInt32(), pageSize = transactions.GetProperty("pageSize").GetInt32(), totalCount = items.Length });
}

static async Task<IResult> StartPaymentReconciliationAsync(Guid paymentId, ReconcilePaymentExperienceRequest body, HttpContext context, IHttpClientFactory clients, InternalJwtIssuer jwtIssuer, CancellationToken ct)
{
    if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out _)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    if (body.TransactionId == Guid.Empty || body.PaymentVersion <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Transaction ID and payment version are required."] });
    using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/v1/payment-reconciliations") { Content = JsonContent.Create(new { transactionId = body.TransactionId, paymentId, paymentVersion = body.PaymentVersion }) };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(context.User));
    request.Headers.TryAddWithoutValidation("Idempotency-Key", context.Request.Headers["Idempotency-Key"].ToString());
    using var response = await clients.CreateClient("reconciliation-service").SendAsync(request, ct);
    var content = await response.Content.ReadAsStringAsync(ct);
    return response.IsSuccessStatusCode ? Results.Content(content, response.Content.Headers.ContentType?.MediaType ?? "application/json", statusCode: (int)response.StatusCode) : Results.Content(content, response.Content.Headers.ContentType?.MediaType ?? "application/problem+json", statusCode: (int)response.StatusCode);
}

static async Task<IResult> ForwardLedgerImportAsync(IFormFile? file, Guid bankAccountId, string? columnMapping, HttpContext httpContext, IHttpClientFactory clientFactory, InternalJwtIssuer jwtIssuer, CancellationToken cancellationToken)
{
    if (!httpContext.Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A GUID idempotency key is required."] });
    }

    if (file is null || file.Length == 0 || bankAccountId == Guid.Empty || string.IsNullOrWhiteSpace(columnMapping))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A non-empty file, bank account ID, and column mapping are required."] });
    }

    using var content = new MultipartFormDataContent();
    content.Add(new StringContent(bankAccountId.ToString()), "bankAccountId");
    content.Add(new StringContent(columnMapping), "columnMapping");
    await using var source = file.OpenReadStream();
    using var fileContent = new StreamContent(source);
    fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
    content.Add(fileContent, "file", file.FileName);

    using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transactions/import") { Content = content };
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwtIssuer.Create(httpContext.User));
    request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.ToString());
    return await ForwardLedgerResponseAsync(request, clientFactory, cancellationToken);
}

static async Task<IResult> ForwardLedgerResponseAsync(HttpRequestMessage request, IHttpClientFactory clientFactory, CancellationToken cancellationToken)
{
    using var response = await clientFactory.CreateClient("ledger-service").SendAsync(request, cancellationToken);
    var content = await response.Content.ReadAsStringAsync(cancellationToken);
    if (response.IsSuccessStatusCode || response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.UnprocessableEntity)
    {
        return Results.Content(content, response.Content.Headers.ContentType?.MediaType ?? "application/json", statusCode: (int)response.StatusCode);
    }

    return Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "Ledger service unavailable");
}

public sealed record LoginRequest(string Email, string Password);
public sealed record AuthenticatedUserResponse(Guid TenantId, Guid UserId, string Role);
public sealed record CurrentUserResponse(Guid UserId, Guid TenantId, Guid ProjectId, string Name, string Email, string Theme, string Language, bool NotificationsEnabled, int Version);
public sealed record UpdateUserProfileRequest(string Name);
public sealed record UpdateUserProfileResponse(Guid UserId, string Name, DateTimeOffset UpdatedAt, int Version);
public sealed record UpdateUserPreferencesRequest(string? Theme, string? Language, bool? NotificationsEnabled);
public sealed record UpdateUserPreferencesResponse(Guid UserId, string Theme, string Language, bool NotificationsEnabled, DateTimeOffset UpdatedAt, int Version);
public sealed record ReconcilePaymentExperienceRequest(Guid TransactionId, int PaymentVersion);

public sealed class InternalJwtIssuer(IConfiguration configuration)
{
    public string Create(ClaimsPrincipal user)
    {
        var tenantId = user.FindFirstValue("tenant_id") ?? throw new InvalidOperationException("Tenant claim is required.");
        var userId = user.FindFirstValue("user_id") ?? throw new InvalidOperationException("User claim is required.");
        var role = user.FindFirstValue(ClaimTypes.Role) ?? throw new InvalidOperationException("Role claim is required.");
        var key = configuration["InternalJwt:SigningKey"] ?? throw new InvalidOperationException("Internal JWT signing key is not configured.");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [new Claim("tenant_id", tenantId), new Claim("user_id", userId), new Claim(ClaimTypes.Role, role)],
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public partial class Program;
