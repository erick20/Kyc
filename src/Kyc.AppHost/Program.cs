var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("kyc-postgres-data")
    .WithPgAdmin();

var database = postgres.AddDatabase("kyc");

builder.AddProject<Projects.Kyc_Api>("kyc-api")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
