# Core — Shared Base Microservices Library (.NET 10)

Ushbu repozitoriy barcha alohida mikroservislar uchun **umumiy asos (Shared Base Library)** bo'lib, har bir servis repozitoriyasiga **Git Submodule** sifatida bog'lanadi.

Loyiha har bir servis alohida ma'lumotlar bazasiga (Database-per-Service) va alohida repozitoriyga ega bo'lgan taqsimlangan korporativ arxitektura uchun chuqur optimallashtirilgan.

---

## 📁 Loyiha Strukturasi

Loyiha Clean / Layered Architecture tamoyillari asosida 3 ta asosiy modulga ajratilgan:

```text
src/
├── Core.Domain/              # Asosiy biznes turlari, shartnomalar, modellar va interfeyslar
│   ├── Common/               # ModelBase, AuditableModelBase, MultiLanguageField
│   │   ├── Context/          # ITenantContext, ITenantEntity, NullTenantContext (Multi-Tenancy)
│   │   └── Lock/             # IDistributedLock (Taqsimlangan qulflash abstraksiyasi)
│   ├── Configurations/       # JwtOption, CronRegistryEntry
│   ├── Constants/            # AppHeaderNames, AppCacheKeys
│   ├── DTOs/                 # BaseDto
│   ├── Enums/                # UserPermissions, ErrorTypes, CustomClaimNames, SignMethods, TokenTypes
│   ├── Events/               # IEventBus, IntegrationEvent, IIntegrationEventHandler
│   ├── Exceptions/           # ApiExceptionBase, NotFoundException, ValidationException, etc.
│   ├── Helpers/              # EncryptionHelper, PasswordHelper, SerializerHelper, HttpClientHelpers
│   └── Models/               # Api modellari (ResponseModel, MetaQueryModel, MetaQueryFilterModel)
│
├── Core.Infrastructure/      # Tashqi xizmatlar, ma'lumotlar bazasi, kesh va xabar almashinuvi
│   ├── Caching/              # ICacheService, MemoryCacheService, RedisCacheService
│   │   └── Hybrid/           # IHybridCacheService, HybridCacheService (.NET 10 HybridCache)
│   ├── Data/
│   │   ├── Context/          # CoreDbContext (xmin Concurrency, Multi-Tenant Query Filter, Outbox)
│   │   ├── Extensions/       # QueryableExtensions (Parametrlashtirilgan Dynamic LINQ, ToReadOnlyListAsync)
│   │   └── Repositories/     # GenericRepository, RepositoryBase, IRepositoryBase
│   ├── Grpc/                 # Code-First gRPC (protobuf-net.Grpc)
│   │   ├── Extensions/       # AddCoreGrpcServer, AddCoreGrpcClient
│   │   └── Interceptors/     # GrpcClientContextInterceptor, GrpcServerContextInterceptor, GrpcExceptionInterceptor
│   ├── Lock/                 # RedisDistributedLock (PeriodicTimer Watchdog va Random Jitter)
│   ├── Messaging/            # MassTransit + RabbitMQ + PostgreSQL Transactional Outbox
│   │   ├── Consumers/        # BaseFaultConsumer (Poison message audit)
│   │   ├── Extensions/       # AddCoreMassTransit (Outbox, Exponential Retry, Kebab-case)
│   │   └── Filters/          # MassTransitTenantFilters (Multi-Tenancy avto-uzatish)
│   ├── Resilience/           # ResiliencePipelineExtensions, ResilienceDelegatingHandler
│   └── Security/             # IJwtTokenHandler, JwtTokenHandler
│
└── Core.Web/                 # HTTP veb qatlami, middleware'lar va DI konfiguratsiyalari
    ├── Attributes/           # ApiGroupAttribute, BasicAuthorizeAttribute, PermissionAuthorizeAttribute
    ├── Constants/            # Params, StaticCache
    ├── Controllers/          # ApiControllerBase, CrudGenericController
    ├── CronSchedulers/       # ICronJob, CronScheduler
    ├── Extensions/
    │   ├── ConfigureApplication.cs   # builder.ConfigureDefault va app.ConfigureDefault
    │   ├── WebCoreExtensions.cs      # Alohida xizmatlarni ulash (AddCoreHybridCache, AddCoreDistributedLock...)
    │   ├── RateLimitingExtensions.cs # Partitioned IP Rate Limiter
    │   └── CompressionExtensions.cs  # Brotli + Gzip Response Compression
    ├── Filters/              # ModelValidationFilter, PermissionRequirementFilter, ExceptionFilter
    ├── Middlewares/
    │   ├── CorrelationIdMiddleware.cs        # X-Correlation-Id propagatsiyasi
    │   ├── GlobalExceptionHandlerMiddleware.cs # Markaziy xatoliklarni ushlash
    │   ├── IdempotencyMiddleware.cs           # Fintech SHA-256 dublikat so'rovlarni bloklash
    │   ├── SessionValidationMiddleware.cs     # L1 + L2 sessiyani bekor qilish (Revocation)
    │   ├── SecurityHeadersMiddleware.cs       # HSTS, Anti-Clickjacking, Anti-XSS
    │   └── RequestResponseLoggingMiddleware.cs
    └── Services/             # IBasicCrudService, BasicCrudService, GenericCrudService
```

---

## 🚀 Yangi Qo'shilgan Imkoniyatlar va Ulardan Foydalanish

### 1. 🔒 Taqsimlangan Qulflash (Redis Distributed Lock + Watchdog)
Parallel serverlarda yoki mikroservis nusxalarida bitta resursni (masalan: to'lov yoki buyurtma) bir vaqtda o'zgartirib qo'yishning oldini oladi. Uzoq davom etadigan operatsiyalarda fonda `PeriodicTimer` orqali qulf muddatini avtomatik uzaytirib turadi (Watchdog).

```csharp
// DI ro'yxatdan o'tkazish:
builder.Services.AddCoreDistributedLock(builder.Configuration.GetConnectionString("Redis"));

// Xizmat ichida ishlatish:
public class PaymentService(IDistributedLock distributedLock)
{
    public async Task ProcessPaymentAsync(long orderId, CancellationToken ct)
    {
        await using var lockHandle = await distributedLock.TryAcquireLockAsync(
            resourceKey: $"order:payment:{orderId}",
            timeout: TimeSpan.FromSeconds(5),
            expiry: TimeSpan.FromSeconds(30),
            ct);

        if (lockHandle is null)
        {
            throw new ConflictException("Ushbu buyurtma to'lovi ayni paytda boshqa oqimda amalga oshirilmoqda!");
        }

        // Qulf olindi! Tranzaksiya xavfsiz bajariladi.
        // Blok tugashi bilan qulf avtomatik bo'shatiladi.
    }
}
```

---

### 2. ⚡ .NET 10 `HybridCache` (L1 In-Memory + L2 Redis)
L1 xotira (nanosoniyalarda) va L2 Redis keshini yagona xizmatda birlashtiradi. **Lock-on-Read (Single-Flight)** orqali Cache Stampede (bir vaqtda yuzlab so'rovlar bazaga borishi) muammosini yo'qotadi. Bog'liq ma'lumotlarni teglar (`RemoveByTagAsync`) orqali bir zumda guruhlab tozalash mumkin.

```csharp
// DI ro'yxatdan o'tkazish:
builder.Services.AddCoreHybridCache(builder.Configuration.GetConnectionString("Redis"));

// Xizmat ichida ishlatish:
public class ProductService(IHybridCacheService cache)
{
    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            key: $"product:{id}",
            factory: async token => await dbContext.Products.FindAsync([id], token),
            localTtl: TimeSpan.FromSeconds(30),  // L1 In-Memory
            remoteTtl: TimeSpan.FromMinutes(10), // L2 Redis
            tags: ["products"],
            cancellationToken: ct);
    }

    public async Task UpdateAsync(ProductDto dto, CancellationToken ct)
    {
        await dbContext.SaveChangesAsync(ct);
        // Barcha mahsulotlar keshini teg bo'yicha tozalash:
        await cache.RemoveByTagAsync("products", ct);
    }
}
```

---

### 3. 🛡️ Fintech HTTP Idempotensiya Middleware (`X-Idempotency-Key`)
POST va PUT so'rovlarida mijoz tugmani qayta bossa yoki tarmoq qayta yuborsa, dublikat amallarning oldini oladi. So'rov tanasining `SHA-256` xeshini tekshirib, bitta kalitdan boshqa parametrlar bilan foydalanishga yo'l qo'ymaydi (`409 Conflict`).

```csharp
// Program.cs da ConfigureDefault() ichida Redis bo'lsa avtomatik faollashadi,
// yoki qo'lda ulash uchun:
builder.Services.AddCoreIdempotency();
app.UseCoreIdempotency();

// Mijoz so'rov sarlavhasi (Header):
// POST /api/v1/orders
// X-Idempotency-Key: e4b29b68-6c84-48f1-8f55-23fba93b79da
```

---

### 4. 🚪 Sessiya va Tokenni Bekor Qilish (Session Revocation Middleware)
Foydalanuvchi "Logout" qilganda yoki parolini o'zgartirganda uning JWT tokeni muddati tugashini kutmasdan darhol bekor qilish imkoniyati. Har bir so'rovda Redis'ga ortiqcha yuk tushmasligi uchun L1 In-Memory keshda 20 soniya keshlanadi.

```csharp
// Program.cs da:
builder.Services.AddCoreSessionValidation();
app.UseCoreSessionValidation(); // UseAuthentication() dan keyin turadi
```

---

### 5. 🗄️ PostgreSQL & EF Core 10 Optimallashtirishlari
`CoreDbContext` va `AddCoreNpgsqlDataSource` orqali:
- **`xmin` Optimistik Qulflash:** Entitiylarga qo'shimcha `RowVersion` ustunisiz, PostgreSQL tizim ustuni `xmin` orqali optimistik qulflash avtomatik yoqilgan.
- **Statement Auto-Prepare:** `MaxAutoPrepare = 50`, `AutoPrepareMinUsages = 2`, `MinPoolSize = 10`, `KeepAlive = 30s` orqali SQL parsing va execution plan rejalashtirish xarajatlari serverda keshlanadi.
- **Parametrlashtirilgan Dynamic LINQ (`QueryableExtensions`):** Closure o'rami orqali dinamik `FilterByExpressions` SQL parametrlari (`@p0`) shaklida yuboriladi va EF Core `CompiledQueryCache` ni 100% qayta ishlatadi.
- **Read-Only so'rovlar:** `query.ToReadOnlyListAsync(ct)` orqali Change Tracker yuklamasini kamaytirish.

---

### 6. 🏢 Ko'p Ijarachilik (Multi-Tenancy)
Agar servis SaaS yoki ko'p tashkilotli bo'lsa, entitiyga `ITenantEntity` interfeysini berasiz:

```csharp
public class Order : ModelBase<long>, ITenantEntity
{
    public long? TenantId { get; set; }
    public decimal TotalAmount { get; set; }
}
```
- `CoreDbContext` avtomatik ravishda faqat joriy ijarachi ma'lumotlarini filtrlaydi (`TenantId == currentTenantId`).
- `(TenantId, IsDelete)` kompozit B-Tree indeksini avtomatik yaratadi (PostgreSQL Sequential Scan muammosi yo'qoladi).
- Vaqtincha ijarachini almashtirish: `using (tenantContext.Change(otherTenantId)) { ... }`
- Filtrni hisobotlar uchun vaqtincha ochish: `using (tenantContext.DisableTenantFilter()) { ... }`

---

### 7. 📬 MassTransit + PostgreSQL Transactional Outbox (RabbitMQ)
Xabarlar baza tranzaksiyasi bilan bitta atomik PostgreSQL tranzaksiyasida saqlanadi. Tarmoq uzilsa ham xabar yo'qolmaydi va baza commit bo'lgandagina yuboriladi:

```csharp
// Program.cs da ro'yxatga olish:
builder.Services.AddCoreMassTransit<MyDbContext>(builder.Configuration, bus =>
{
    bus.AddConsumer<OrderCreatedConsumer>();
});

// Servisda xabar chiqarish (Publish):
await publishEndpoint.Publish(new OrderCreatedIntegrationEvent { OrderId = order.Id }, ct);
await dbContext.SaveChangesAsync(ct); // Baza va Outbox bitta tranzaksiyada saqlanadi!
```

---

### 8. ⚡ Servislararo Aloqa: Code-First gRPC (`protobuf-net.Grpc`)
Mikroservislararo aloqani sekin HTTP/REST o'rniga tezyurar binary Code-First gRPC orqali amalga oshirish:

```csharp
// 1. Shartnoma interfeysi (Contracts kutubxonasida):
[ServiceContract]
public interface IBillingGrpcService
{
    [OperationContract]
    Task<BillingDto> GetBillingAsync(GetBillingRequest request, CallContext context = default);
}

// 2. Server servisda ro'yxatdan o'tkazish:
builder.Services.AddCoreGrpcServer();
app.MapGrpcService<BillingGrpcService>();

// 3. Chaqiruvchi servisda mijozni ulash:
builder.Services.AddCoreGrpcClient<IBillingGrpcService>("http://billing-service:5001");
```
> `CorrelationId`, `TenantId`, `UserId` va **10 soniyalik majburiy Deadline** hamda `RequestAborted` avtomatik uzatiladi.

---

### 9. 🚦 Rate Limiting va Korporativ Xavfsizlik Sarlavhalari
- **IP Rate Limiter:** `AddCoreRateLimiting()` har bir mijoz IP bo'yicha alohida darcha (100 req/s sliding window) ochadi.
- **Security Headers:** Har bir javobga `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Strict-Transport-Security` sarlavhalari avtomatik beriladi.
- **Compression:** Brotli (Fastest) + Gzip orqali faqat matn va JSON siqiladi (`AddCoreCompression()`).

---

### 10. 🔑 Taqsimlangan Ruxsatlar va Avto-Sinxronizatsiya (Decentralized Permissions)
Har bir servis o'zining ruxsatlariga o'zi egalik qiladi. `Core` dagi umumiy enumga qaramlik yo'q!

#### 1. Mikroservis o'zining lokal ruxsatlar enum'ini yaratadi:
```csharp
[PermissionsModule("QuranCourse", serviceCode: 60, moduleName: "Quran Training")]
public enum QuranPermissions
{
    [Description("Yangi o'quv markazi yaratish")]
    CreateTrainingCenter = 60100001,

    [Description("O'quv markazlarini ko'rish")]
    ViewTrainingCenters  = 60100002
}
```

#### 2. Kontrollerda o'zining lokal enumi orqali tekshiradi:
```csharp
[PermissionAuthorize(QuranPermissions.CreateTrainingCenter)]
[HttpPost]
public async Task<IActionResult> CreateCenter() { ... }
```
*(Eski `[PermissionAuthorize(UserPermissions.LogOut)]` va raqamli `[PermissionAuthorize(60100001)]` kodlari ham 100% qo'llab-quvvatlanadi).*

#### 3. Mikroservis Program.cs da avtomatik sinxronlashni yoqish:
Servis ishga tushishi bilan barcha ruxsatlar RabbitMQ (yoki gRPC) orqali `AuthApi` ga avtomatik yuboriladi:
```csharp
builder.Services.AddCorePermissionAutoSync<QuranPermissions>();
```

#### 4. AuthApi tomonida qabul qilib bazaga saqlash:
```csharp
// 1. IPermissionStorageHandler ni implement qilish (EF Core orqali DB ga Upsert qilish):
public class AuthPermissionStorageHandler : IPermissionStorageHandler
{
    public async Task<int> HandlePermissionsReceivedAsync(string serviceName, int? serviceCode, IReadOnlyList<PermissionDefinition> permissions, CancellationToken ct)
    {
        // Bazaga yangi ruxsatlarni qo'shish yoki yangilash
        return permissions.Count;
    }
}

// 2. Program.cs da ro'yxatdan o'tkazish:
builder.Services.AddCorePermissionReceiver<AuthPermissionStorageHandler>();
```

---

## 🧩 Tezkor Boshlash (`Program.cs`)

Barcha standart sozlamalar bitta chaqiruv bilan ishga tushadi:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Barcha standartlarni (DbContext, Logging, OpenTelemetry, Redis, DistributedLock, HybridCache) faollashtirish:
builder.ConfigureDefault<MyDbContext>();

var app = builder.Build();

// Pipeline (SecurityHeaders, GlobalException, CorrelationId, Idempotency, SessionValidation, Swagger):
await app.ConfigureDefault<MyDbContext>();

app.MapControllers();
app.Run();
```

---

## 🧪 Sinovlarni Ishga Tushirish (Unit Tests)

```bash
dotnet test Core.sln
```
Barcha 12 ta unit testlar muvaffaqiyatli o'tadi.
