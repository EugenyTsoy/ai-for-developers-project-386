using System.Text.Json.Serialization.Metadata;
using CallBooking.Api.Api;
using CallBooking.Api.Configuration;
using CallBooking.Api.Generated;
using CallBooking.Api.Serialization;
using CallBooking.Application.Bookings;
using CallBooking.Application.Calendar;
using CallBooking.Application.EventTypes;
using CallBooking.Application.Ports;
using CallBooking.Domain.Calendar;
using CallBooking.Infrastructure.InMemory;

var builder = WebApplication.CreateBuilder(args);

// PaaS hosts provide PORT; bind all interfaces rather than localhost.
if (builder.Configuration["PORT"] is { } portValue)
{
    if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
    {
        throw new InvalidOperationException("PORT must be an integer between 1 and 65535.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new BookingInputValidationFilter());
    options.Filters.Add(new EventTypeInputValidationFilter());
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new UtcInstantJsonConverter());
    options.JsonSerializerOptions.TypeInfoResolver = options.JsonSerializerOptions.TypeInfoResolver!
        .WithAddedModifier(EventTypeRequestJson.Configure);
});
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton(_ => CalendarConfigurationLoader.Load(builder.Configuration));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CalendarContext>();
builder.Services.AddScoped<ListAvailableSlots>();
builder.Services.AddScoped<CreateBooking>();
builder.Services.AddScoped<ListUpcomingBookings>();
builder.Services.AddScoped<CreateEventType>();
builder.Services.AddScoped<ListEventTypes>();

// Shared in-memory calendar store. A single instance is shared by
// every repository inside one host so that persisted entities are
// visible to subsequent reads instead of falling back to
// independent placeholder collections.
builder.Services.AddSingleton<IInMemoryCalendarStore, InMemoryCalendarStore>();
builder.Services.AddSingleton<ICalendarWriteScope>(services => services.GetRequiredService<IInMemoryCalendarStore>());
builder.Services.AddScoped<IEventTypeRepository, InMemoryEventTypeRepository>();
builder.Services.AddScoped<IBookingRepository, InMemoryBookingRepository>();

// Register the handwritten API-boundary adapters behind the generated
// controller seams.
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
    app.UseHttpsRedirection();
}

// Production TLS terminates at the hosting platform, not in the container.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// Only known client routes fall back to the SPA. Unknown API/asset paths stay 404.
app.MapFallbackToFile("/booking", "index.html");
app.MapFallbackToFile("/events", "index.html");

app.Run();
