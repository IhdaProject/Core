# Core

Backend mikroservislari uchun umumiy, takrorlanuvchi mantiqlar va asosiy qatlamlarni o'z ichiga olgan Shared Core kutubxonasi.

## Loyiha arxitekturasi

Loyiha Clean / Layered Architecture tamoyillari asosida uchta asosiy modulga ajratilgan:

```
src/
├── Core.Domain/              # Asosiy biznes turlari, modellar va yordamchilar
│   ├── Common/               # ModelBase, AuditableModelBase, MultiLanguageField
│   ├── Configurations/       # JwtOption, MinIoConfiguration, CronRegistryEntry
│   ├── DTOs/                 # BaseDto
│   ├── Enums/                # UserPermissions, ErrorTypes, CustomClaimNames, SignMethods, TokenTypes
│   ├── Exceptions/           # ApiExceptionBase, NotFoundException, ValidationException, etc.
│   ├── Helpers/              # SerializerHelper, EncryptionHelper, PasswordHelper, HttpClientHelpers, RestClientHelpers
│   └── Models/               # Api modellari (ResponseModel, MetaQueryModel, etc.) va Auth modellari
│
├── Core.Infrastructure/      # Tashqi xizmatlar va ma'lumotlar bazasi bilan aloqa
│   ├── Caching/              # ICacheService, MemoryCacheService, RedisCacheService
│   ├── Data/                 # CoreDbContext, IRepositoryBase, RepositoryBase, GenericRepository, QueryableExtensions
│   ├── Security/             # IJwtTokenHandler, JwtTokenHandler
│   └── Storage/              # IMinioService, MinioService
│
└── Core.Web/                 # Veb qatlami, API kontrollerlar va ASP.NET Core konfiguratsiyalari
    ├── Attributes/           # ApiGroupAttribute, BasicAuthorizeAttribute, PermissionAuthorizeAttribute
    ├── Constants/            # Params, StaticCache
    ├── Controllers/          # ApiControllerBase, CrudGenericController
    ├── CronSchedulers/       # ICronJob, CronScheduler
    ├── Extensions/           # ConfigureApplication (builder.ConfigureDefault va app.ConfigureDefault), StringAndLongExtensions
    ├── Filters/              # BasicAuthenticationFilter, ModelValidationFilter, PermissionRequirementFilter, ExceptionFilter
    ├── Middlewares/          # GlobalExceptionHandlerMiddleware, RequestResponseLoggingMiddleware
    ├── Services/             # IBasicCrudService, BasicCrudService, GenericCrudService
    └── GeneralSettings.json  # Standart serilog va swagger konfiguratsiyalari
```

## Texnologiyalar
- **.NET 10.0**
- **Entity Framework Core 8 / Npgsql (PostgreSQL)**
- **StackExchange Redis & MemoryCache**
- **MinIO Client**
- **Serilog**
- **AutoMapper**
- **JWT Bearer Authentication**
- **Swashbuckle (Swagger/OpenAPI)**
