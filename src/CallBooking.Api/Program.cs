using CallBooking.Api.Generated;
using CallBooking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register the handwritten implementations behind the generated controller
// seams. These are placeholder proof-of-seam implementations only.
builder.Services.AddScoped<IEventTypesController, EventTypesService>();
builder.Services.AddScoped<IBookingsController, BookingsService>();
builder.Services.AddScoped<ICalendarController, CalendarService>();

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
