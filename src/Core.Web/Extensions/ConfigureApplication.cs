using System.Text;
using System.Text.Json.Serialization;
using Core.Domain.Configurations;
using Core.Domain.Helpers;
using Core.Infrastructure.Caching;
using Core.Infrastructure.Data.Context;
using Core.Infrastructure.Data.Repositories;
using Core.Infrastructure.Security;
using Core.Web.Attributes;
using Core.Web.Constants;
using Core.Web.CronSchedulers;
using Core.Web.Filters;
using Core.Web.Middlewares;
using Core.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NCrontab;
using Npgsql;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace Core.Web.Extensions;

public static class ConfigureApplication
{
    public static void AddConfigurationJson(this WebApplicationBuilder builder)
    {
        var externalConfigPath = Environment.GetEnvironmentVariable("EXTERNAL_CONFIG_PATH");

        builder.Configuration
            .AddJsonFile(Path.Combine(Directory.GetParent(builder.Environment.ContentRootPath)?.Parent?.FullName ?? builder.Environment.ContentRootPath,
                "src", "Core.Web", "GeneralSettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(Directory.GetParent(builder.Environment.ContentRootPath)?.FullName ?? builder.Environment.ContentRootPath,
                "GeneralSettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile("GeneralSettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(externalConfigPath ?? string.Empty, "GeneralSettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(externalConfigPath ?? string.Empty, builder.Environment.ApplicationName, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
    }

    public static void RunPort(this WebApplicationBuilder builder)
    {
        var port = builder.Configuration.GetConnectionString("Port");
        if (!string.IsNullOrEmpty(port))
        {
            builder.WebHost.UseUrls(port);
        }
    }

    public static void AddSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            var isDevelopment = context.HostingEnvironment.IsDevelopment();

            configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName);

            // Always write to Console
            configuration.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");

            if (isDevelopment)
            {
                // Development: File sink + Console
                var filePath = context.Configuration["Serilog:WriteTo:1:Args:path"] ?? "Logs/mylog-.log";
                configuration.WriteTo.File(
                    path: filePath,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 10485760,
                    retainedFileCountLimit: 31,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
            }
            else
            {
                // Production: Seq structured log search & monitoring (free self-hosted/developer license)
                var seqUrl = context.Configuration["SerilogProfiles:Production:SeqServerUrl"]
                             ?? context.Configuration["Serilog:Seq:ServerUrl"]
                             ?? context.Configuration["SEQ_URL"]
                             ?? "http://localhost:5341";

                var seqApiKey = context.Configuration["SerilogProfiles:Production:SeqApiKey"]
                                ?? context.Configuration["SEQ_API_KEY"];

                configuration.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrEmpty(seqApiKey) ? null : seqApiKey);

                // Production: Telegram sink for Fatal alert notifications
                var botToken = context.Configuration["SerilogProfiles:Production:TelegramBotToken"]
                               ?? context.Configuration["TELEGRAM_BOT_TOKEN"]
                               ?? context.Configuration["Telegram:BotToken"];

                var chatId = context.Configuration["SerilogProfiles:Production:TelegramChatId"]
                             ?? context.Configuration["TELEGRAM_CHAT_ID"]
                             ?? context.Configuration["Telegram:ChatId"];

                if (!string.IsNullOrWhiteSpace(botToken) &&
                    !string.IsNullOrWhiteSpace(chatId) &&
                    !botToken.Contains("YOUR_"))
                {
                    configuration.WriteTo.Telegram(
                        botToken: botToken,
                        chatId: chatId,
                        restrictedToMinimumLevel: LogEventLevel.Fatal);
                }
            }

            // Read any additional overrides or configurations from GeneralSettings.json
            configuration.ReadFrom.Configuration(context.Configuration);
        });
    }

    public static void AddUseCors(this WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policyBuilder =>
            {
                policyBuilder
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });
    }

    public static void ConfigureDefault(this WebApplicationBuilder builder)
    {
        builder.ConfigureDefault<CoreDbContext>();
    }

    public static void ConfigureDefault<TContext>(this WebApplicationBuilder builder)
        where TContext : CoreDbContext
    {
        builder.AddConfigurationJson();
        builder.RunPort();
        builder.AddSerilog();

        builder.Services.AddHttpContextAccessor();

        builder.Services.Configure<JwtOption>(builder.Configuration.GetSection("JwtOption"));
        builder.Services.AddScoped<IJwtTokenHandler, JwtTokenHandler>();

        StaticCache.SymmetricKey = builder.Configuration.GetConnectionString("SymmetricKey") ?? "";

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnectionString");
        if (!string.IsNullOrEmpty(connectionString))
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.EnableDynamicJson();
            var dataSource = dataSourceBuilder.Build();

            builder.Services.AddDbContext<TContext>((sp, options) =>
            {
                options.UseNpgsql(dataSource);
                options.UseLazyLoadingProxies();
                options.UseLoggerFactory(new SerilogLoggerFactory(Log.Logger));
            });

            if (typeof(TContext) != typeof(CoreDbContext))
            {
                builder.Services.AddScoped<CoreDbContext>(sp => sp.GetRequiredService<TContext>());
            }
        }

        builder.Services.AddScoped(typeof(GenericRepository<,>));
        builder.Services.AddScoped(typeof(GenericCrudService<,,>));
        builder.Services.AddScoped(typeof(BasicCrudService<,,>));
        builder.Services.AddAutoMapper(cfg => cfg.AddMaps(AppDomain.CurrentDomain.GetAssemblies()));

        builder.Services.AddMemoryCache();
        builder.Services.AddHealthChecks();
        builder.AddUseCors();

        // HttpClient factory and socket exhaustion prevention
        builder.Services.AddHttpClient();
        builder.Services.AddTransient<CorrelationIdDelegatingHandler>();

        // OpenTelemetry Tracing
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSource("Core.*"));

        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressModelStateInvalidFilter = true;
        });

        builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ModelValidationFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.WriteIndented = true;
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            var docNames = builder.Configuration.GetSection("Swagger:Docs").Get<List<string>>() ?? ["v1"];

            foreach (var docName in docNames)
                options.SwaggerDoc(docName, new OpenApiInfo { Title = AppDomain.CurrentDomain.FriendlyName });

            options.DocInclusionPredicate((docName, apiDesc) =>
            {
                var apiGroupAttribute = apiDesc.ActionDescriptor.EndpointMetadata
                    .OfType<ApiGroupAttribute>()
                    .FirstOrDefault();

                return apiGroupAttribute != null && apiGroupAttribute.GroupNames.Contains(docName);
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new List<string>()
                }
            });
        });

        builder.Services
            .AddAuthorization()
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptionsSection = builder.Configuration.GetSection("JwtOption");
                var secretKey = jwtOptionsSection["SecretKey"];

                if (!string.IsNullOrEmpty(secretKey))
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptionsSection["Issuer"],

                        ValidateAudience = true,
                        ValidAudience = jwtOptionsSection["Audience"],

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                }
            });

        var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        var environment = builder.Configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
            builder.Services.AddScoped<ICacheService, RedisCacheService>();
        }
        else if (environment is not null && !environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<ICacheService, RedisCacheService>();
        }
        else
        {
            builder.Services.AddScoped<ICacheService, MemoryCacheService>();
        }

        builder.Services.AddScoped<CorrelationIdMiddleware>();
        builder.Services.AddScoped<RequestResponseLoggingMiddleware>();
        builder.Services.AddScoped<GlobalExceptionHandlerMiddleware>();
    }

    public static async Task ConfigureDefault(this WebApplication app)
    {
        await app.ConfigureDefault<CoreDbContext>();
    }

    public static async Task ConfigureDefault<TContext>(this WebApplication app)
        where TContext : CoreDbContext
    {
        // Set HttpClientFactory on helper for static calls
        var httpClientFactory = app.Services.GetService<IHttpClientFactory>();
        if (httpClientFactory != null)
        {
            HttpClientHelpers.HttpClientFactory = httpClientFactory;
        }

        // 1. Exception handling must be first to catch any exceptions downstream
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

        // 2. Correlation ID for distributed tracing & request tracking
        app.UseMiddleware<CorrelationIdMiddleware>();

        // 3. Request / Response logging
        app.UseMiddleware<RequestResponseLoggingMiddleware>();

        // 4. Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var docNames = app.Configuration.GetSection("Swagger:Docs").Get<List<string>>() ?? ["v1"];

            foreach (var docName in docNames)
                options.SwaggerEndpoint($"/swagger/{docName}/swagger.json", $"{docName} API");
        });

        // 5. Health checks
        app.UseHealthChecks("/healths");

        // 6. CORS policy
        app.UseCors();

        // 7. Authentication & Authorization pipeline
        app.UseAuthentication();
        app.UseAuthorization();

        app.AppStartLog();
    }

    public static async Task UpMigration(this WebApplication app)
    {
        await app.UpMigration<CoreDbContext>();
    }

    public static async Task UpMigration<TContext>(this WebApplication app)
        where TContext : CoreDbContext
    {
        using var scope = app.Services.CreateScope();
        await using var dataContext = scope.ServiceProvider.GetService<TContext>();
        if (dataContext != null)
        {
            Log.Information("{0}", "Migrations applying...");
            await dataContext.Database.MigrateAsync();
            Log.Information("{0}", "Migrations applied.");
        }
    }

    public static void AppStartLog(this WebApplication app)
    {
        Log.Fatal("#{ApplicationName} \nApplication starting...", app.Environment.ApplicationName);
    }

    public static IServiceCollection AddCronJob<T>(this IServiceCollection services, string cronExpression)
        where T : class, ICronJob
    {
        var cron = CrontabSchedule.TryParse(cronExpression)
                   ?? throw new ArgumentException("Invalid cron expression", nameof(cronExpression));

        var entry = new CronRegistryEntry(typeof(T), cron);

        services.AddHostedService<CronScheduler>();
        services.TryAddTransient<T>();
        services.AddSingleton(entry);

        return services;
    }
}
