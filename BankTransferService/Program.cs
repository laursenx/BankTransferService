using BankTransferService.Data;
using BankTransferService.Diagnostics;
using BankTransferService.Interfaces;
using BankTransferService.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "BankTransferService API",
            Version = "v1",
            Description = "Microservice for executing atomic bank account transfers.",
        }
    );
});

// Problem details and exception handling (modern ASP.NET Core 8.0+ pattern)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITransferRepository, TransferRepository>();
builder.Services.AddScoped<ITransferQueryRepository, TransferQueryRepository>();
builder.Services.AddScoped<ITransferService, TransferService>();

var connectionString =
    builder.Configuration.GetConnectionString("BankDb")
    ?? throw new InvalidOperationException("Connection string 'BankDb' is not configured.");
builder.Services.AddHealthChecks().AddSqlServer(connectionString, name: "database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
}

// Modern exception handling middleware (replaces old UseExceptionHandler pattern)
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
