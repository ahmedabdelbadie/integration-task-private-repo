using System.Text.Json.Serialization;
using GitHubMonitor.Api.Errors;
using GitHubMonitor.Api.Services;
using GitHubMonitor.Api.Wcf;

const string CorsPolicy = "frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// WCF client: one ChannelFactory for the app lifetime, wrapped behind an interface.
builder.Services.AddOptions<WcfClientOptions>()
    .Bind(builder.Configuration.GetSection(WcfClientOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<ICommitServiceClient, CommitServiceClient>();
builder.Services.AddScoped<IGitHubCommitsService, GitHubCommitsService>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .WithMethods("GET")));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
