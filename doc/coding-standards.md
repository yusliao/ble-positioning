# 编码规范 — BLE 室内定位系统

> 版本：1.3 | 适用范围：全项目所有 .NET / Blazor / MAUI 代码
> AI 生成代码必须符合本规范，Code Review 以此为标准。

**API HTTP 契约**（成功信封、ProblemDetails、`/health`、`X-Trace-Id`、RSSI **deviceId** 与 Key 一致性）：以 **`doc/api-conventions.md`** 为权威；本文件不重复定义。

---

## 1. 命名约定

### C# 通用规则

| 类型 | 规范 | 示例 |
|------|------|------|
| 类、接口、枚举 | PascalCase | `PositioningEngine`, `IBeaconRepository` |
| 方法 | PascalCase | `CalculatePosition()` |
| 属性 | PascalCase | `LastKnownPosition` |
| 私有字段 | `_camelCase` | `_redisClient`, `_logger` |
| 局部变量、参数 | camelCase | `beaconSignal`, `floorId` |
| 常量 | PascalCase | `DefaultTxPower`, `MaxBeaconCount` |
| 异步方法 | 以 `Async` 结尾 | `GetFloorByIdAsync()` |
| 接口 | 以 `I` 前缀 | `IPositioningService` |
| 泛型参数 | `T` 或 `T描述` | `TEntity`, `TResult` |

### 项目特定命名

**轻量 CQS（默认）**：按模块的应用服务 + 明确 Async 方法名；DTO / 领域事件命名不变。

```csharp
// 应用服务（推荐默认形态，与 design-spec.md ADR-001 一致）
public interface IFloorService
{
    Task<FloorDto?> GetByIdAsync(Guid floorId, CancellationToken ct = default);
    Task<IReadOnlyList<FloorListItemDto>> ListAsync(CancellationToken ct = default);
    Task<Result<Guid>> CreateAsync(CreateFloorRequest request, CancellationToken ct = default);
}

// 可选：复杂用例再引入显式 Query/Command + Handler（不强制全项目铺开）
public record GetFloorByIdQuery(Guid FloorId) : IQuery<FloorDto>;
public class GetFloorByIdQueryHandler : IQueryHandler<GetFloorByIdQuery, FloorDto> { }

// 仓储
public interface IFloorRepository : IRepository<Floor> { }
public class FloorRepository : IFloorRepository { }

// DTO 后缀
public record FloorDto(Guid Id, string Name, double WidthMeters, double HeightMeters);
public record BeaconSignalDto(string Uuid, int Major, int Minor, int Rssi);

// 领域事件
public record PositionCalculatedDomainEvent(Guid DeviceId, Position Position) : IDomainEvent;
public record DeviceEnteredZoneDomainEvent(Guid DeviceId, Guid ZoneId) : IDomainEvent;
```

---

## 2. 文件与目录结构

### Application 层结构

**默认（轻量 CQS）**：按特性文件夹 + `*Service.cs` / `I*Service.cs`；测试置于 `tests/.../Application` 镜像目录或 `Floors/FloorServiceTests.cs`。

```
Application/
├── Floors/
│   ├── IFloorService.cs
│   ├── FloorService.cs
│   └── Models/                    ← 该模块专用请求/响应 DTO（可选）
├── Devices/
│   ├── IDeviceService.cs
│   └── DeviceService.cs
├── Positioning/
│   └── （可选）与围栏/查询相关的应用服务；RSSI 热路径在 API/Infrastructure（ADR-002）
└── Common/
    ├── Interfaces/
    │   ├── IRepository.cs
    │   ├── IUnitOfWork.cs
    │   ├── ITrajectoryBulkWriter.cs   ← 轨迹批量落库端口（实现位于 Infrastructure）
    │   ├── IQuery.cs                  ← 可选：仅当某用例采用 Handler 时使用
    │   └── ICommand.cs
    └── Models/
        ├── Result.cs
        └── ApiResponse.cs
```

**可选（单用例一文件夹）**：对规则复杂或变更频繁的模块，可局部保留 `GetFloorById/GetFloorByIdQuery.cs` + `Handler` 结构；不必全项目统一为 Handler 风格。

### Infrastructure 层结构

```
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/       ← 每实体一个 Configuration 文件
│   ├── Repositories/
│   ├── BulkWriters/          ← 默认 NpgsqlTrajectoryBulkWriter；可选 SqlServerTrajectoryBulkWriter（当前不启用）
│   │   ├── NpgsqlTrajectoryBulkWriter.cs
│   │   └── SqlServerTrajectoryBulkWriter.cs
│   └── Migrations/
├── Caching/
│   ├── RedisPositionCache.cs
│   └── RedisCacheService.cs
├── Messaging/
│   ├── Consumers/
│   │   └── RssiReportConsumer.cs
│   └── Publishers/
├── Positioning/
│   ├── PathLossCalculator.cs
│   ├── TrilaterationEngine.cs
│   ├── KalmanFilter2D.cs
│   └── PositioningPipeline.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs   ← DI 注册入口
```

---

## 3. 依赖注入规范

```csharp
// Infrastructure/Extensions/ServiceCollectionExtensions.cs
// 所有 Infrastructure 层服务在此注册，API 层只调用此扩展方法

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddPersistence(configuration)
            .AddCaching(configuration)
            .AddMessaging(configuration)
            .AddPositioningServices();

        return services;
    }

    private static IServiceCollection AddPositioningServices(this IServiceCollection services)
    {
        services.AddScoped<IPathLossCalculator, PathLossCalculator>();
        services.AddScoped<ITrilaterationEngine, TrilaterationEngine>();
        // 卡尔曼 Singleton：进程内状态适用于单实例；多实例须外置状态到 Redis，见 design-spec ADR-008
        services.AddSingleton<KalmanFilter2D>();
        services.AddHostedService<PositioningPipelineService>();
        return services;
    }
}

// 生命周期规则：
// Singleton  → 无状态工具类、缓存管理器、卡尔曼滤波状态管理器
// Scoped     → DbContext、Repository、应用服务（IFloorService 等）；若存在则含 Query/Command Handler
// Transient  → 轻量无状态服务
```

---

## 4. 异步编程规范

```csharp
// ✅ 正确：所有 I/O 操作必须异步
public async Task<FloorDto?> GetFloorByIdAsync(Guid id, CancellationToken ct = default)
{
    var floor = await _repository.GetByIdAsync(id, ct);
    return floor is null ? null : _mapper.Map<FloorDto>(floor);
}

// ✅ 正确：ConfigureAwait 在 Library 层使用 false
public async Task<double> CalculateDistanceAsync(...)
{
    await Task.Delay(0).ConfigureAwait(false);
    // ...
}

// ❌ 错误：同步阻塞（可能死锁）
var floor = _repository.GetByIdAsync(id).Result;  // 禁止

// ✅ 正确：Channel 用于 RSSI 高频数据异步处理
private readonly Channel<RssiReport> _channel =
    Channel.CreateBounded<RssiReport>(new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,  // 满时丢弃最旧数据
        SingleReader = true
    });

// ✅ 正确：CancellationToken 向下传递
public async Task ProcessAsync(CancellationToken stoppingToken)
{
    await foreach (var report in _channel.Reader.ReadAllAsync(stoppingToken))
    {
        await HandleReportAsync(report, stoppingToken);
    }
}
```

---

## 5. EF Core 使用规范

```csharp
// ✅ 正确：查询时明确 AsNoTracking（只读操作）
var beacons = await _context.Beacons
    .AsNoTracking()
    .Where(b => b.FloorId == floorId && b.Status == BeaconStatus.Active)
    .OrderBy(b => b.Minor)
    .ToListAsync(ct);

// ✅ 正确：Include 明确关联，避免 N+1
var floor = await _context.Floors
    .Include(f => f.Beacons.Where(b => b.Status == BeaconStatus.Active))
    .FirstOrDefaultAsync(f => f.Id == floorId, ct);

// ✅ 正确：高吞吐轨迹写入经 ITrajectoryBulkWriter（默认 Npgsql COPY），禁止在热路径逐条 SaveChanges
await _trajectoryBulkWriter.WriteBatchAsync(rows, ct);

// ⚠️ 仅测试或极低频场景：单条 ExecuteSqlRawAsync 可接受；不得用作定位管道主路径

// ❌ 错误：在循环中执行数据库查询（N+1）
foreach (var deviceId in deviceIds)
{
    var pos = await _context.PositionLogs  // 禁止，应一次性批量查询
        .Where(p => p.DeviceId == deviceId)
        .FirstOrDefaultAsync();
}

// ✅ 正确：软删除（通过全局查询过滤器）
// 实体继承 ISoftDelete，AppDbContext 统一配置过滤器
modelBuilder.Entity<Beacon>().HasQueryFilter(b => !b.IsDeleted);
```

### 5.1 轨迹批量写入端口（`ITrajectoryBulkWriter`）

- **接口定义**放在 `Application/Common/Interfaces/ITrajectoryBulkWriter.cs`，**实现**放在 `Infrastructure/Persistence/BulkWriters/`。按 **ADR-005**，`AddInfrastructure` **默认注册** `NpgsqlTrajectoryBulkWriter`；仅在明确需要时再注册 `SqlServerTrajectoryBulkWriter`（当前文档不启用）。
- **行模型**与数据库方言无关，例如：

```csharp
public readonly record struct PositionLogRow(
    Guid DeviceId,
    Guid FloorId,
    double X,
    double Y,
    double Accuracy,
    DateTime TimestampUtc);

public interface ITrajectoryBulkWriter
{
    Task WriteBatchAsync(IReadOnlyList<PositionLogRow> rows, CancellationToken ct = default);
}
```

- **实现要点**：
  - **PostgreSQL（默认）**：`NpgsqlConnection.BeginBinaryImport` / `COPY` 至分区父表 `position_logs`；批大小（如 500～5000）与 `MaxPoolSize`、事务时长联调。
  - **SQL Server（可选，当前不采用）**：`SqlBulkCopy` 或 TVP；仅当 ADR 变更时实现。
  - 管道侧可先 **内存/Channel 聚合** 再调用 `WriteBatchAsync`，失败时 **结构化日志 + 指标**（`ble_positioning_failures_total`），可选死信队列（与 `design-spec.md` 监控节一致）。
- **`COPY` 列列表**：`NpgsqlTrajectoryBulkWriter` 中 `COPY position_logs (...)` 的**显式列名与顺序**须与 **`design-spec.md` §2.4.2** 中 `position_logs` 及迁移脚本一致；通常包含 `device_id`、`floor_id`、`x`、`y`、`accuracy`、`timestamp`；若不在 `COPY` 中提供 `id`，则依赖 **`bigserial`** 默认填充。代码评审以 DDL/迁移为单一事实来源。

### 5.2 PostgreSQL 物理命名与 EF Core 映射（ADR-007）

- **表名、列名**：与 **`design-spec.md` §2.4.2** 一致，使用 **snake_case**（如 `beacons`、`floor_id`、`is_deleted`、`created_at`）。
- **推荐做法**：在 `AppDbContext.OnConfiguring` 或 `AddDbContext` 链式调用 **`UseSnakeCaseNamingConvention()`**（`EFCore.NamingConventions` 包），使 CLR `FloorId` 自动映射为 `floor_id`；或对少数例外使用 `HasColumnName`。
- **禁止**：将物理表命名为与 §2.4.2 不一致的 PascalCase（如 `Beacons`），以免迁移、`COPY` 与运维脚本分叉。
- **分区表 `position_logs`**：EF 仅用于 **只读查询**（历史轨迹）；映射到分区父表名 `position_logs`；写入仍仅经 **`ITrajectoryBulkWriter`**。

---

## 6. Redis 操作规范

```csharp
// ✅ 正确：操作前检查连接可用性，失败降级而非抛出
public async Task<Position?> GetLatestPositionAsync(Guid deviceId)
{
    try
    {
        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync($"pos:{deviceId}");
        return json.IsNull ? null : JsonSerializer.Deserialize<Position>(json!);
    }
    catch (RedisConnectionException ex)
    {
        _logger.LogWarning(ex, "Redis unavailable, falling back to database. DeviceId={DeviceId}", deviceId);
        return await _positionRepository.GetLatestAsync(deviceId);  // 降级到数据库
    }
}

// ✅ 正确：写入时设置 TTL
await db.StringSetAsync(
    key: $"pos:{deviceId}",
    value: JsonSerializer.Serialize(position),
    expiry: TimeSpan.FromSeconds(60));

// ✅ 正确：使用 Lua 脚本保证原子性（如需要）
// ❌ 错误：在 Redis 中存储原始 RSSI 数据（只存最终坐标）
```

---

## 7. SignalR 规范

```csharp
// Hub 只做路由，不包含业务逻辑
[Authorize]
public class PositioningHub : Hub
{
    private readonly IPositioningNotificationService _notificationService;

    public async Task JoinFloor(Guid floorId)
    {
        // 验证用户有权限查看该楼层
        await Groups.AddToGroupAsync(Context.ConnectionId, $"floor:{floorId}");
        _logger.LogDebug("Client {ConnectionId} joined floor {FloorId}", Context.ConnectionId, floorId);
    }

    public async Task LeaveFloor(Guid floorId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"floor:{floorId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // 清理订阅资源
        await base.OnDisconnectedAsync(exception);
    }
}

// 从后台服务推送位置更新（不在 Hub 内部执行定位计算）
public class PositioningNotificationService : IPositioningNotificationService
{
    private readonly IHubContext<PositioningHub> _hubContext;

    public async Task NotifyPositionUpdatedAsync(
        Guid floorId, Guid deviceId, Position position, CancellationToken ct)
    {
        await _hubContext.Clients
            .Group($"floor:{floorId}")
            .SendAsync("PositionUpdated",
                deviceId, position.X, position.Y, position.Accuracy,
                cancellationToken: ct);
    }
}
```

---

## 8. MAUI BLE 扫描规范

```csharp
// BLE 扫描回调中禁止执行网络请求，必须先入本地队列
public class BleScanner : IBleScanner
{
    private readonly Channel<RssiReport> _localQueue =
        Channel.CreateBounded<RssiReport>(500);

    // 扫描回调（UI线程或BLE线程，不可阻塞）
    private void OnAdvertisementReceived(BleAdvertisementReceivedEventArgs e)
    {
        var signal = ParseAdvertisement(e);
        if (signal is null) return;

        // 非阻塞写入本地队列
        _localQueue.Writer.TryWrite(signal);
    }

    // 后台聚合任务（每 300ms 批量上报）
    private async Task AggregateAndReportAsync(CancellationToken ct)
    {
        var window = new Dictionary<string, BeaconSignal>();
        var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));

        while (await timer.WaitForNextTickAsync(ct))
        {
            // 读取队列中所有积累数据
            while (_localQueue.Reader.TryRead(out var signal))
                window[signal.Key] = signal; // 保留最新值

            if (window.Count >= 1)
            {
                await _apiClient.ReportRssiAsync(window.Values.ToList(), ct);
                window.Clear();
            }
        }
    }
}
```

---

## 9. Blazor 组件规范

```razor
@* ✅ 正确：组件文件命名 PascalCase，与类名一致 *@
@* FloorMapViewer.razor *@

@inject IPositioningHubClient HubClient
@inject ILogger<FloorMapViewer> Logger
@implements IAsyncDisposable

<div class="floor-map-container">
    @* UI 内容 *@
</div>

@code {
    [Parameter, EditorRequired] public Guid FloorId { get; set; }
    [Parameter] public EventCallback<DevicePosition> OnDeviceSelected { get; set; }

    // 私有状态
    private List<DevicePosition> _positions = [];
    private bool _isConnected;

    protected override async Task OnInitializedAsync()
    {
        await HubClient.JoinFloorAsync(FloorId);
        HubClient.OnPositionUpdated += HandlePositionUpdate;
    }

    // 事件处理：InvokeAsync 确保回到 Blazor 同步上下文
    private async void HandlePositionUpdate(DevicePosition pos)
    {
        await InvokeAsync(() =>
        {
            _positions = UpdatePositions(_positions, pos);
            StateHasChanged();
        });
    }

    public async ValueTask DisposeAsync()
    {
        HubClient.OnPositionUpdated -= HandlePositionUpdate;
        await HubClient.LeaveFloorAsync(FloorId);
    }
}
```

---

## 10. 配置管理规范

```json
// appsettings.json 结构（所有业务参数必须可配置）
{
  "Positioning": {
    "DefaultTxPower": -59,
    "PathLossExponent": 2.0,
    "MinBeaconsRequired": 3,
    "RssiAggregationWindowMs": 300,
    "KalmanProcessNoise": 0.1,
    "KalmanMeasurementNoise": 2.0,
    "DeviceOfflineThresholdSeconds": 30,
    "PositionUpdateIntervalMs": 500
  },
  "Redis": {
    "ConnectionString": "",
    "PositionTtlSeconds": 60,
    "DeviceSetTtlSeconds": 120
  }
}
```

```csharp
// 强类型配置绑定
public class PositioningOptions
{
    public const string SectionName = "Positioning";

    public int DefaultTxPower { get; init; } = -59;
    public double PathLossExponent { get; init; } = 2.0;
    public int MinBeaconsRequired { get; init; } = 3;
    public int RssiAggregationWindowMs { get; init; } = 300;
    // ...
}

// DI 注册（带数据注解验证）
services.AddOptions<PositioningOptions>()
    .BindConfiguration(PositioningOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

## 11. 性能关键路径规范

以下代码路径在高并发下被频繁调用，AI 生成相关代码时须特别注意：

**RSSI 上报接口（POST /api/v1/rssi/report）**
- 目标：< 5ms 响应时间（不含定位计算）
- 必须：写入 Channel 后立即返回 202，不等待计算结果
- 禁止：同步等待数据库写入

**定位计算管道**
- 目标：< 100ms 完成一次定位（含卡尔曼滤波）
- 必须：使用 `Span<T>` 和值类型避免 GC 压力
- 推荐：关键数学计算使用 `System.Numerics.Vector2`

**SignalR 位置推送**
- 目标：< 50ms 延迟（计算完成到客户端收到）
- 必须：推送时使用 Group 广播（不遍历连接）
- 禁止：在推送循环中 await 数据库操作

---

## 12. 安全规范

**RSSI 设备认证（与 `design-spec.md` ADR-006、§3.1、§4.1 一致）**

- 客户端必须发送 HTTP 头：**`X-Api-Key: {apiKey}`**（**不得**依赖 `Authorization: ApiKey …`）。
- 注册认证方案：**`AddAuthentication().AddScheme<ApiKeyAuthenticationHandler>("ApiKey", …)`**，方案名称为字符串 **`"ApiKey"`**。
- `ApiKeyAuthenticationHandler` 从 **`Request.Headers["X-Api-Key"]`** 读取，校验成功则 `AuthenticateResult.Success`，Claims 中可含 `device_id` 等（与业务一致即可）。
- OpenAPI：为该端点声明 **ApiKey** 安全方案，`Name = "X-Api-Key"`，`In = ParameterLocation.Header`。
- **RSSI**：校验 Key 后，**请求体 `deviceId` 必须与 Key 绑定设备 `id` 一致**，否则 **403**（`api-conventions.md` §6）。

```csharp
// 所有 API 端点默认要求认证
[ApiController]
[Authorize]  // 默认应用
[Route("api/v1/[controller]")]
public class FloorsController : ControllerBase { }

// RSSI 上报端点：仅 X-Api-Key + 方案名 ApiKey（设备无 JWT）
[HttpPost("/api/v1/rssi/report")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public async Task<IActionResult> ReportRssi([FromBody] ReportRssiRequest request) { }

// 严禁在日志中记录设备 ID 之外的个人信息
// 设备位置数据保留期限：默认 90 天，超期自动清除（使用 HangFire 定时任务）
```

---

## 附录：常用代码片段索引

| 功能 | 文件位置 |
|------|---------|
| 定位计算完整流程 | `Infrastructure/Positioning/PositioningPipeline.cs` |
| Redis 位置读写 | `Infrastructure/Caching/RedisPositionCache.cs` |
| SignalR 推送 | `Infrastructure/Notifications/PositioningNotificationService.cs` |
| MAUI BLE 扫描 | `Mobile/Services/BleScanner.cs` |
| 卡尔曼滤波器 | `Infrastructure/Positioning/KalmanFilter2D.cs` |
| RSSI 三边定位 | `Infrastructure/Positioning/TrilaterationEngine.cs` |
| 全局异常中间件 | `API/Middleware/GlobalExceptionMiddleware.cs` |
| API Key 认证 | `API/Authentication/ApiKeyAuthenticationHandler.cs` |
| 轨迹批量写入（COPY，默认） | `Infrastructure/Persistence/BulkWriters/NpgsqlTrajectoryBulkWriter.cs` |
| 轨迹批量写入（SqlBulkCopy，可选） | `Infrastructure/Persistence/BulkWriters/SqlServerTrajectoryBulkWriter.cs` |
| 数据库选型与部署假设 | `doc/database-selection.md` |
