# BLE 室内定位（BlePositioning）

## 本地开发

1. 启动 **PostgreSQL 16** 与 **Redis**（或使用仓库根目录 `docker-compose.yml` 仅起 `postgres` + `redis`）。
2. 在仓库根目录执行：`dotnet build BlePositioning.slnx`  
   - 自动化测试：`dotnet test BlePositioning.slnx`（`tests/BlePositioning.Tests`：定位数学、`ApiKeyHasher`、RSSI、**`ApiWebAppFactory` 烟雾**、楼层/信标/设备列表/轨迹等；**`Category=docker`** 使用 **Testcontainers** 真实 PostgreSQL+Redis，含 **`/health/ready`、登录、楼层列表、写入 `position_logs` 后轨迹查询**）  
   - 无 Docker 时排除容器集成：`dotnet test --filter "Category!=docker"`
3. 运行 API：`dotnet run --project src/BlePositioning.API/BlePositioning.API.csproj`  
   启动时会自动执行 **EF 迁移**（`MigrateAsync`）。
4.（可选）再开一个终端，运行管理端：`dotnet run --project src/BlePositioning.Admin/BlePositioning.Admin.csproj`  
   - 默认连接 `Api:Base=http://localhost:5230`（与 API `http` 配置一致，见 `BlePositioning.Admin/appsettings.Development.json`）  
   - 浏览器打开提示端口（如 `http://localhost:5223`），使用 **Admin** 账号登录后可 **查看/创建/编辑/删除楼层**，在楼层下 **管理信标**；**Viewer** 账号仅可浏览（写操作在 API 上为 **403**）。亦可通过 Swagger 调同一套 API（详见 `doc/customer-learning.md`）。
5. Swagger：`http://localhost:5230/swagger`（端口以 `launchSettings.json` 为准）。

### 默认账号（仅开发）

- 见 `src/BlePositioning.API/appsettings.json` → **`DevAdmin`**。  
- 若配置了 **`Users`** 列表：默认含 **`admin` / `ChangeMe!`（Admin）** 与 **`viewer` / `ViewOnly!`（Viewer，只读）**；若未使用 `Users`，则仅 **`Username` / `Password`** 一对且为 **Admin**。  
- 角色与 API 行为说明：**`doc/api-conventions.md` §8**；分步练习：**`doc/customer-learning.md`**。

### Docker（API + Admin + PG + Redis）

```bash
docker compose up -d --build
```

**若出现** `x-docker-expose-session-sharedkey` / `non-printable ASCII`：**仓库所在路径请避免中文等特殊字符**（例如将目录改为 `...\ble-positioning` 再 `git clone`/移动项目），或在当前终端尝试临时关闭 BuildKit 后重试：

```powershell
$env:DOCKER_BUILDKIT="0"; $env:COMPOSE_DOCKER_CLI_BUILD="0"; docker compose up -d --build
```

- API：`http://localhost:5000`（**`GET /` 为文字说明**；调试请用 **`/swagger`**）。若浏览器提示无法连接，请先执行 `docker compose up -d` 并在「服务」中确认 **`bp-api`** 已运行、本机 **5000** 未被占用。  
- **Blazor 管理端**（楼层/信标 CRUD UI）：`http://localhost:5001`（容器内 `Api__Base=http://api:8080`）  
- 连接串与密钥见 `docker-compose.yml` 的 `environment`。

**一键健康检查**（需已安装 PowerShell 与 Docker）：

```powershell
pwsh -File scripts/verify-mvp.ps1
```

## 生产部署（阶段 G）

- **检查表**：[`doc/production-readiness-checklist.md`](doc/production-readiness-checklist.md)（密钥、网络、限流、冒烟、回滚）。
- **密钥轮换与限流/安全头**：[`doc/security-operations.md`](doc/security-operations.md)。
- **环境**：`ASPNETCORE_ENVIRONMENT=Production` 加载 `appsettings.Production.json`（HSTS、限流默认开启；**不**暴露 Swagger）。
- **限流**：RSSI 默认 **10 次/秒/设备**；其余 API **60 次/分钟/用户或 IP**；超限 **429**（见 `api-conventions.md` §4）。
- **CI**：push/PR 跑无 Docker 测试；**`integration-docker`** job 跑 `Category=docker`（Testcontainers）。

## 文档

- **索引**：`doc/README.md`（契约以 **`doc/api-conventions.md`** 为准）。  
- **客户学习与动手实践**（分步练习、JWT/角色、Swagger、RSSI→位姿、自测表）：**[`doc/customer-learning.md`](doc/customer-learning.md)**。  
- **演示 Runbook**（**主线 15～20 分钟** + 可选 **§8 扩展 8～12 分钟**）：[`doc/demo-runbook.md`](doc/demo-runbook.md)。

## 客户演示（无真 BLE 硬件）

- 步骤、端口、**扩展时间盒**与排障：[`doc/demo-runbook.md`](doc/demo-runbook.md)  
- 口播与计时（**主线 A / 扩展 B** 分表）：[`doc/demo-rehearsal-checklist.md`](doc/demo-rehearsal-checklist.md)  
- 种子数据与坐标约定：[`doc/demo-seed.md`](doc/demo-seed.md)（勿将 API Key 提交到 git）  
- 合成 RSSI：`pwsh -File scripts/demo-simulate-rssi.ps1 -Config <你的config.json>`（模板见 `doc/demo-simulate-config.example.json`）  
- Admin：创建楼层/信标/设备，**实时地图** 路由为 `/map-live`。

## 已实现（MVP 骨架）

- 分层：`Domain` / `Application` / `Infrastructure` / `API`
- `GET /health`、`GET /health/ready`
- JWT 登录、**`X-Api-Key`** 设备认证、`POST /api/v1/rssi/report`（202 + `X-Trace-Id`）
- RSSI **Channel** + **`PositioningPipelineService`**（加权质心 + 简单平滑）、**Redis** `pos:{deviceId}`
- **`GET /api/v1/devices/{id}/position`**（JWT，成功信封；无位置或设备不存在 → 404）
- **`GET /api/v1/devices/{id}/trajectory`**（JWT；`startTime` / `endTime` 必填 ISO 8601，`floorId` 可选，`intervalSeconds` 默认 1、范围 1–60；按秒时间桶聚合 **`position_logs`**，最多 **10000** 点，超限 → **400**；设备不存在 → **404**）
- **进/出区事件 `geofence_events`**：管道内边沿判定 + **Redis** 状态；**GET** `/api/v1/devices/{id}/geofence-events`；SignalR **`GeofenceEvent`**；可选 **HTTP Webhook 出站**（`GeofenceWebhook`，见 **`doc/api-conventions.md` §10–§11**）
- **在线/离线 `device_presence_events`**：**`pos:` + `PositionTtlSeconds`** 与 **`isOnline`** 一致；**`GET`** `/api/v1/devices/{id}/presence-events`；SignalR **`DevicePresenceEvent`**（见 **`doc/api-conventions.md` §12**）
- **卡尔曼（阶段 F，可选）**：**`Positioning:UseKalmanFilter`**、**`StoreKalmanStateInRedis`**（Redis 键 **`device:{id}:kalman`**）；默认仍为 **α 平滑**；详见 **`doc/design-spec.md`** ADR-008、**`doc/implementation-phases.md`** 阶段 F
- **围栏规则 `alert_rules`（JWT）**：`GET/POST /api/v1/floors/{floorId}/alert-rules`，`PUT/DELETE .../alert-rules/{ruleId}`；**`zone_polygon`** 为（GeoJSON `Polygon` 子集、米制、闭合外环，见 `doc/api-conventions.md` §9）；**`trigger_on`/`AlertTriggerKind`** 0/1/2；**写** 需 **Admin**；**Admin** 在楼层行点 **「围栏」** 管理
- **楼层与信标 CRUD（JWT）**：`GET/POST /api/v1/floors`、`GET/PUT/DELETE /api/v1/floors/{id}`；**`POST /api/v1/floors/{floorId}/map-image`**（multipart **`file`**，静态 URL **`/maps/...`**）；`GET/POST /api/v1/floors/{floorId}/beacons`、`PUT/DELETE /api/v1/floors/{floorId}/beacons/{beaconId}`；新建信标若与**未软删**行 **(uuid, major, minor)** 冲突 → **409**
- **`GET /api/v1/devices`**（JWT）：追踪设备摘要列表（管理端「设备」页）
- 创建设备 **`POST /api/v1/devices`** 返回 **一次性明文 API Key**（哈希落库）
- EF **snake_case**、迁移 **`tracked_devices.api_key_hash`**；信标 **`ix_beacons_uuid_major_minor`** 为 **`WHERE is_deleted = false`** 的部分唯一索引（与 `doc/design-spec.md` §2.4.2 一致）
- **`ITrajectoryBulkWriter`** 默认 **`NpgsqlTrajectoryBulkWriter`**（`COPY` 二进制导入 → `position_logs` 分区父表；`NoOpTrajectoryBulkWriter` 仍保留供测试替换）
- **SignalR** `/hubs/positioning`：**`JoinFloor` / `LeaveFloor`**，后台推送 **`PositionUpdated`**（与 `IPositioningNotificationService` 对接）；浏览器连接请带 `?access_token=<JWT>`，已配置 CORS 策略 `SignalR`
- **P7 管理端**：`BlePositioning.Admin`（Blazor Server），JWT 调 API：**楼层**（含地图文件上传）、**信标 CRUD**（**Admin**）、**设备列表与轨迹查询**、导航链接 **Swagger**；**Viewer** 隐去写操作入口
- **`docker-compose.yml`** 顶层 **`name: blepositioning`**，避免在中文目录下出现 `project name must not be empty`
