var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.CallBooking_Api>("api", launchProfileName: "http");

builder.AddViteApp("web", "../../frontend/callbooking.web")
    .WithReference(api)
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("http"))
    .WaitFor(api);

builder.Build().Run();
