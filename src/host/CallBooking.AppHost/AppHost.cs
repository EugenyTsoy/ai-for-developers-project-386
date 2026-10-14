var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CallBooking_Api>("api");

builder.AddViteApp("web", "../../frontend/callbooking.web")
    .WithReference(api);

builder.Build().Run();
