var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder.AddPostgres("postgres");
var identityDatabase = postgres.AddDatabase("identity", "contapop_identity");
postgres.AddDatabase("ledger", "contapop_ledger");
postgres.AddDatabase("billing", "contapop_billing");
postgres.AddDatabase("bookkeeping", "contapop_bookkeeping");
postgres.AddDatabase("reporting", "contapop_reporting");

var server = builder.AddProject<Projects.Contapop_Application_Server>("server")
    .WithReference(cache)
    .WaitFor(cache)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var identity = builder.AddProject<Projects.Contapop_Identity_Service>("identity-service")
    .WithReference(identityDatabase)
    .WaitFor(identityDatabase)
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
