using System.Text;
using System.Text.Json.Serialization;
using Core.Domain.Configurations;
using Core.Domain.Enums;
using Core.Domain.Models.Auth;
using Core.Infrastructure.Caching;
using Core.Infrastructure.Data.Context;
using Core.Infrastructure.Data.Repositories;
using Core.Infrastructure.Security;
using Core.Infrastructure.Storage;
using Core.Web.Attributes;
using Core.Web.Constants;
using Core.Web.CronSchedulers;
using Core.Web.Filters;
using Core.Web.Middlewares;
using Core.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NCrontab;
using Npgsql;
using Serilog;
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
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration));
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

    public static void AddMinIoService(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<MinIoConfiguration>(builder.Configuration.GetSection("Minio"));

        builder.Services.PostConfigure<MinIoConfiguration>(options =>
        {
            if (string.IsNullOrEmpty(options.BucketName))
            {
                options.BucketName = "cloudapi";
            }
        });

        builder.Services.AddSingleton<IMinioService, MinioService>();
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
        builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

        builder.Services.AddMemoryCache();
        builder.AddMinIoService();
        builder.Services.AddHealthChecks();
        builder.AddUseCors();

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
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var docNames = app.Configuration.GetSection("Swagger:Docs").Get<List<string>>() ?? ["v1"];

            foreach (var docName in docNames)
                options.SwaggerEndpoint($"/swagger/{docName}/swagger.json", $"{docName} API");
        });

        app.UseHealthChecks("/healths");

        app.UseMiddleware<RequestResponseLoggingMiddleware>();
        app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

        await app.SynchronizePermissions<TContext>();
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

    public static async Task SynchronizePermissions(this WebApplication app)
    {
        await app.SynchronizePermissions<CoreDbContext>();
    }

    public static async Task SynchronizePermissions<TContext>(this WebApplication app)
        where TContext : CoreDbContext
    {
        try
        {
            Log.Information("Permissions synchronization starting....");
            using var scope = app.Services.CreateScope();
            await using var dataContext = scope.ServiceProvider.GetService<TContext>();

            if (dataContext == null)
            {
                Log.Warning("DataContext not found for permission synchronization, skipping.");
                return;
            }

            var permissionCodes = typeof(UserPermissions).GetEnumValues().Cast<object>();
            var enumerable = permissionCodes as object[] ?? permissionCodes.ToArray();

            foreach (var permissionCode in enumerable)
            {
                var codeVal = (int)permissionCode;
                var storedCode = await dataContext.Permissions.FirstOrDefaultAsync(x => x.Code == codeVal);
                if (storedCode is not null) continue;

                await dataContext.Permissions.AddAsync(new Permission
                {
                    Code = codeVal,
                    Name = permissionCode.ToString() ?? string.Empty
                });
            }

            await dataContext.SaveChangesAsync();

            var codes = enumerable.Cast<int>().ToList();

            var removedCodes = await dataContext
                .Permissions
                .Where(x => !codes.Contains(x.Code))
                .ToListAsync();

            if (removedCodes.Count > 0)
            {
                dataContext.Permissions.RemoveRange(removedCodes);
                await dataContext.SaveChangesAsync();
            }

            #region default Structure data
            if (!await dataContext.Structures.AnyAsync(s => s.Type == 1))
            {
                int[] defaultPermission = [(int)UserPermissions.LogOut, (int)UserPermissions.ViewProfile, (int)UserPermissions.ViewMyPermissions];

                await dataContext.Structures.AddAsync(new Structure
                {
                    Id = 1,
                    Type = 1,
                    Name = "Default",
                    StructurePermissions = dataContext.Permissions
                        .Where(p => defaultPermission.Contains(p.Code))
                        .Select(p => new StructurePermission
                        {
                            PermissionId = p.Id
                        })
                        .ToList()
                });

                await dataContext.SaveChangesAsync();
            }
            #endregion

            #region Super Admin Structure data
            if (!await dataContext.Structures.AnyAsync(s => s.Type == 2))
            {
                await dataContext.Structures.AddAsync(new Structure
                {
                    Id = 2,
                    Type = 2,
                    Name = "Super Admin",
                    StructurePermissions = dataContext.Permissions
                        .Select(p => new StructurePermission
                        {
                            PermissionId = p.Id
                        })
                        .ToList()
                });

                await dataContext.SaveChangesAsync();
            }
            #endregion

            StaticCache.Permissions = await dataContext.Structures
                .Select(s => new { s.Id, permissionCodes = s.StructurePermissions.Select(sp => sp.Permission.Code) })
                .ToDictionaryAsync(s => s.Id, s => s.permissionCodes.ToList());

            Log.Information("Permissions synchronization finished successfully.");
        }
        catch (Exception e)
        {
            Log.Error(e, "Permissions synchronization crashed.");
        }
    }

    public static IServiceCollection AddCronJob<T>(this IServiceCollection services, string cronExpression)
        where T : class, ICronJob
    {
        var cron = CrontabSchedule.TryParse(cronExpression)
                   ?? throw new ArgumentException("Invalid cron expression", nameof(cronExpression));

        var entry = new CronRegistryEntry(typeof(T), cron);

        services.AddHostedService<CronScheduler>();
        services.TryAddSingleton<T>();
        services.AddSingleton(entry);

        return services;
    }
}
