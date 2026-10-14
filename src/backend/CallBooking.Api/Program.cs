using CallBooking.Api.Api;
using CallBooking.Api.Configuration;
using CallBooking.Api.Generated;
using CallBooking.Application.Calendar;
using CallBooking.Application.Ports;
using CallBooking.Domain.Calendar;
using CallBooking.Infrastructure.InMemory;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(_ => CalendarConfigurationLoader.Load(builder.Configuration));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CalendarContext>();

// Shared in-memory calendar store. A single instance is shared by
// every repository inside one host so that persisted entities are
// visible to subsequent reads instead of falling back to
// independent placeholder collections.
builder.Services.AddSingleton<IInMemoryCalendarStore, InMemoryCalendarStore>();
builder.Services.AddScoped<IEventTypeRepository, InMemoryEventTypeRepository>();
builder.Services.AddScoped<IBookingRepository, InMemoryBookingRepository>();

// Register the handwritten API-boundary adapters behind the generated
// controller seams. Event types and bookings remain placeholders for later tickets.
builder.Services.AddScoped<IEventTypesController, EventTypesControllerImpl>();
builder.Services.AddScoped<IBookingsController, BookingsControllerImpl>();
builder.Services.AddScoped<ICalendarController, CalendarControllerImpl>();

var app = builder.Build();

// Fail at startup instead of serving metadata for an invalid calendar.
_ = app.Services.GetRequiredService<CalendarConfiguration>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
