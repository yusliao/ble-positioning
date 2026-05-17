# 会话进度存档（下次接续用）

> 最后更新：2026-05-17（**阶段 G 完成**）

## 已完成（相对历史版本）

- **文档**：`api-conventions.md`、`mvp-scope.md`、`packages.md`、`doc/README.md` 等；**`README.md`** 含 DevAdmin、Docker/Admin 端口、**测试说明**、**中文路径下 Docker 报错** 与 `DOCKER_BUILDKIT=0` 临时处理。
- **解耦与构建**：`BlePositioning.slnx` 含 **Domain / Application / Infrastructure / API / Admin**；测试工程 **`tests/BlePositioning.Tests`**。`dotnet build BlePositioning.slnx` 与 **`dotnet test BlePositioning.slnx`** 当前可通过（见下「测试条数」）。
- **数据与管道**：`position_logs` 分区表 + **`DEFAULT` 子分区**；**`NpgsqlTrajectoryBulkWriter`**（Binary COPY）；**`ITrajectoryBulkWriter` 已非 NoOp 默认**；`DbContext` 与 `DbContextFactory` 的 **Options 生命周期**已修正；**`AddPositionLogs`** 迁移在库中应存在。
- **实时推送**：**SignalR** `/hubs/positioning`，**`JoinFloor` / `LeaveFloor`**，**`IPositioningNotificationService`** + **`PositionUpdated`**；Pipeline 在落库后通知（失败只打日志）。
- **只读信标 API**：`GET /api/v1/floors/{floorId}/beacons` + **`IFloorRepository.ExistsActiveAsync` / `ListActiveBeaconsByFloorAsync`**。
- **管理端 P7**：**`BlePositioning.Admin`**（Blazor Server，JWT 调 API，楼层/信标/设备 CRUD 与地图上传）；`docker-compose` 含 **admin:5001**；根 **`name: blepositioning`** 避免项目名为空（中文父目录时）。
- **阶段 A～F**：权限、围栏 CRUD、进出区、Webhook、在线/离线、卡尔曼（见 `implementation-phases.md` 各阶段摘要）。
- **阶段 G（生产就绪）**：
  - API：**安全响应头**（`SecurityHeadersMiddleware`）、**限流**（`RateLimiting:*`，RSSI `rssi-report` / 其余 `general`，429 + ProblemDetails）。
  - 配置：**`appsettings.Production.json`**（HSTS、限流）。
  - 文档：**`production-readiness-checklist.md`** v1、**`security-operations.md`**；根 **`README` 生产部署** 节。
  - CI：**`.github/workflows/ci.yml`** 增加 **`integration-docker`** job（`Category=docker`）。
  - 测试：**`RateLimitingApiTests`**、**`SecurityHeadersApiTests`**；`ApiWebAppFactory` 默认 **`RateLimiting:Enabled=false`**。
- **测试条数**（`Category!=docker` 本地）：**52** 用例；全量含 docker 为 **56** 用例（以本机 `dotnet test` 为准）。

## 环境注意

- 仓库在 **`…\蓝牙室内定位\` 等含中文路径** 时，部分环境 **`docker compose --build`** 会报 **gRPC / `x-docker-expose-session-sharedkey` / non-printable ASCII**；**推荐**将仓库放在纯英文路径，或设 **`DOCKER_BUILDKIT=0` + `COMPOSE_DOCKER_CLI_BUILD=0`** 后重试（见根 **`README.md`**）。

## 仍待 / 可后续

- **MAUI** 移动端、独立监控大屏、**MassTransit/Rabbit**、生产 **K8s** 清单（仍 Out of scope）。
- **DevicePresence / Alert 出站 Webhook**、登录专用更严限流、Refresh Token 端点（设计 spec 有述，代码未全量）。
- 在 **英文路径** 下复验 **`docker compose up -d --build`** + **`verify-mvp.ps1`**（本地运维，非代码门禁）。

## 下次接续可选项

1. MAUI 采集端垂直切片（RSSI 上报 + 最小 UI）。  
2. 生产 IdP / 多租户替换 `DevAdmin` 配置用户。  
3. Prometheus/Grafana 仪表盘与 SLO 告警规则落地。

## 常用命令

```bash
cd D:\MyDomain\src\AI\ble-positioning
dotnet build BlePositioning.slnx
dotnet test BlePositioning.slnx
dotnet test --filter "Category!=docker"   # 不拉 Testcontainers
dotnet test --filter "Category=docker"    # 需 Docker
dotnet run --project src/BlePositioning.API/BlePositioning.API.csproj
docker compose up -d --build
```

## 文档入口

优先阅读顺序见 **`doc/README.md`**；**客户/上机分步实践**见 **`doc/customer-learning.md`**；**生产上线**见 **`production-readiness-checklist.md`**。

**全量实现**见 **[`doc/implementation-phases.md`](implementation-phases.md)**（阶段 G 已勾选 DoD）。
