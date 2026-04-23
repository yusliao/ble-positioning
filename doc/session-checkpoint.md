# 会话进度存档（下次接续用）

> 最后更新：2026-04-23（阶段 A、B、C、D、E、F 完成）

## 已完成（相对历史版本）

- **文档**：`api-conventions.md`、`mvp-scope.md`、`packages.md`、`doc/README.md` 等；**`README.md`** 含 DevAdmin、Docker/Admin 端口、**测试说明**、**中文路径下 Docker 报错** 与 `DOCKER_BUILDKIT=0` 临时处理。
- **解耦与构建**：`BlePositioning.slnx` 含 **Domain / Application / Infrastructure / API / Admin**；测试工程 **`tests/BlePositioning.Tests`**。`dotnet build BlePositioning.slnx` 与 **`dotnet test BlePositioning.slnx`** 当前可通过（见下「客户模拟演示」条中的条数；含 Docker 的 filter 时更多）。
- **数据与管道**：`position_logs` 分区表 + **`DEFAULT` 子分区**；**`NpgsqlTrajectoryBulkWriter`**（Binary COPY）；**`ITrajectoryBulkWriter` 已非 NoOp 默认**；`DbContext` 与 `DbContextFactory` 的 **Options 生命周期**已修正；**`AddPositionLogs`** 迁移在库中应存在。
- **实时推送**：**SignalR** `/hubs/positioning`，**`JoinFloor` / `LeaveFloor`**，**`IPositioningNotificationService`** + **`PositionUpdated`**；Pipeline 在落库后通知（失败只打日志）。
- **只读信标 API**：`GET /api/v1/floors/{floorId}/beacons` + **`IFloorRepository.ExistsActiveAsync` / `ListActiveBeaconsByFloorAsync`**。
- **管理端 P7**：**`BlePositioning.Admin`**（Blazor Server，JWT 调 API，楼层/信标/设备 CRUD 与地图上传）；`docker-compose` 含 **admin:5001**；根 **`name: blepositioning`** 避免项目名为空（中文父目录时）。
- **自动化测试**：
  - **无容器**：`PathLoss` / 质心 / **`ApiKeyHasher`**；**`ApiWebAppFactory`（`Testing` + InMemory + 模拟 Redis）** 测 `/health`、**`/health/ready`**；**RSSI 无 `X-Api-Key` → 401**。
  - **需 Docker**（`Category=docker`）：**Testcontainers** 真 PG+Redis，**`Integration` 环境** + **`UseSetting` 覆盖连接串`**；测 **`/health`、ready、login + 楼层列表**。
  - **产品辅助**：`Program` 在 **`Testing` 不执行 `MigrateAsync`**；**`Program.Partial.cs`** 暴露 `Program` 供 `WebApplicationFactory`。
- **`scripts/verify-mvp.ps1`**：起 compose 后打 `/health`、`/health/ready`。
- **客户模拟演示**：
  - 文档：[`doc/demo-runbook.md`](demo-runbook.md)、[`doc/demo-seed.md`](demo-seed.md)、[`doc/demo-rehearsal-checklist.md`](demo-rehearsal-checklist.md)（`doc/README.md` 已索引）。
  - **`scripts/demo-simulate-rssi.ps1`** + [`doc/demo-simulate-config.example.json`](demo-simulate-config.example.json)：无硬件合成 RSSI（路径损耗、三角往返路径）。
  - **Admin**：设备页 **新建设备** + 弹窗**一次性 API Key**；**`/map-live` 实时地图**（轮询 `GET /api/v1/devices/{id}/position`，地图叠加与 [`demo-seed` 坐标约定](demo-seed.md) 一致）。
- **阶段 A（权限）**：JWT **`role` claim**（`Admin` / `Viewer`）；`DevAdmin:Users` 双账户，空 `Users` 时回退 `Username`/`Password` 为 **Admin**；`POST/PUT/DELETE` 楼层与信标、**地图上传**、**新建设备** 需 **Admin**；`POST /api/v1/auth/login` 的 `data.role` 与 Admin `AdminAuthState` 对齐；测试 **`AuthorizationRolesApiTests`**（无 Docker）+ docker 健康/登录对 `Admin` 角色断言。
- **阶段 B（围栏规则）**：`alert_rules` 的 **POST/PUT/DELETE** + **`ZonePolygonValidator`**（`Polygon` 闭合外环、米制）；`GET .../alert-rules` 只读**双角色**；**Admin** 页 **围栏** 路由与 **`customer-learning`/`api-conventions` §9** 已同步。测试 **`AlertRulesCrudApiTests`**、**`ZonePolygonValidatorTests`**。
- **阶段 C（进/出区）**：表 **`geofence_events`**；管道内 **`IGeofenceEvaluationService`**（先 **`PositionUpdated`** 再判围栏）；**Redis** `gfstate:*`；**GET** `.../geofence-events`；SignalR **`GeofenceEvent`**。契约 **`api-conventions` §10**。测试 **`GeofenceEvaluationServiceTests`**、**`PointInPolygonTests`**。
- **阶段 D（Webhook 出站）**：配置 **`GeofenceWebhook`**（默认关）；`HttpGeofenceWebhookPublisher` 与 SignalR 经 **`CompositeGeofenceEventPublisher`** 串联；**HMAC**、重试/非重试策略见 **`api-conventions` §11**。测试 **`GeofenceWebhookPublisherTests`**。
- **阶段 E（在线/离线）**：**`isOnline`/`pos:`/TTL** 与 **`PositionTtlSeconds`** 对齐；**`dpl:`** + **`DevicePresenceSweeperService`**；**`device_presence_events`**、**`GET .../presence-events`**、Hub **`DevicePresenceEvent`**；**`DevicePresence`** 可调间隔/TTL/查询条数。契约 **§12**。测试 **`DevicePresenceOptionsTests`**；集成 `Testing` 下**不**跑扫频。
- **阶段 F（卡尔曼 + ADR-008）**：**`KalmanFilter2DMath`** + **`IKalmanPositionFilter`**；**`Positioning:UseKalmanFilter`**、**`StoreKalmanStateInRedis`**、**`device:{id}:kalman`**；单测 **`KalmanFilter2DMathTests`**。切换内存/Redis 存储需**重启**。
- **测试条数**（`Category!=docker` 本地）：**50** 用例；全量 `dotnet test` 含 `Category=docker` 为 **54** 用例（以本机 `dotnet test` 为准）。

## 环境注意

- 仓库在 **`…\蓝牙室内定位\` 等含中文路径** 时，部分环境 **`docker compose --build`** 会报 **gRPC / `x-docker-expose-session-sharedkey` / non-printable ASCII**；**推荐**将仓库放在纯英文路径，或设 **`DOCKER_BUILDKIT=0` + `COMPOSE_DOCKER_CLI_BUILD=0`** 后重试（见根 **`README.md`**）。

## 仍待 / 可后续

- （已做，阶段 A）**角色模型**：生产环境可再接入 IdP / 多租户；当前为 DevAdmin 配置 + JWT。
- **在线/离线事件** 已落库、Hub 可推送（**阶段 E**）；**DevicePresence 出站 Webhook**、**AlertTriggered** 与 **MassTransit/Email/「必须送达」队列** 等可后续与 D 同模式扩展。
- **RSSI 全链路集成测**：已增加 **`TestcontainersRssiPositioningTests`**（`Category=docker`：创楼层+3 信标+设备、POST RSSI、轮询位姿），Testcontainers 仍用于 Health/Auth/轨迹。
- **CI**：**`.github/workflows/ci.yml`** 在 push/PR 上 `dotnet test --filter "Category!=docker"`；含 Docker 的用例在本地/具备 Docker 的 agent 上跑全量 `dotnet test`。

## 下次接续可选项

1. 将仓库换到 **英文路径** 后，再验 **`docker compose up -d --build`** 与 **`verify-mvp.ps1`**。  
2. 权限模型与 **Blazor** 已分 **Admin/Viewer**；**alert-rules 列表与 Admin 管理** 已具备。  
3. 在 CI 中增加 **`Category=docker`** 独立 job（需 **Docker 服务** 或自托管 runner）。

## 常用命令

```bash
cd D:\MyDomain\src\AI\蓝牙室内定位
dotnet build BlePositioning.slnx
dotnet test BlePositioning.slnx
dotnet test --filter "Category!=docker"   # 不拉 Testcontainers
dotnet run --project src/BlePositioning.API/BlePositioning.API.csproj
docker compose up -d --build
```

## 文档入口

优先阅读顺序见 **`doc/README.md`**；**客户/上机分步实践**见 **`doc/customer-learning.md`**。

**全量实现**（多阶段、每阶段后更新 + 清上下文续做）见 **[`doc/implementation-phases.md`](implementation-phases.md)**（与本文档需同步：阶段完成时二选一并更新，推荐以 `implementation-phases` 的 DoD 打勾 + 本文档 2～5 行摘要为主）。
