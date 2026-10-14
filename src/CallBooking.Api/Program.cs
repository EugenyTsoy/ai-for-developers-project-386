using CallBooking.Api.Api;
using CallBooking.Api.Generated;
using CallBooking.Application.Ports;
using CallBooking.Infrastructure.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Shared in-memory calendar store. A single instance is shared by
// every repository inside one host so that persisted entities are
// visible to subsequent reads instead of falling back to
// independent placeholder collections.
builder.Services.AddSingleton<IInMemoryCalendarStore, InMemoryCalendarStore>();
builder.Services.AddScoped<IEventTypeRepository, InMemoryEventTypeRepository>();
builder.Services.AddScoped<IBookingRepository, InMemoryBookingRepository>();

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
