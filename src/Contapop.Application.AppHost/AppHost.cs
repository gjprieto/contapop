var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
builder.AddDapr();
var pubSub = builder.AddDaprPubSub("pubsub");
var internalJwtSigningKey = builder.AddParameter("internal-jwt-signing-key", secret: true);

var postgres = builder.AddPostgres("postgres");
var identityDatabase = postgres.AddDatabase("identity", "contapop_identity");
var ledgerDatabase = postgres.AddDatabase("ledger", "contapop_ledger");
postgres.AddDatabase("billing", "contapop_billing");
postgres.AddDatabase("bookkeeping", "contapop_bookkeeping");
postgres.AddDatabase("reporting", "contapop_reporting");

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

experienceApi.WithReference(identity).WaitFor(identity);
experienceApi.WithReference(ledger).WaitFor(ledger);

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(experienceApi)
    .WaitFor(experienceApi)
    .WithHttpEndpoint(port: 5173);

experienceApi.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
