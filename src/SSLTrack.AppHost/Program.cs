var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.SSLTrack>("ssltrack");

builder.Build().Run();
