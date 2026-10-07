var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CallBooking_Api>("api");

builder.AddViteApp("web", "../callbooking.web")
    .WithReference(api);

builder.Build().Run();
