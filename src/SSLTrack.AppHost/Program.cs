var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.SSLTrack>("ssltrack");

builder.AddProject<Projects.SSLTrackAgent>("ssltrackagent");

builder.Build().Run();
