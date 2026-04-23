# CLAUDE.md — BLE 室内定位系统 AI 编程上下文

> 本文件为 AI 辅助编程主上下文，供 Claude Code / Cursor / GitHub Copilot 读取。
> 每次生成代码前请参考本文件中的规范和约定。

**文档阅读顺序（零歧义）**：`design-spec.md` → **`api-conventions.md`** → `mvp-scope.md` → `packages.md` → `coding-standards.md` → `database-selection.md` → 本文件。

---

## 项目概述

**项目名称**：BLE Indoor Positioning System（蓝牙室内定位系统）

**目标**：基于 BLE 5.0 Beacon 信号强度（RSSI）实现室内人员/资产定位，
提供实时位置追踪、历史轨迹回放、围栏告警功能，定位精度目标 1.5–3 米。

**用户群体**：
- 管理员：通过 Blazor Admin 管理地图、信标、人员
- 终端用户：通过 MAUI 移动端接收位置信息或作为定位采集器
- 监控人员：通过 Web Dashboard 实时观察位置大屏（**MVP 不交付**独立 Dashboard 项目，见 `doc/mvp-scope.md` §3）

---

## 技术栈（锁定版本）

| 层级 | 技术 | 版本 |
|------|------|------|
| 运行时 | .NET | 8.0 LTS |
| Web API | ASP.NET Core | 8.0 |
| 实时通信 | SignalR | 8.0 |
| 管理前端 | Blazor WebAssembly | 8.0 |
| 移动端 | .NET MAUI | 8.0 |
| ORM | Entity Framework Core | 8.0 |
| 数据库 | PostgreSQL 16+（开发与生产） | `doc/database-selection.md`；ADR 见 `design-spec.md` ADR-005 |
| 缓存 | Redis (StackExchange.Redis) | 7.x |
| 消息队列 | RabbitMQ (MassTransit) | 3.x；首版 RSSI 不经 AMQP，见 `design-spec.md` ADR-009 |
| 容器化 | Docker + Kubernetes | — |
| 日志 | Serilog + Seq | — |
| 监控 | Prometheus + Grafana | — |
| 测试 | xUnit + Moq + FluentAssertions | — |

---

## 解决方案结构

```
BlePositioning/
├── src/
│   ├── BlePositioning.Domain/          # 领域实体、值对象、领域事件、仓储接口
│   ├── BlePositioning.Application/     # 用例、轻量 CQS（默认应用服务）、DTO、端口接口（见 design-spec ADR-001）
│   ├── BlePositioning.Infrastructure/  # EF Core、Redis、RabbitMQ、BLE算法实现
│   ├── BlePositioning.API/             # ASP.NET Core Web API + SignalR Hub
│   ├── BlePositioning.Admin/           # Blazor WebAssembly 管理后台
│   └── BlePositioning.Mobile/          # .NET MAUI 移动端
├── tests/
│   ├── BlePositioning.Domain.Tests/
│   ├── BlePositioning.Application.Tests/
│   └── BlePositioning.Infrastructure.Tests/
├── docker-compose.yml
├── k8s/
└── CLAUDE.md                           # 本文件
```

**层级依赖规则（严格执行）**：
- Domain：不依赖任何其他层，只依赖 .NET BCL
- Application：只依赖 Domain
- Infrastructure：依赖 Application + Domain
- API：依赖 Application（通过 DI），**不**在控制器或服务定位器中直接 `new` Infrastructure 类型

**Composition Root（程序集引用）**：允许 **`BlePositioning.API` 项目引用 `BlePositioning.Infrastructure`**，且 **仅** 在启动路径（如 `Program.cs`）调用 `AddInfrastructure(...)` 等扩展方法完成注册；业务代码仍通过 **Application 接口** 解析服务。若团队希望 API 项目零引用 Infrastructure，可另增宿主项目（如 `BlePositioning.Host`）承担 Composition Root，由该宿主引用 API（启动管线）与 Infrastructure。

---

## 核心领域模型

AI 生成代码时，以下实体是系统核心，必须遵循这些定义：

```csharp
// 楼层地图
public class Floor : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }       // "1F", "2F"
    public string BuildingCode { get; private set; }
    public double WidthMeters { get; private set; }
    public double HeightMeters { get; private set; }
    public string? MapImageUrl { get; private set; }
    public IReadOnlyList<Beacon> Beacons { get; }
}

// BLE 信标
public class Beacon : Entity
{
    public Guid Id { get; private set; }
    public string Uuid { get; private set; }       // "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"
    public int Major { get; private set; }
    public int Minor { get; private set; }
    public double X { get; private set; }          // 相对地图坐标（米）
    public double Y { get; private set; }
    public Guid FloorId { get; private set; }
    public int TxPower { get; private set; }       // 默认 -59 dBm at 1m
    public BeaconStatus Status { get; private set; }
}

// 追踪设备/人员（API Key 哈希仅存数据库列 api_key_hash，见 design-spec §2.4.2.1；Domain 不持久化明文 Key）
public class TrackedDevice : AggregateRoot
{
    public Guid Id { get; private set; }
    public string DeviceCode { get; private set; } // 唯一业务编码
    public string DisplayName { get; private set; }
    public DeviceType Type { get; private set; }   // Person / Asset
    public Guid? CurrentFloorId { get; private set; }
    public Position? LastKnownPosition { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
}

// 位置值对象
public record Position(double X, double Y, double Accuracy, DateTime Timestamp);

// RSSI 上报数据（不入库，直接处理）
public record RssiReport(
    Guid DeviceId,
    List<BeaconSignal> Signals,
    DateTime Timestamp);

public record BeaconSignal(string Uuid, int Major, int Minor, int Rssi);
```

---

## 定位算法规范

AI 生成定位相关代码时，必须遵循以下算法流程和参数：

### 路径损耗模型

```csharp
// 标准路径损耗公式：distance = 10 ^ ((TxPower - RSSI) / (10 * n))
// n: 环境因子（办公室=2.0，工厂=2.8，医院=3.0）
public static double RssiToDistance(int rssi, int txPower, double pathLossExponent = 2.0)
{
    if (rssi == 0) return double.MaxValue;
    return Math.Pow(10.0, (txPower - rssi) / (10.0 * pathLossExponent));
}
```

### 三边定位

采用加权最小二乘法（WLS），权重 = 1 / distance²，最少需要 3 个信标。

### 卡尔曼滤波

状态向量：`[x, y, vx, vy]`，过程噪声 Q=0.1，测量噪声 R=2.0（可配置）。
每次收到新坐标时执行 predict + update 两步。

**状态存储（`design-spec.md` ADR-008）**：**单实例**部署可使用进程内字典；**多实例**水平扩展时，滤波状态必须外置到 **Redis**（键 `device:{deviceId}:kalman`），避免同一设备在不同节点上状态分裂。

### 处理管道

```
RssiReport → [RSSI聚合窗口 300ms] → [距离计算] → [三边定位] → [卡尔曼滤波] → [写Redis] → [SignalR推送]
```

---

## API 设计规范

### 路由约定

```
GET    /api/v1/floors                    # 获取楼层列表
GET    /api/v1/floors/{id}/beacons       # 获取楼层信标
POST   /api/v1/rssi/report               # 上报 RSSI 数据（MAUI 客户端调用）
GET    /api/v1/devices                   # 获取设备列表
GET    /api/v1/devices/{id}/trajectory   # 历史轨迹（带时间范围参数）
POST   /api/v1/alerts/rules              # 创建围栏告警规则
```

### 响应包装与错误（权威：`doc/api-conventions.md`）

- **2xx 有 JSON 体**（除 RSSI **202 空体**）：HTTP 层使用 **`ApiResponse<T>`** 形状：`success`、`data`、`error`、`traceId`（见 `api-conventions.md` §2.1）。
- **202 RSSI**：**空体** + 响应头 **`X-Trace-Id`**（`api-conventions.md` §2.2）。
- **4xx / 5xx**：**RFC 7807 ProblemDetails**，**禁止**用 200 + `success: false` 表示业务/资源错误；Application 层 `Result<T>` 在 API 边界映射为状态码 + ProblemDetails（`api-conventions.md` §3）。

```csharp
public record ApiResponse<T>(bool Success, T? Data, string? Error, string? TraceId);
```

### RSSI 上报接口（高频调用，性能关键）

```
POST /api/v1/rssi/report
X-Api-Key: {deviceApiKey}
Content-Type: application/json

{
  "deviceId": "guid",
  "signals": [
    { "uuid": "...", "major": 1, "minor": 1, "rssi": -65 }
  ],
  "timestamp": "ISO8601"
}
```

- 认证：**`X-Api-Key`** 头 + ASP.NET Core 方案名 **`ApiKey`**（见 `design-spec.md` **ADR-006**、`coding-standards.md` §12）。
- 此接口不等待定位计算结果，直接返回 202 Accepted，计算异步进行。

---

## SignalR Hub 规范

### Hub 端点

```csharp
// Hub 地址：/hubs/positioning
public class PositioningHub : Hub
{
    // 客户端加入楼层分组（只接收该楼层位置更新）
    public Task JoinFloor(Guid floorId);

    // 客户端离开楼层分组
    public Task LeaveFloor(Guid floorId);
}
```

### 服务端推送事件（客户端监听）

```javascript
// 位置更新（每 500ms 推送一次，或有变化时）
connection.on("PositionUpdated", (deviceId, x, y, accuracy, floorId) => { });

// 设备离线（30s 未收到 RSSI 上报）
connection.on("DeviceOffline", (deviceId) => { });

// 围栏告警
connection.on("AlertTriggered", (alertId, deviceId, ruleName, timestamp) => { });
```

### 分组命名

楼层分组：`floor:{floorId}`，单设备追踪：`device:{deviceId}`

---

## EF Core 约定

```csharp
// 实体配置使用 IEntityTypeConfiguration<T>，统一放在 Infrastructure/Persistence/Configurations/
// 物理表名列名与 design-spec.md §2.4.2 一致：snake_case（ADR-007）；推荐 AppDbContext 使用 UseSnakeCaseNamingConvention()
public class BeaconConfiguration : IEntityTypeConfiguration<Beacon>
{
    public void Configure(EntityTypeBuilder<Beacon> builder)
    {
        builder.ToTable("beacons");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Uuid).HasMaxLength(36).IsRequired();
        builder.HasIndex(b => new { b.Uuid, b.Major, b.Minor }).IsUnique();
        // 坐标精度：2位小数，单位：米（PostgreSQL numeric(10,2)）
        builder.Property(b => b.X).HasColumnType("numeric(10,2)");
        builder.Property(b => b.Y).HasColumnType("numeric(10,2)");
    }
}

// 历史轨迹表按月分区（高写入量）
// position_logs 不使用 EF Core 写入；通过 ITrajectoryBulkWriter（默认 Npgsql COPY）批量写入；EF 仅只读查询
```

---

## Redis 键命名规范

> 与代码一致（2026-04 起）；**信标解析**当前走 **EF 查询**（`IBeaconLookup`），**无** `beacon:...` Redis 键。

```
pos:{deviceId}                         # 设备最新位置（JSON；TTL = Positioning:PositionTtlSeconds，默认 60s）
gfstate:{deviceId:N}:{ruleId:N}        # 围栏边沿上一状态（阶段 C）
dpl:{deviceId}                         # 设备在/离线生命状态 on|off（阶段 E；TTL = DevicePresence:StateKeyTtl）
device:{deviceId}:kalman               # 可选 2D 卡尔曼状态（JSON；TTL = Positioning:KalmanStateTtlSeconds）；仅当 UseKalmanFilter 且 StoreKalmanStateInRedis，见 ADR-008
```

---

## 错误处理规范

```csharp
// 使用 Result<T> 模式，禁止在 Application 层 throw 业务异常
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}

// 基础设施层异常允许 throw（数据库、Redis连接等），在 API 层通过全局异常中间件处理
// 全局异常中间件统一转换为 ProblemDetails 格式返回
```

---

## 日志规范

```csharp
// 使用结构化日志，关键操作必须记录
_logger.LogInformation("Position calculated. DeviceId={DeviceId}, X={X}, Y={Y}, Accuracy={Accuracy}m",
    deviceId, position.X, position.Y, position.Accuracy);

// 定位失败（信标数不足等）
_logger.LogWarning("Insufficient beacons for trilateration. DeviceId={DeviceId}, BeaconCount={Count}",
    deviceId, signals.Count);

// 禁止在 Debug 以外记录原始 RSSI 数组（高频日志会产生大量噪音）
_logger.LogDebug("Raw RSSI signals: {@Signals}", signals); // 仅 Debug 环境
```

---

## 单元测试规范

```csharp
// 测试命名：被测方法_场景_期望结果
[Fact]
public void RssiToDistance_ValidRssi_ReturnsCorrectDistance()
{
    // Arrange
    var calculator = new PathLossCalculator();
    // Act
    var distance = calculator.RssiToDistance(rssi: -65, txPower: -59, pathLossExponent: 2.0);
    // Assert
    distance.Should().BeApproximately(1.99, precision: 0.1);
}

// 每个 Application 用例都要有对应的测试类
// 定位算法模块测试覆盖率目标：≥ 90%
```

---

## 禁止事项（AI 生成代码时必须避免）

- **禁止**在 Domain 层引用 EF Core / Redis 等基础设施包
- **禁止**在 Application 层直接 new DbContext
- **禁止**使用 `Thread.Sleep`，用 `await Task.Delay` 替代
- **禁止**将原始 RSSI 数据写入关系数据库（只写处理后的坐标和日志）
- **禁止**在 MAUI BLE 扫描回调中执行网络请求（必须先入队，异步上报）
- **禁止**SignalR Hub 方法直接执行耗时操作（必须通过后台服务异步处理）
- **禁止**在生产代码中硬编码 TxPower / 路径损耗系数（必须从配置读取）
- **禁止**对定位坐标结果不做边界校验（X/Y 不能超出楼层地图尺寸）
