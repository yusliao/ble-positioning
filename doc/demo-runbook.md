# 客户演示 Runbook

在会议前按顺序执行，保证 **15–20 分钟** 主线可复现；**新功能（进区、在线、Webhook、可选卡尔曼等）** 建议放在 **§8 扩展演示**，避免主线超时。权威契约仍以 [`api-conventions.md`](api-conventions.md) 与根目录 [`README.md`](../README.md) 为准；**分步学习与自测**见 [**`customer-learning.md`**](customer-learning.md)（与本文互补：本文偏「会议演示」，`customer-learning` 偏「可照着练」）。**口播与计时**见 [`demo-rehearsal-checklist.md`](demo-rehearsal-checklist.md)。

## 1. 环境

| 项 | 说明 |
|----|------|
| 推荐路径 | 仓库放在 **纯英文路径**（如 `D:\...\ble-positioning`），避免 Docker 构建/compose 在中文父目录下报错。 |
| .NET 8 | `dotnet --version` 为 8.x。 |
| Docker Desktop | 用于 `docker compose` 一键起 API + Admin + PG + Redis。 |
| 浏览器 | 演示 Admin 用 Chrome/Edge 即可。 |

## 2. 一键起栈与健康检查

在仓库**根目录**执行：

```powershell
docker compose up -d --build
```

稍等服务就绪后：

```powershell
pwsh -File scripts/verify-mvp.ps1
```

- API Swagger：`http://localhost:5000/swagger`（若本地仅 `dotnet run` API 而无 compose，则端口以 `launchSettings` 为准，如 `http://localhost:5230`）。
- 管理端：`http://localhost:5001`

若 `docker compose` 报 **gRPC / `x-docker-expose-session-sharedkey` / non-printable ASCII**（常见与路径或 BuildKit 有关）：

```powershell
$env:DOCKER_BUILDKIT="0"; $env:COMPOSE_DOCKER_CLI_BUILD="0"; docker compose up -d --build
```

仍失败时，将仓库移到英文路径后重试（见根 `README.md`）。

## 3. 默认账号

通过 **`POST /api/v1/auth/login`** 与 Admin 登录页使用同一套开发凭据，见 `appsettings.json` → `DevAdmin`：

- 若使用 **`Users`** 列表：示例为 **`admin` / `ChangeMe!`（`Admin`）** 与 **`viewer` / `ViewOnly!`（`Viewer`）**；**演示建楼层/信标/设备**请用 **Admin**；**只读走查界面**可用 **Viewer**（写接口在 API 为 **403**）。  
- 若未配 **`Users`**：单账号 **`Username` / `Password`**，角色为 **Admin**。

角色与 JWT 行为见 [`api-conventions.md`](api-conventions.md) §8；上机练习见 [`customer-learning.md`](customer-learning.md) §5–§6。

## 4. 演示前数据准备（最短路径）

1. 浏览器打开 **Admin** → 登录。  
2. **楼层**：创建一层，填写**宽/高 (m)**，上传一张**平面图**或填写地图 URL。  
3. **信标**：在该层至少 **3 个** 信标（与 [`PositioningOptions:MinBeaconsRequired`](../src/BlePositioning.Infrastructure/Options/PositioningOptions.cs) 默认一致），记录各信标的 `Uuid` / `Major` / `Minor` / 坐标。  
4. **设备**：在 **设备** 页使用 **新建设备** 创建追踪设备，**立即保存一次性 API Key**（仅显示一次；勿写入 git）。若使用 Swagger，则 `POST /api/v1/devices`。有 RSSI/位姿写入后，同页 **「在线」** 列会随 Redis `pos:` 键刷新（与 [`PositionTtlSeconds`](../src/BlePositioning.Infrastructure/Options/PositioningOptions.cs) 一致；无点或未上报时为否）。  
5. **RSSI 模拟**：使用 [`scripts/demo-simulate-rssi.ps1`](../scripts/demo-simulate-rssi.ps1) 与示例配置 [`doc/demo-simulate-config.example.json`](demo-simulate-config.example.json)，填入 `apiBase`（与 API 可访问根 URL 一致）、`deviceId`、`apiKey`、信标与楼层尺寸。见脚本内注释。  
6. **可见动点**：打开 Admin **「实时地图」**（`/map-live`），选择楼层与设备，观察地图上的**当前点**与模拟器联动的位姿（需先运行模拟器并等待管道写入 Redis）。

`GET /health/ready` 若初访 **503**，等待 PostgreSQL/Redis 完全就绪后重试。

## 5. 故障预案

| 现象 | 处理 |
|------|------|
| Admin 白屏/无法登录 | 确认 `Api:Base`（或环境变量 `Api__Base`）与浏览器能访问的 API 根地址一致。 |
| 健康检查失败 | `docker ps` 看容器；读 API 日志；确认连接串与 Redis/PG 已起。 |
| 模拟器 401/403 | 检查 `X-Api-Key` 是否与设备匹配；请求体 `deviceId` 与 Key 绑定设备须一致。 |
| 有 RSSI 但无位姿 | 信标数是否 ≥3；信标 Uuid/Major/Minor 是否与库中一致；看 API 日志中 `Insufficient beacons` 等警告。 |
| 无 Docker 仅 PPT 演示 | 使用预录屏 + Runbook 步骤截图；或远程桌面到已部署环境。 |

## 6. 相关脚本与文档

- [`scripts/verify-mvp.ps1`](../scripts/verify-mvp.ps1) — 起 compose 后打 `/health`、`/health/ready`。  
- [`scripts/demo-simulate-rssi.ps1`](../scripts/demo-simulate-rssi.ps1) — 无 BLE 硬件时模拟上报。  
- [`doc/demo-rehearsal-checklist.md`](demo-rehearsal-checklist.md) — 口播与计时清单（含主线 + **扩展**时间盒）。  
- [`doc/demo-simulate-config.example.json`](demo-simulate-config.example.json) — 模拟器配置样例。  

## 7. 不在主线内但仍已实现（可放到 §8）

以下能力**仓库内已有**，不占用 15–20 分钟主线路径；口播时一句话带过或进入 **§8**：

- **进/出区事件、在线/离线事件**：落库可 REST 查，Hub 有对应推送（`api-conventions` §10、§12）。  
- **进区 Webhook**：`GeofenceWebhook:Enabled` + `Url` 时对事件体做 HTTP 出站，可选 HMAC（§11）。  
- **2D 卡尔曼**：`Positioning:UseKalmanFilter` 与 `StoreKalmanStateInRedis` 见 [`PositioningOptions`](../src/BlePositioning.Infrastructure/Options/PositioningOptions.cs) 与 `design-spec` §2.3 / ADR-008。  

## 8. 扩展演示（选做，8～12 分钟，时间盒）

> 在主线 **§4** 已跑通（楼层/信标/设备/RSSI/实时地图或轨迹）之后再加；**总控时可跳过整节**。以下每项可单独选做，不必全讲。

| 时间盒 | 内容 | 操作要点 / 口播 |
|--------|------|-----------------|
| **0:00–2:30** | **围栏 + 进区** | 在 Admin 楼层行点 **「围栏」**，为当前楼层加一条**启用**的 `alert_rules`（`zonePolygon` 为**米制**、外环**闭合**的 GeoJSON `Polygon`，多边形**包住**你模拟器里设备大致活动区域，见 `api-conventions` §9）。**Admin** 保持 RSSI 模拟运行，使位姿**穿越**多边形边沿。用 Swagger **`GET /api/v1/devices/{id}/geofence-events?startTime=&endTime=`** 展示有记录（同楼层 Hub 会推 **`GeofenceEvent`**，在 **`PositionUpdated` 之后**）。无 Admin「事件列表」页，**以 Swagger/契约为准**。 |
| **2:30–4:30** | **在线列 + 停报看离线** | 回到 **设备** 表：有位姿时 **「在线」** 为是。**停止** `demo-simulate-rssi.ps1`，等待超过 **`PositionTtlSeconds`（默认 60s）** 后再看列表或点进设备 —— 可配合口播 `device_presence_events` 与 `GET .../presence-events`（§12）。**不必**在会议上演满 60s，可「切幻灯片说明 + 会后再验」。 |
| **4:30–6:00** | **Hub 多事件**（技术听众） | 若有人已连 SignalR：同楼层有 **`PositionUpdated`**、**`GeofenceEvent`**；**`DevicePresenceEvent`** 为**全局广播**（与楼层订阅独立）。三者在 `api-conventions` §8 / §10 / §12。 |
| **6:00–8:00** | **Webhook 听筒**（有准备时） | 会前准备一个可收 POST 的 URL（如 `webhook.site` 或本机 `nc`/小 HTTP 服务）。在 API 环境设置 **`GeofenceWebhook:Enabled=true`**、**`GeofenceWebhook:Url=...`**。复现进区后展示收到 JSON 与可选 **`X-Ble-Webhook-Signature`**（§11）。**未准备则跳过**（不影响主线）。 |
| **8:00–10:00** | **卡尔曼**（选讲） | 说明默认仍为 **质心 + α 平滑**；多实例/对比场景可开 **`Positioning:UseKalmanFilter`** 与（水平扩展时）**`StoreKalmanStateInRedis`**，需**重启** API 后生效。不现场改配置也成立。 |
| **+ 缓冲 1–2 min** | Q&A、回到主线回顾 | 强调「RSSI 热路径不经 Rabbit」仍成立；**Rabbit / MassTransit** 见 `mvp-scope` 与 ADR-009，非本次必讲。 |

**扩展合计**：约 **8–12 分钟**（不执行「停报等 60s」可省出约 1–2 分钟）。与主线相加时，**单次会议可控制在 25–32 分钟**；若只讲「围栏 + 一条查询」**约 +4 分钟** 即可。

## 9. 明确不在产品承诺内的内容（范围墙）

**产品级**身份系统（企业 IdP、SSO、更多角色矩阵）、**生产** K8s 全套、真实 **MAUI/蓝牙** 终端等，仍以 [`mvp-scope.md`](mvp-scope.md) **Out of scope** 为准。  

**当前定位与精度口播**建议表述为：**默认**为加权质心 + **一阶低通**；**可选**开启 **2D 卡尔曼** 与多实例下 **Redis 卡尔曼状态**（`design-spec` ADR-008），仍非室外 GNSS 级方案；NLOS/多径需后续算法与场勘。
