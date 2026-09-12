var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
builder.AddDapr();
var pubSub = builder.AddDaprPubSub("pubsub");
var internalJwtSigningKey = builder.AddParameter("internal-jwt-signing-key", secret: true);

var postgres = builder.AddPostgres("postgres");
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var billingAttachments = storage.AddBlobs("billing-attachments");
var identityDatabase = postgres.AddDatabase("identity", "contapop_identity");
var ledgerDatabase = postgres.AddDatabase("ledger", "contapop_ledger");
var billingDatabase = postgres.AddDatabase("billing", "contapop_billing");
var bookkeepingDatabase = postgres.AddDatabase("bookkeeping", "contapop_bookkeeping");
postgres.AddDatabase("reporting", "contapop_reporting");
var reconciliationDatabase = postgres.AddDatabase("reconciliation", "contapop_reconciliation");

var experienceApi = builder.AddProject<Projects.Contapop_Experience_Api>("experience-api")
    .WithReference(cache)
    .WithEnvironment("InternalJwt__SigningKey", internalJwtSigningKey)
    .WaitFor(cache)
    .WithHttpEndpoint(port: 5112)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var identity = builder.AddProject<Projects.Contapop_Identity_Service>("identity-service")
    .WithReference(identityDatabase)
    .WithEnvironment("InternalJwt__SigningKey", internalJwtSigningKey)
    .WaitFor(identityDatabase)
    .WithHttpEndpoint(port: 5111)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints()
    .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub));

var ledger = builder.AddProject<Projects.Contapop_Ledger_Service>("ledger-service")
    .WithReference(ledgerDatabase)
    .WithEnvironment("InternalJwt__SigningKey", internalJwtSigningKey)
    .WaitFor(ledgerDatabase)
    .WithHttpEndpoint(port: 5113)
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub));

var billing = builder.AddProject<Projects.Contapop_Billing_Service>("billing-service")
    .WithReference(billingDatabase)
    .WithReference(billingAttachments)
    .WithEnvironment("InternalJwt__SigningKey", internalJwtSigningKey)
    .WaitFor(billingDatabase)
    .WithHttpEndpoint(port: 5115)
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub));

var bookkeeping = builder.AddProject<Projects.Contapop_Bookkeeping_Service>("bookkeeping-service")
    .WithReference(bookkeepingDatabase)
    .WithEnvironment("InternalJwt__SigningKey", internalJwtSigningKey)
    .WaitFor(bookkeepingDatabase)
    .WithHttpEndpoint(port: 5116)
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub));

var reconciliation = builder.AddProject<Projects.Contapop_Reconciliation_Service>("reconciliation-service")
    .WithReference(reconciliationDatabase)
    .WithReference(ledger)
    .WithReference(billing)
    .WithEnvironment("InternalJwt__SigningKey", internalJwtSigningKey)
    .WaitFor(reconciliationDatabase)
    .WithHttpEndpoint(port: 5114)
    .WithHttpHealthCheck("/health")
    .WithDaprSidecar(sidecar => sidecar.WithReference(pubSub));

experienceApi.WithReference(identity).WaitFor(identity);
experienceApi.WithReference(ledger).WaitFor(ledger);
experienceApi.WithReference(billing).WaitFor(billing);
experienceApi.WithReference(bookkeeping).WaitFor(bookkeeping);
experienceApi.WithReference(reconciliation).WaitFor(reconciliation);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(experienceApi)
    .WaitFor(experienceApi)
    .WithHttpEndpoint(port: 5173);

experienceApi.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
