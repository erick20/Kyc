using Kyc.Api.Middleware;
using Kyc.Application;
using Kyc.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "KYC Aggregator API",
        Version = "v1",
        Description = "API for KYC verification management"
    });
});

// Add application layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("KycDatabase")
    ?? throw new InvalidOperationException("Connection string 'KycDatabase' not found."));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add custom middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
