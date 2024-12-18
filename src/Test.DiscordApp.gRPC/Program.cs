using CorrelationId;
using CorrelationId.DependencyInjection;
using Microsoft.OpenApi.Models;
using Serilog;
using Test.DiscordApp.Application;
using Test.DiscordApp.gRPC.Services;
using Test.DiscordApp.Domain.Config;
using Test.DiscordApp.Infrastructure;

namespace Test.DiscordApp.gRPC;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Program");
        logger.LogInformation("Starting TestDiscordApp.Application...");

        var builder = WebApplication.CreateBuilder(args);

        ConfigureBuilder(builder);

        var app = builder.Build();
        ConfigureApp(app);

        await app.RunAsync();
    }

    private static void ConfigureBuilder(WebApplicationBuilder builder)
    {
        #region Configure settings

        builder.Configuration
            .AddJsonFile("Config/appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("Config/logging.json", optional: false, reloadOnChange: true)
            .AddUserSecrets(typeof(Program).Assembly, optional: false)
            .AddEnvironmentVariables();
        builder.Services
            .Configure<DiscordConfig>(builder.Configuration.GetSection("Discord"))
            .Configure<GithubConfig>(builder.Configuration.GetSection("Github"))
            .AddSingleton(TimeProvider.System);
        builder.Services.AddDefaultCorrelationId(options =>
        {
            options.CorrelationIdGenerator = () => Guid.NewGuid().ToString("N");
            options.AddToLoggingScope = true;
            options.EnforceHeader = false;
            options.IgnoreRequestHeader = false;
            options.IncludeInResponse = true;
            options.LoggingScopeKey = "CorrelationId";
            options.RequestHeader = "X-Correlation-Id";
            options.ResponseHeader = "X-Correlation-Id";
            options.UpdateTraceIdentifier = true;
        });

        #endregion

        #region Configure Logging setting

        builder.Logging.ClearProviders();
        builder.Host.AddSerilog();

        #endregion

        #region Configure project specific services

        builder
            .AddInfrastructureServices(builder.Configuration)
            .AddApplication();
        builder.Host.ConfigureDiscordClient(builder.Configuration);

        #endregion

        #region Configure gRPC

        builder.Services
            .AddCors(c => c.AddPolicy("AllowAllOrigins", b => b
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod()
            ))
            .AddGrpc()
            .AddJsonTranscoding();

        #endregion

        #region Configure Swagger

        builder.Services
            .AddGrpcSwagger()
            .AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "gRPC transcoding", Version = "v1" });
                var filePath = Path.Combine(AppContext.BaseDirectory, "Server.xml");
                c.IncludeXmlComments(filePath);
                c.IncludeGrpcXmlComments(filePath, includeControllerXmlComments: true);
            });

        #endregion
    }

    private static void ConfigureApp(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Test.DiscordApp API");
                c.RoutePrefix = string.Empty; // Automatically open Swagger at the root
            });
        }
        app.UseCorrelationId();
        app.UseCors("AllowAllOrigins");
        app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });
        app.MapGrpcService<GithubService>();
        app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");
        app.UseSerilogRequestLogging();
    }

    private static void AddSerilog(this ConfigureHostBuilder host)
    {
        host.UseSerilog((context, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration);
        });
    }
}