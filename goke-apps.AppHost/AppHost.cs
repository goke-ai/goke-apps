var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Goke_WebServer>("goke-webserver");



builder.AddProject<Projects.Goke_Hyb>("goke-hyb");



builder.AddProject<Projects.Goke_Hyb_Web>("goke-hyb-web");



builder.Build().Run();
