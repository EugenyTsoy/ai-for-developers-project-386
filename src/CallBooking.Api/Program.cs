using CallBooking.Api.Api;
using CallBooking.Api.Generated;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the handwritten API-boundary adapters behind the generated
// controller seams. These are placeholder proof-of-seam implementations only.
builder.Services.AddScoped<IEventTypesController, EventTypesControllerImpl>();
builder.Services.AddScoped<IBookingsController, BookingsControllerImpl>();
builder.Services.AddScoped<ICalendarController, CalendarControllerImpl>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
