# 架构设计方案 — BLE 室内定位系统

> 版本：1.3 | 本文档描述各模块的详细设计决策和实现模式
> AI 生成模块代码时，以本文档中的设计为准

---

## 1. 架构决策记录（ADR）

### ADR-001：采用 Clean Architecture + 轻量 CQRS（CQS）

**决策**：Domain / Application / Infrastructure / Presentation 四层分离。Application 层采用 **轻量命令查询分离（CQS）**：**读路径**与**写路径**在职责与技术上可分开优化；**不强制**「每个用例一对 `ICommand`/`IQuery` + Handler 类」的完整 CQRS 仪式。

**理由**：读操作（位置/轨迹/楼层列表）远多于管理端写操作；读侧可用 `AsNoTracking`、Dapper 或 Redis，写侧经领域模型与事务边界更清晰。项目规模有限时，全套 Handler 目录结构易带来维护噪音，收益递减。

**影响**：
- **默认推荐**：按 **特性/模块** 组织 **应用服务类**（如 `FloorService`、`DeviceService`），方法命名体现读写意图（`GetFloorByIdAsync`、`CreateFloorAsync`）；复杂或高变更用例可再抽 **独立 Query/Command + Handler**。
- **读路径**：可直接查询 DTO / 投影视图，**不必**经过 Domain 实体装载（与性能目标一致）。
- **写路径**：变更聚合状态须经过 **Domain 实体**（或明确设计的领域服务），禁止在 API 控制器中散落事务与领域规则。
- **RSSI / 定位热路径**：由 **Channel + `PositioningPipelineService`** 等专用管道承担（见 ADR-002），**不**套用 Application 层 Command Handler 形态。

### ADR-002：RSSI 上报使用 Channel + 后台管道处理

**决策**：RSSI 上报 API 接收数据后写入内存 Channel，由独立的 `PositioningPipelineService`（BackgroundService）消费并执行定位计算。

**理由**：设备可能每秒上报 2–5 次，100 台设备并发时峰值 500 req/s，定位计算（矩阵运算 + Redis）约需 50ms，同步处理会阻塞请求线程。

**Channel 配置**：容量 1000，满时丢弃最旧（DropOldest）——宁可丢弃旧数据，不阻塞上报接口。

### ADR-003：历史轨迹不使用 EF Core 写入

**决策**：`PositionLogs` 表通过 **`ITrajectoryBulkWriter` 端口** 批量写入。本项目开发与生产均使用 **PostgreSQL**，默认实现为 **`COPY` / `BeginBinaryImport`（Npgsql）** 等批量路径。**禁止**在高频路径使用 EF Core Change Tracker 逐行 `SaveChanges`。若将来个别环境需接 SQL Server，可另行提供基于 **SqlBulkCopy/TVP** 的 `ITrajectoryBulkWriter` 实现，不作为当前默认。

**理由**：轨迹数据每台设备每天约产生 86400 行（每秒 1 条），100 台设备 = 864 万行/天。EF Core 的 Change Tracker 机制在此场景开销过大。

**影响**：
- 业务层只依赖 `ITrajectoryBulkWriter` 抽象；实现类位于 `Infrastructure/Persistence/BulkWriters/`（见 `coding-standards.md`）。
- 集成测试可对 `ITrajectoryBulkWriter` 使用 Fake 或 Testcontainers 真实驱动。

### ADR-004：定位结果 Redis 优先，数据库兜底

**决策**：最新位置只写 Redis（TTL 60s），历史轨迹写数据库。实时查询读 Redis，离线/历史查询读数据库。

**理由**：实时地图刷新频率 500ms/次，100 台设备 = 200 req/s 读操作，Redis 可轻松支撑，避免数据库压力。

### ADR-005：关系型数据库提供商（PostgreSQL）

**决策（已确定）**：**开发与生产环境均使用 PostgreSQL 16+**（具体小版本与托管形态见 `doc/database-selection.md`）。EF Core 使用 **Npgsql** 提供程序；本地 `docker-compose` 以 **PostgreSQL 容器** 为默认依赖；轨迹批量写入以 **`NpgsqlTrajectoryBulkWriter`（`COPY`）** 为默认实现。

**理由**：统一方言降低环境漂移；OSS 许可与托管选型灵活；高写入轨迹与 `COPY` 组合成熟。

**影响**：
- 迁移脚本、Testcontainers 配置与运维手册均以 **PostgreSQL** 为准；**不得**在默认路径假设存在 `NEWSEQUENTIALID()`、`SqlBulkCopy` 或 SQL Server 分区函数。
- DDL、UUID 与分区策略遵循 `design-spec.md` **§2.4.2**；`ITrajectoryBulkWriter` 默认注册 Npgsql 实现（见 `coding-standards.md`）。

### ADR-006：RSSI 设备端认证（HTTP 头与 ASP.NET Core Scheme）

**决策**：设备调用 `POST /api/v1/rssi/report` 时，**唯一**支持的凭据传递方式为 HTTP 头 **`X-Api-Key: {apiKey}`**。服务端使用自定义认证处理器，注册认证方案名 **`ApiKey`**，与控制器上 **`[Authorize(AuthenticationSchemes = "ApiKey")]`** 对应。OpenAPI/Swagger 以 **API Key 安全定义（name=`X-Api-Key`，in=`header`）** 描述该端点。

**非目标**：首版**不**支持 `Authorization: ApiKey …` 或 `Authorization: Bearer {deviceKey}` 作为设备上报凭据，避免与管理员 JWT 混淆及网关解析歧义。

**影响**：MAUI 与集成测试客户端必须发送 `X-Api-Key`；`ApiKeyAuthenticationHandler` 从该头读取并校验哈希（见 **§4.1**、**§2.4.2.1**）。**请求体 `deviceId` 必须与 Key 绑定设备一致**，否则 **403**（`api-conventions.md` §6）。

### ADR-007：PostgreSQL 物理命名与 EF Core 映射

**决策**：数据库表与列的**物理名**与 **§2.4.2 DDL** 一致，采用 **snake_case**（如 `floors`、`beacons`、`tracked_devices`、`position_logs`、`alert_rules` 及列 `floor_id`、`device_id` 等）。EF Core 中通过 **`ToTable`**、**`HasColumnName`** 或 **全局 `UseSnakeCaseNamingConvention()`**（Npgsql 生态常用扩展）将领域模型 PascalCase 属性映射到上述物理名。

**理由**：与 PostgreSQL 惯用风格及已有 DDL 一致；`NpgsqlTrajectoryBulkWriter` 的 `COPY` 列顺序必须与物理列顺序一致，避免与迁移/DDL 漂移。

**影响**：`CLAUDE.md` 中 EF 示例与迁移脚本均以 snake_case 为准；**不得**再使用与 §2.4.2 不一致的 PascalCase 表名（如 `Beacons`）作为物理表名。

### ADR-008：卡尔曼滤波器状态的部署形态

**决策**：
- **单 API 实例**（或定位管道与上报同进程且仅一实例）：`KalmanFilter2D` 可在进程内使用 **`ConcurrentDictionary<Guid, FilterState>`** 维护状态，与 **§2.1** 伪代码一致。
- **水平扩展（多 API/多 Worker 实例）**：进程内字典会导致同一 `deviceId` 在不同实例上滤波状态分裂。此时 **`KalmanFilter2D` 实现必须改为以 Redis 为主存储**：键 **`device:{deviceId}:kalman`**（见 `CLAUDE.md`），序列化滤波状态 JSON，TTL 与在线判定策略对齐（默认 300s）；读-改-写需考虑并发（可选用 Redis Lua 或按设备串行化通道）。

**影响**：部署拓扑从单实例扩展到多实例时，需切换 Redis 后端实现或增加 **按设备粘性路由**（备选，运维复杂度高，非默认）。

### ADR-009：RabbitMQ / MassTransit 首版职责边界

**决策**：**RSSI 热路径**（上报 → Channel → `PositioningPipelineService`）**不**经过 RabbitMQ，与 **ADR-002** 一致。首版（MVP）中 MassTransit + RabbitMQ 用于 **非热路径、可异步丢失容忍或可重试** 的场景预留，例如：**围栏告警出站通知**、**审计事件**、与其他系统的 **集成事件**；若首版尚未实现任何消费者，**允许**不在 `Program.cs` 中注册 MassTransit，但 **`docker-compose` 仍保留 `rabbitmq` 服务**，以便本地与生产拓扑一致及后续联调。

**理由**：避免为上报链路引入额外网络 hop 与序列化成本；同时保留消息基础设施供告警与扩展。

**影响**：性能文档与压测以 **Channel** 路径为准；监控告警中「队列」主要指 **§ADR-002** 内存 Channel，而非 AMQP 队列深度（除非已启用 MassTransit）。

---

## 2. 核心模块详细设计

### 2.1 定位引擎（Positioning Engine）

```csharp
// 完整处理管道实现参考
public class PositioningPipelineService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var aggregator = new RssiAggregator(windowMs: _options.RssiAggregationWindowMs);

        await foreach (var report in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            // 1. 聚合：同一设备 300ms 内的多个上报合并，取最强信号
            var aggregated = aggregator.Aggregate(report);
            if (aggregated is null) continue;  // 窗口未满

            // 2. 过滤：只保留已知 Beacon（查缓存）
            var knownBeacons = await FilterKnownBeaconsAsync(aggregated.Signals, stoppingToken);
            if (knownBeacons.Count < _options.MinBeaconsRequired)
            {
                _logger.LogWarning("Insufficient beacons. DeviceId={Id}, Count={Count}",
                    report.DeviceId, knownBeacons.Count);
                continue;
            }

            // 3. 计算距离
            var distances = knownBeacons
                .Select(b => new BeaconDistance(b.Beacon, _calculator.RssiToDistance(b.Rssi, b.Beacon.TxPower)))
                .OrderBy(d => d.Distance)
                .Take(5)  // 只用最近 5 个信标
                .ToList();

            // 4. 三边定位
            var rawPosition = _trilaterationEngine.Calculate(distances);
            if (rawPosition is null) continue;

            // 5. 卡尔曼滤波平滑（单实例进程内；多实例见 ADR-008）
            var smoothed = _kalmanFilter.Update(report.DeviceId, rawPosition);

            // 6. 边界裁剪（不能超出楼层地图）
            var clamped = ClampToFloor(smoothed, knownBeacons[0].Beacon.FloorId);

            // 7. 写入 Redis + 轨迹入队；后台刷盘必须实现为 ITrajectoryBulkWriter（默认 Npgsql COPY），见 ADR-003
            await _positionCache.SetAsync(report.DeviceId, clamped, stoppingToken);
            _trajectoryQueue.Enqueue(new TrajectoryPoint(report.DeviceId, clamped));

            // 8. SignalR 推送
            await _notificationService.NotifyPositionUpdatedAsync(
                knownBeacons[0].Beacon.FloorId, report.DeviceId, clamped, stoppingToken);
        }
    }
}
```

### 2.2 三边定位算法实现

```csharp
public class TrilaterationEngine : ITrilaterationEngine
{
    // 加权最小二乘三边定位
    // 至少需要 3 个信标，取前 3–5 个最近信标
    public Position? Calculate(List<BeaconDistance> beaconDistances)
    {
        if (beaconDistances.Count < 3) return null;

        // 以第一个信标为参考点，减少数值误差
        var anchor = beaconDistances[0];
        var points = beaconDistances.Skip(1).ToList();

        // 构造 Ax = b 方程组（线性化后的三边定位方程）
        int n = points.Count;
        var A = new double[n, 2];
        var b = new double[n];
        var weights = new double[n];

        for (int i = 0; i < n; i++)
        {
            var p = points[i];
            A[i, 0] = 2 * (p.Beacon.X - anchor.Beacon.X);
            A[i, 1] = 2 * (p.Beacon.Y - anchor.Beacon.Y);
            b[i] = Math.Pow(anchor.Distance, 2) - Math.Pow(p.Distance, 2)
                   + Math.Pow(p.Beacon.X, 2) - Math.Pow(anchor.Beacon.X, 2)
                   + Math.Pow(p.Beacon.Y, 2) - Math.Pow(anchor.Beacon.Y, 2);
            weights[i] = 1.0 / Math.Max(p.Distance * p.Distance, 0.1);  // 距离越近权重越高
        }

        // 加权最小二乘求解：x = (A^T W A)^-1 A^T W b
        var solution = SolveWeightedLeastSquares(A, b, weights);
        if (solution is null) return null;

        // 计算精度估计（残差均方根）
        double accuracy = CalculateRmse(A, b, solution);

        return new Position(solution[0], solution[1], accuracy, DateTime.UtcNow);
    }
}
```

### 2.3 卡尔曼滤波器

**当前实现（阶段 F）**：应用层 **`KalmanFilter2DMath`**（位置子空间 2×2 协方差、随机游走预测 + 量测更新）；**`PositioningPipelineService`** 在 **质心 + 楼层裁剪** 之后，若 **`Positioning:UseKalmanFilter`** 为 **`true`** 则调用 **`IKalmanPositionFilter`**，否则保留原 **α 一阶低通**。状态由 **`IKalmanStateStore`** 提供：**`InMemoryKalmanStateStore`**（单实例）或 **`RedisKalmanStateStore`**（键 **`device:{deviceId}:kalman`**，TTL **`KalmanStateTtlSeconds`**，与 ADR-008 一致）。**切换** `StoreKalmanStateInRedis` **需重启进程**（单例注册）。

以下为 **单实例** 进程内字典示意；**多实例**部署须将状态外置到 Redis（键约定见 `CLAUDE.md`），见 **ADR-008**。

```csharp
// 2D 卡尔曼滤波器（状态: [x, y, vx, vy]）
public class KalmanFilter2D
{
    // 每台设备独立维护滤波器状态
    private readonly ConcurrentDictionary<Guid, FilterState> _states = new();

    public Position Update(Guid deviceId, Position measured)
    {
        var state = _states.GetOrAdd(deviceId, _ => FilterState.Initial(measured));
        var dt = (measured.Timestamp - state.LastUpdate).TotalSeconds;

        // 预测步骤（匀速运动模型）
        var predicted = Predict(state, dt);

        // 更新步骤（融合测量值）
        var updated = UpdateWithMeasurement(predicted, measured);

        _states[deviceId] = updated;

        return new Position(updated.X, updated.Y,
            accuracy: Math.Sqrt(updated.Px + updated.Py),  // 协方差对角元素
            measured.Timestamp);
    }
}
```

### 2.4 数据库 Schema（PostgreSQL 默认 + SQL Server 对照）

#### 2.4.1 跨数据库约定（与 ADR-005 一致）

**当前项目**：开发与生产 **锁定 PostgreSQL 16+**；下表保留 **SQL Server** 列仅供对照或极少数迁移场景查阅。

| 概念 | 应用层 / C# | PostgreSQL（默认） | SQL Server（对照，当前不采用） |
|------|-------------|-------------------|------------------------------|
| 主键（业务实体） | `Guid` | `uuid`；**推荐** `gen_random_uuid()`（PG 13+）或 **UUIDv7** / 应用侧时间有序 UUID | `uniqueidentifier` + `NEWSEQUENTIALID()` 等 |
| 时间戳 | `DateTime`（**UTC**） | `timestamptz` + `now()` | `datetime2` + `SYSUTCDATETIME()` |
| 布尔 | `bool` | `boolean` | `bit` |
| 小数坐标 | `double` / `decimal` | `numeric(10,2)` 等 | `decimal(10,2)` 等 |
| 轨迹表主键策略 | 行标识 + 时间分区键 | **推荐** `BIGSERIAL` + `timestamp` 分区键（见 §2.4.2） | `BIGINT IDENTITY` + 分区方案 |
| 高写入轨迹 | `ITrajectoryBulkWriter` | **`COPY` / Binary COPY（Npgsql）** | `SqlBulkCopy` / TVP |

**分区运维**：须有自动化任务 **预创建下一月分区**（或等价分区边界），并与「位置数据保留 90 天」策略（见 `coding-standards.md` 安全节）对齐。

#### 2.4.2 PostgreSQL 示例 DDL（默认）

以下为 **实现与评审的权威 DDL 示意**；生产需补充 **分区维护脚本**、**索引并发创建** 与 **RLS（若需要）** 等组织标准。

```sql
CREATE TABLE floors (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name            varchar(50) NOT NULL,
    building_code   varchar(20) NOT NULL,
    width_meters    numeric(10,2) NOT NULL,
    height_meters   numeric(10,2) NOT NULL,
    map_image_url   varchar(500),
    is_deleted      boolean NOT NULL DEFAULT false,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE beacons (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    floor_id    uuid NOT NULL REFERENCES floors(id),
    uuid        varchar(36) NOT NULL,
    major       int NOT NULL,
    minor       int NOT NULL,
    x           numeric(10,2) NOT NULL,
    y           numeric(10,2) NOT NULL,
    tx_power    int NOT NULL DEFAULT -59,
    status      smallint NOT NULL DEFAULT 1,
    is_deleted  boolean NOT NULL DEFAULT false
);
CREATE INDEX ix_beacons_floor_id ON beacons(floor_id) WHERE is_deleted = false;
-- 与 EF 迁移一致：仅在未软删行上唯一，删除后可重用同一 (uuid, major, minor)
CREATE UNIQUE INDEX ix_beacons_uuid_major_minor ON beacons (uuid, major, minor) WHERE is_deleted = false;

CREATE TABLE tracked_devices (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    device_code        varchar(50) NOT NULL UNIQUE,
    display_name       varchar(100) NOT NULL,
    type               smallint NOT NULL,
    api_key_hash       varchar(64) NOT NULL,
    api_key_created_at timestamptz NOT NULL DEFAULT now(),
    is_deleted         boolean NOT NULL DEFAULT false,
    created_at         timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX ix_tracked_devices_api_key_hash
    ON tracked_devices(api_key_hash)
    WHERE is_deleted = false;

-- 父表：按月 range 分区（示例按 2026-01；生产用动态脚本建子表）
CREATE TABLE position_logs (
    id          bigserial,
    device_id   uuid NOT NULL,
    floor_id    uuid NOT NULL,
    x           numeric(10,2) NOT NULL,
    y           numeric(10,2) NOT NULL,
    accuracy    numeric(5,2) NOT NULL,
    timestamp   timestamptz NOT NULL,
    PRIMARY KEY (timestamp, id)
) PARTITION BY RANGE (timestamp);

-- 示例子分区（每月一张；实际由运维任务滚动创建）
CREATE TABLE position_logs_2026_01 PARTITION OF position_logs
    FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');

CREATE INDEX ix_position_logs_device_ts
    ON position_logs (device_id, timestamp DESC)
    INCLUDE (x, y, accuracy, floor_id);

CREATE TABLE alert_rules (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    floor_id     uuid NOT NULL REFERENCES floors(id),
    name         varchar(100) NOT NULL,
    zone_polygon text NOT NULL,
    trigger_on   smallint NOT NULL,
    is_enabled   boolean NOT NULL DEFAULT true
);
```

**说明**：PostgreSQL **分区表上的 `INCLUDE`** 以实际 PG 版本文档为准（通常 PG 11+ 可用）；若目标环境不支持 `INCLUDE`，改为普通复合索引 `(device_id, timestamp DESC)`，按需增加覆盖列。

#### 2.4.2.1 设备 API Key 存储与校验（权威）

- **存储列**：`api_key_hash`（**varchar(64)**，**小写十六进制**）、`api_key_created_at`（轮换审计，首版可仅用创建时间）。
- **禁止**：数据库中存储 **明文** API Key 或可逆加密 ciphertext（密钥材料仅创建时返回一次给管理员/脚本）。
- **哈希计算（固定）**：
  1. 配置项 **`ApiKey:Pepper`**（非空字符串，存 **User Secrets / 环境变量**，**不入库**）。若本地开发允许空 pepper，**生产必须非空**。
  2. 令 `raw` = 客户端提交的 API Key 明文（UTF-8 字符串）。
  3. 计算 **`SHA256( UTF8Bytes( pepper + raw ) )`**，输出 **64 字符小写 hex**，写入/比对 `api_key_hash`。
- **校验**：认证时对待验证 `raw` 按上式计算哈希，在 **`tracked_devices`** 上按 **`api_key_hash`** 索引查找 **`is_deleted = false`** 行；命中则设备身份为该行的 **`id`**。
- **与请求体一致性**：`POST /api/v1/rssi/report` 的 **`deviceId`** 必须与该行 **`id`** 一致，否则 **403**（详见 `doc/api-conventions.md` §6）。

#### 2.4.3 SQL Server 示例 DDL（备选，当前不采用）

以下与 §2.4.2 **语义等价**，仅作历史对照；**默认实现与 Docker 不以此为准**。

```sql
-- 楼层表
CREATE TABLE Floors (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Name        NVARCHAR(50)     NOT NULL,
    BuildingCode NVARCHAR(20)    NOT NULL,
    WidthMeters  DECIMAL(10,2)   NOT NULL,
    HeightMeters DECIMAL(10,2)   NOT NULL,
    MapImageUrl  NVARCHAR(500),
    IsDeleted    BIT             NOT NULL DEFAULT 0,
    CreatedAt    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt    DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);

-- 信标表
CREATE TABLE Beacons (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    FloorId     UNIQUEIDENTIFIER NOT NULL REFERENCES Floors(Id),
    Uuid        NVARCHAR(36)     NOT NULL,
    Major       INT              NOT NULL,
    Minor       INT              NOT NULL,
    X           DECIMAL(10,2)    NOT NULL,
    Y           DECIMAL(10,2)    NOT NULL,
    TxPower     INT              NOT NULL DEFAULT -59,
    Status      TINYINT          NOT NULL DEFAULT 1,
    IsDeleted   BIT              NOT NULL DEFAULT 0
);
CREATE INDEX IX_Beacons_FloorId ON Beacons(FloorId) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX IX_Beacons_Uuid_Major_Minor_Active ON Beacons(Uuid, Major, Minor) WHERE IsDeleted = 0;

-- 追踪设备表
CREATE TABLE TrackedDevices (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    DeviceCode       NVARCHAR(50)     NOT NULL UNIQUE,
    DisplayName      NVARCHAR(100)    NOT NULL,
    Type             TINYINT          NOT NULL,
    ApiKeyHash       CHAR(64)         NOT NULL,
    ApiKeyCreatedAt  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    CreatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX IX_TrackedDevices_ApiKeyHash ON TrackedDevices(ApiKeyHash) WHERE IsDeleted = 0;

CREATE TABLE PositionLogs (
    Id          BIGINT           IDENTITY(1,1),
    DeviceId    UNIQUEIDENTIFIER NOT NULL,
    FloorId     UNIQUEIDENTIFIER NOT NULL,
    X           DECIMAL(10,2)    NOT NULL,
    Y           DECIMAL(10,2)    NOT NULL,
    Accuracy    DECIMAL(5,2)     NOT NULL,
    Timestamp   DATETIME2        NOT NULL,
    CONSTRAINT PK_PositionLogs PRIMARY KEY CLUSTERED (Timestamp, Id)
) ON [MonthlyPartitionScheme](Timestamp);

CREATE INDEX IX_PositionLogs_DeviceId_Timestamp
    ON PositionLogs(DeviceId, Timestamp DESC)
    INCLUDE (X, Y, Accuracy, FloorId);

CREATE TABLE AlertRules (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    FloorId     UNIQUEIDENTIFIER NOT NULL REFERENCES Floors(Id),
    Name        NVARCHAR(100)    NOT NULL,
    ZonePolygon NVARCHAR(MAX)    NOT NULL,
    TriggerOn   TINYINT          NOT NULL,
    IsEnabled   BIT              NOT NULL DEFAULT 1
);
```

---

## 3. API 接口设计

**HTTP 成功/错误体、健康检查、RSSI 与 `deviceId` 一致性**：以 **`doc/api-conventions.md`** 为权威；本节描述路径与业务字段，**不**重复定义信封与 ProblemDetails。

### 3.1 RSSI 上报接口（性能关键）

```
POST /api/v1/rssi/report
X-Api-Key: {deviceApiKey}
Content-Type: application/json

请求体：
{
  "deviceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "signals": [
    { "uuid": "FDA50693-A4E2-4FB1-AFCF-C6EB07647825", "major": 1, "minor": 1, "rssi": -65 },
    { "uuid": "FDA50693-A4E2-4FB1-AFCF-C6EB07647825", "major": 1, "minor": 2, "rssi": -72 },
    { "uuid": "FDA50693-A4E2-4FB1-AFCF-C6EB07647825", "major": 1, "minor": 3, "rssi": -80 }
  ],
  "timestamp": "2024-01-15T10:30:00.000Z"
}

响应：**202 Accepted**，**空体**；响应头 **`X-Trace-Id`**（见 `api-conventions.md` §2.2）。

错误响应（ProblemDetails，见 `api-conventions.md` §3）：
400 Bad Request → 请求格式错误
401 Unauthorized → 缺失或无效 API Key
403 Forbidden → Key 有效但请求体 **deviceId** 与 Key 绑定设备不一致
429 Too Many Requests → 超过速率限制（每设备 10 req/s）
```

### 3.2 实时位置查询

```
GET /api/v1/devices/{deviceId}/position
Authorization: Bearer {jwt}

响应 200（成功信封，见 `api-conventions.md` §2.1）：
{
  "success": true,
  "data": {
    "deviceId": "guid",
    "floorId": "guid",
    "x": 12.50,
    "y": 8.30,
    "accuracy": 1.8,
    "timestamp": "2024-01-15T10:30:05.123Z",
    "isOnline": true
  },
  "error": null,
  "traceId": "..."
}

响应 404：设备不存在或从未上线（ProblemDetails）
```

### 3.3 历史轨迹查询

**已实现（MVP）**：数据来自 **`position_logs`**；按 **`intervalSeconds`** 将时间轴分桶，每桶内对 **x / y** 取平均并取 **`min(timestamp)`** 作为该点时刻；成功/错误体以 **`api-conventions.md`** 为准。

```
GET /api/v1/devices/{deviceId}/trajectory
  ?startTime=2024-01-15T08:00:00Z
  &endTime=2024-01-15T18:00:00Z
  &floorId=guid（可选，过滤楼层）
  &intervalSeconds=5（可选，采样间隔，默认1s，最大60s）
```

响应 200（成功信封，见 `api-conventions.md` §2.1）示例：

```json
{
  "success": true,
  "data": {
    "deviceId": "guid",
    "totalPoints": 3600,
    "points": [
      { "x": 10.2, "y": 5.1, "floorId": "guid", "timestamp": "..." },
      { "x": 10.5, "y": 5.3, "floorId": "guid", "timestamp": "..." }
    ]
  },
  "error": null,
  "traceId": "..."
}
```

注意：最多返回 10000 个点。若 **`ceil((endTime - startTime) / intervalSeconds) > 10000`** 或数据库聚合后仍超过上限，返回 **400**（ProblemDetails）；设备不存在 → **404**。

### 3.4 楼层地图管理

**已实现（MVP）** 路径如下；成功/错误体以 **`api-conventions.md`** 为准。创建信标时若 **(uuid, major, minor)** 与**未软删**行冲突 → **409**。

```
GET    /api/v1/floors                              → 楼层列表
GET    /api/v1/floors/{id}                         → 楼层详情
POST   /api/v1/floors                              → 创建楼层
PUT    /api/v1/floors/{id}                         → 更新楼层信息（含 mapImageUrl，空白则清空）
POST   /api/v1/floors/{floorId}/map-image          → 上传地图图片（multipart，字段 file）
DELETE /api/v1/floors/{id}                         → 软删除楼层（并软删除其下信标）

GET    /api/v1/floors/{floorId}/beacons            → 该楼层信标列表
POST   /api/v1/floors/{floorId}/beacons            → 添加信标（201 + 成功信封）
PUT    /api/v1/floors/{floorId}/beacons/{beaconId} → 更新信标坐标与 TxPower
DELETE /api/v1/floors/{floorId}/beacons/{beaconId} → 软删除信标
```

**已实现**：`POST /api/v1/floors/{floorId}/map-image`（`multipart/form-data`，字段名 **`file`**；JPEG/PNG/WebP/GIF，大小见配置 **`FloorMapStorage:MaxBytes`**）；成功后 **`mapImageUrl`** 指向可通过 **`GET /maps/...`** 匿名访问的静态文件（`UseStaticFiles`）。

**未实现（可后续迭代）**：列表侧「含信标数量统计」等增强字段；对象存储 / CDN 分发。

### 3.5 围栏规则（`alert_rules`）

**已实现**：按楼层 **CRUD**（REST 与 **Blazor Admin** 楼层下「**围栏**」页）；`zone_polygon` 存储 **GeoJSON `Polygon` 子集**（**楼层米制**平面坐标，校验见 `api-conventions.md` §9）。`trigger_on` 对应 **`AlertTriggerKind`**：0/1/2（进入 / 离开 / 两者）。**仅 JWT `Admin` 可写**。

```
GET    /api/v1/floors/{floorId}/alert-rules
POST   /api/v1/floors/{floorId}/alert-rules
PUT    /api/v1/floors/{floorId}/alert-rules/{ruleId}
DELETE /api/v1/floors/{floorId}/alert-rules/{ruleId}
```

### 3.6 进/出区事件（`geofence_events`，阶段 C）

**已实现**：在 **RSSI 定位管道**（`PositioningPipelineService`）每次成功解算并落 **轨迹** 后，对当前楼层**已启用**规则做点内判定（**射线法**，`Application.Geofence.PointInPolygon`）；边沿与 `trigger_on` 组合后写入 **`geofence_events`**；**上一状态**存 **Redis**（`gfstate:...`）。**HTTP 查询** `GET /api/v1/devices/{deviceId}/geofence-events`；**SignalR** 同楼层组广播 **`GeofenceEvent`**（在 **`PositionUpdated` 之后** 执行判定，契约见 `api-conventions.md` §10）。

**Webhook 出站**（**阶段 D**）：`GeofenceWebhook` 配置为 **Enabled** 且配置 **Url** 时，对同一事件体 **`POST` JSON**（HMAC 可选，见 `api-conventions.md` §11）。**在线/离线**（**阶段 E**）：`device_presence_events`、`dpl:` 状态与扫频、REST/SignalR 见 `api-conventions.md` §12。**未实现（可后续）**：**邮件** 与 **MassTransit「必须送达」**、复杂自交多边形专门处理、事件按租户隔离等。

---

## 4. 安全设计

### 4.1 认证方案

系统使用两套认证：

**JWT Bearer**（管理员 + 普通用户）：
- 有效期 15 分钟，Refresh Token 7 天
- 刷新端点：`POST /api/v1/auth/refresh`
- Blazor Admin / Web Dashboard 使用

**API Key**（设备端）：
- 每台追踪设备分配唯一 API Key
- Header 传递：`X-Api-Key: {key}`（与 **ADR-006**、**§3.1** 一致；ASP.NET Core 方案名 `ApiKey`）
- 仅用于 RSSI 上报接口（`/api/v1/rssi/report`）
- 存储与校验算法：**§2.4.2.1**（**pepper + SHA-256 hex**，不明文存储）

### 4.2 速率限制

```csharp
// 速率限制策略（Program.cs 配置）
builder.Services.AddRateLimiter(options =>
{
    // RSSI 上报：每设备每秒 10 次（滑动窗口）
    options.AddSlidingWindowLimiter("rssi-report", cfg =>
    {
        cfg.Window = TimeSpan.FromSeconds(1);
        cfg.SegmentsPerWindow = 2;
        cfg.PermitLimit = 10;
        cfg.QueueLimit = 0;
    });

    // 普通 API：每用户每分钟 60 次
    options.AddFixedWindowLimiter("general", cfg =>
    {
        cfg.Window = TimeSpan.FromMinutes(1);
        cfg.PermitLimit = 60;
    });
});
```

---

## 5. 部署架构

### Docker Compose（本地开发）

**数据库**：按 **ADR-005**，本地与生产一致使用 **PostgreSQL 16+**（示例镜像 `postgres:16-alpine`）。连接字符串使用 **Npgsql** 格式，详见 `doc/database-selection.md` §5。

```yaml
# docker-compose.yml 服务规划
services:
  api:
    build: ./src/BlePositioning.API
    ports: ["5000:8080"]
    depends_on: [postgres, redis, rabbitmq]
    environment:
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=blepositioning;Username=postgres;Password=Dev_Postgres_ChangeMe"
      ConnectionStrings__Redis: "redis:6379"

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: Dev_Postgres_ChangeMe
      POSTGRES_DB: blepositioning
    ports: ["5432:5432"]
    volumes: [postgres-data:/var/lib/postgresql/data]

  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru

  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports: ["15672:15672"]  # 管理界面；首版 RSSI 不经 AMQP，见 ADR-009

  seq:
    image: datalust/seq:latest
    ports: ["5341:80"]      # 日志查看界面
    environment:
      ACCEPT_EULA: "Y"

volumes:
  postgres-data:
```

### Kubernetes 资源规划（生产）

| 服务 | 副本数 | CPU Request | Memory Request |
|------|--------|-------------|----------------|
| BLE API | 2–4（HPA） | 500m | 512Mi |
| Positioning Worker | 2 | 1000m | 1Gi |
| Blazor Admin | 2 | 200m | 256Mi |
| Redis | 1 (Sentinel) | 500m | 1Gi |
| PostgreSQL | 1（HA 依厂商：Patroni / 云 RDS 多 AZ 等） | 2000m | 4Gi |

---

## 6. 性能指标目标（SLA）

| 指标 | 目标值 | 测量方法 |
|------|--------|---------|
| RSSI 上报接口 P99 响应时间 | < 10ms | Prometheus histogram |
| 定位计算延迟（上报→坐标就绪） | < 150ms | 内部计时 |
| SignalR 推送延迟 | < 50ms | 客户端端到端测量 |
| 并发追踪设备数（单实例） | 500 台 | 压测验证 |
| 位置历史查询（7天轨迹）P95 | < 2s | API 响应时间 |
| 系统可用性 | 99.5% | 月度统计 |

---

## 7. 监控告警配置

### 关键监控指标

```yaml
# Prometheus 采集指标（代码中需通过 prometheus-net 暴露）
ble_rssi_reports_total          # 上报总数（Counter）
ble_positioning_duration_ms     # 定位耗时（Histogram，桶: 10,50,100,200,500ms）
ble_channel_queue_depth         # Channel 队列深度（Gauge）
ble_active_devices              # 在线设备数（Gauge）
ble_positioning_failures_total  # 定位失败数（Counter，按原因分类）
ble_redis_errors_total          # Redis 错误数（Counter）
```

### Grafana 告警规则

| 告警 | 条件 | 严重性 |
|------|------|--------|
| RSSI 上报量骤降 | 5分钟内上报量下降 > 50% | Warning |
| 定位失败率过高 | 失败率 > 10%（5分钟滚动） | Critical |
| Channel 队列堆积 | 队列深度 > 500 持续 2 分钟 | Warning |
| Redis 连接失败 | 错误数 > 5 / 分钟 | Critical |
| API P99 超阈值 | P99 > 500ms 持续 5 分钟 | Warning |

---

## 8. 测试策略

### 定位算法测试

```csharp
// 测试数据集：已知信标坐标 + 已知 RSSI → 验证定位结果
[Theory]
[InlineData(-65, -70, -80, expectedX: 5.0, expectedY: 3.0, toleranceM: 0.5)]
[InlineData(-55, -75, -85, expectedX: 2.0, expectedY: 7.0, toleranceM: 0.5)]
public void TrilaterationEngine_KnownInputs_CalculatesCorrectPosition(
    int rssi1, int rssi2, int rssi3,
    double expectedX, double expectedY, double toleranceM)
{
    // 固定测试信标布局（等边三角形，边长 10m）
    var beacons = TestBeaconLayout.EquilateralTriangle(sideLength: 10.0);
    var distances = CalculateDistances(new[] { rssi1, rssi2, rssi3 }, beacons);

    var result = _engine.Calculate(distances);

    result.Should().NotBeNull();
    result!.X.Should().BeApproximately(expectedX, toleranceM);
    result.Y.Should().BeApproximately(expectedY, toleranceM);
}
```

### 集成测试（使用 WebApplicationFactory）

```csharp
// 测试 RSSI 上报 → 定位计算 → Redis 写入 完整流程
[Fact]
public async Task ReportRssi_ValidSignals_PositionStoredInRedis()
{
    // Arrange：使用内存 Redis（Testcontainers）
    var client = _factory.CreateClient();
    var request = new ReportRssiRequest(TestDeviceId, TestSignals);

    // Act
    var response = await client.PostAsJsonAsync("/api/v1/rssi/report", request);

    // Assert：等待异步处理
    response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    await Task.Delay(500);  // 等待后台处理完成
    var position = await _redis.GetAsync($"pos:{TestDeviceId}");
    position.Should().NotBeNull();
}
```
