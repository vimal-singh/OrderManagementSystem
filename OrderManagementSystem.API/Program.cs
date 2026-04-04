using Microsoft.EntityFrameworkCore;
using OrderManagementSystem.API.Services;
using OrderManagementSystem.API.Data;
using Serilog;
using OrderManagementSystem.API.Middlewares;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
// Reads all Serilog settings (sinks, levels, properties, enrichers)
// from appsettings.json → "Serilog" section.
// This allows you to control logging behavior WITHOUT touching the code.
.ReadFrom.Configuration(builder.Configuration)
// Adds contextual properties to every log event.
// Enrich log events with any properties stored in Serilog's LogContext,
// such as CorrelationId pushed by our CorrelationIdMiddleware.
// This makes those contextual values automatically appear on every log	
// written during the lifetime of the current request.
.Enrich.FromLogContext()
// Builds and creates the Serilog logger instance.
// After this, Serilog becomes ready to capture logs.
.CreateLogger();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Allow services to access HttpContext (used for CorrelationId, etc.)
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

// Add DbContext class to use DI in the application
builder.Services.AddDbContext<OrderManagementDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("OrderSystemConnectionString")));


// Register application services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

// Logging Configuration
// Remove default logging providers
builder.Logging.ClearProviders();
builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
// Attach a Correlation ID to each request for tracing
app.UseCorrelationId();

app.MapControllers();
app.Run();

