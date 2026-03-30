using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartArchive.Data;
using SmartArchive.Services;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

// Configure DbContext - expects a connection string named "DefaultConnection" in configuration
services.AddDbContext<AppDbContext>(opts =>
{
    var conn = configuration.GetConnectionString("DefaultConnection") ?? configuration["DefaultConnection"];
    if (string.IsNullOrEmpty(conn))
    {
        // fallback to local DB for development convenience (LocalDB)
        conn = "Server=(localdb)\\mssqllocaldb;Database=SmartArchiveDb;Trusted_Connection=True;MultipleActiveResultSets=true";
    }
    opts.UseSqlServer(conn);
});

// Add services
services.AddScoped<IArchiveService, ArchiveService>();
services.AddScoped<IOllamaService, OllamaService>();

// Configure HttpClient for Ollama local instance - BaseAddress can be overridden via configuration "OllamaBaseUrl"
var ollamaBase = configuration["OllamaBaseUrl"] ?? "http://localhost:11434";
services.AddHttpClient<OllamaService>(client => { client.BaseAddress = new Uri(ollamaBase); }).SetHandlerLifetime(TimeSpan.FromMinutes(5));
services.AddHttpClient<IOllamaService, OllamaService>(client => { client.BaseAddress = new Uri(ollamaBase); }).SetHandlerLifetime(TimeSpan.FromMinutes(5));

services.AddControllers().AddJsonOptions(opts =>
{
    opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.Run();
