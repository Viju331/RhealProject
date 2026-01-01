using RhealAI.Application.Interfaces;
using RhealAI.Infrastructure.Services;
using RhealAI.Infrastructure.AI;
using RhealAI.Infrastructure.FileProcessing;
using RhealAI.Infrastructure.Persistence;
using RhealAI.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel server limits for large file uploads
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 2147483648; // 2 GB
});

// Add services to the container
builder.Services.AddControllers(options =>
{
    options.MaxModelBindingCollectionSize = int.MaxValue;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition");
    });
});

// Register application services
builder.Services.AddSingleton<InMemoryCache>();
builder.Services.AddSingleton<ZipExtractor>();
builder.Services.AddSingleton<FolderAnalyzer>();
builder.Services.AddSingleton<GitHubProcessor>();
builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddScoped<StandardsGeneratorService>();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IDocumentationService, DocumentationService>();
builder.Services.AddScoped<IAIAnalysisService, AIAnalysisService>();
builder.Services.AddScoped<IProgressHub, SignalRProgressHub>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RhealAI API V1");
        c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseCors("AllowAngular");
app.UseHttpsRedirection();
app.MapControllers();
app.MapHub<RhealAI.API.Hubs.UploadProgressHub>("/hubs/upload-progress");

app.Run();
