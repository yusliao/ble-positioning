# 客户学习与动手实践指南

> **目标读者**：需要在本系统上**亲自跑通、理解接口、做演示或二次开发**的实施人员、工程师与培训讲师。  
> **读完应能**：独立启动环境；用 **Admin** 或 **Swagger** 完成「楼层 → 信标 → 设备 → RSSI → 位姿/轨迹」闭环；理解 **JWT 角色** 与 **设备 API Key** 两种身份；会查契约与排障。  
> **权威契约**仍以 [`api-conventions.md`](api-conventions.md) 为准；本指南侧重**可操作的步骤与自测**。

---

## 1. 建议阅读顺序（学习与动手）

| 阶段 | 文档 | 目的 |
|------|------|------|
| 入门 | 本文 + [`README.md`](../README.md) | 环境、端口、命令 |
| 契约 | [`api-conventions.md`](api-conventions.md) | 成功信封、错误体、`/health`、RSSI 安全、**§8 认证与角色** |
| 范围 | [`mvp-scope.md`](mvp-scope.md) | 已实现 vs 未实现，避免对功能误解 |
| 架构 | [`design-spec.md`](design-spec.md) §2–3 | 表结构、主要 REST 与管道概念 |
| 演示向 | [`demo-runbook.md`](demo-runbook.md) | **主线** 15–20 分钟；**扩展**（进区/在线/Webhook/卡尔曼口播）见该文 **§8** 与 [`demo-rehearsal-checklist.md`](demo-rehearsal-checklist.md) **B** |
| 无硬件 | [`demo-seed.md`](demo-seed.md)、[`demo-simulate-config.example.json`](demo-simulate-config.example.json) | 坐标与合成 RSSI 配置 |

**冲突时**：HTTP 细节以 `api-conventions.md` 为准（见 `doc/README.md` 说明）。

---

## 2. 核心概念（先读这几句再动手）

- **管理面（人）**：浏览器 **Admin** 或 **Swagger** 用 **JWT**。登录 `POST /api/v1/auth/login` 得到 `accessToken` 与 `role`（`Admin` 或 `Viewer`）。**写**楼层/信标/地图/创建设备等需要 **`Admin`**；**Viewer** 只能读列表与轨迹等。详见 **api-conventions §8**。
- **设备面（物）**：手机/网关上送 RSSI 使用 **`X-Api-Key`**，与**具体设备**绑定，**不**受 JWT 角色影响；请求体里的 `deviceId` 须与该 Key 一致。详见 **api-conventions §6**。
- **位姿与轨迹**：RSSI 经管道解算后写入 **Redis**（当前点）和 **PostgreSQL**（`position_logs` 轨迹）。查询用 JWT：`GET /api/v1/devices/{id}/position`、`.../trajectory`。
- **实时地图**：管理端 **实时地图** 轮询位姿 + 可选 **SignalR** 订阅；Hub 需带 `access_token` 参数。JWT 的 **Admin / Viewer** 均可订阅，**不**在 Hub 里做楼层/信标 CRUD。
- **进/出区（阶段 C）**：在有 **围栏规则** 且 RSSI 能解算位姿时，服务端会落库 **`geofence_events`**，并向同楼层 Hub 客户端推送 **`GeofenceEvent`**（顺序上在 **`PositionUpdated`** 之后）。REST 查询见 **`api-conventions` §10**。
- **在线/离线（阶段 E）**：设备列表 **「在线」** 与 Redis **`pos:`** 键是否未过期一致（**`PositionTtlSeconds`**）；**停报**超过 TTL 后可出现**离线**边沿，落库 **`device_presence_events`**，Hub 广播 **`DevicePresenceEvent`**。见 **§12**。
- **Webhook 出站（阶段 D）**：配置 **`GeofenceWebhook`** 后，进/出区事件可 **HTTP POST** 到外部 URL，可选 **HMAC**；**尽力而为**，非 Rabbit 热路径。见 **§11**。
- **卡尔曼与多实例（阶段 F）**：默认仍是 **质心 + α 平滑**；可打开 **`Positioning:UseKalmanFilter`**，多 API 实例时建议 **`StoreKalmanStateInRedis`**（键 **`device:{id}:kalman`**）。见 `design-spec` §2.3、ADR-008。

---

## 3. 环境准备（动手前检查）

- [ ] **.NET 8** SDK（`dotnet --version` 为 8.x）。
- [ ] **PostgreSQL 16** + **Redis 7**（可本机安装，或用仓库根目录 `docker compose` 一键起，见根 `README.md`）。
- [ ] 仓库在 **纯英文路径** 下（避免部分环境下 Docker/Compose 异常；见根 `README.md`）。
- [ ] 终端能访问：API 根 URL（如 `http://localhost:5230` 或 compose 的 `http://localhost:5000`）、（可选）Admin（如 `http://localhost:5001`）。

**两条常用启动方式**（二选一）：

1. **Docker 全栈**（API + Admin + PG + Redis）：在仓库根目录执行 `docker compose up -d --build`，再执行 `pwsh -File scripts/verify-mvp.ps1` 做健康检查。端口见根 `README.md`。
2. **本机 `dotnet run`**：先起 PG+Redis，再 `dotnet run` API 与 Admin 两个项目；`Admin` 的 `Api:Base` 须指向可访问的 API 根地址（如 `http://localhost:5230`）。

---

## 4. 实践 A：健康检查与根路径（约 5 分钟）

1. 浏览器或命令行请求 **`GET /health`**，应 **200**，体为 `{"status":"Healthy"}`（与契约一致）。  
2. 请求 **`GET /health/ready`**，在 PG+Redis 正常时应 **200**；若刚启动为 **503**，等待数秒后重试。  
3. 浏览器打开 **`GET /`（根路径）** 可看到纯文本的 API 说明；**OpenAPI 调试**用 **`/swagger`**。

**自测**：能说出 **Liveness** 与 **Readiness** 分别对应哪个端点、为何演示前要看 `/health/ready`。

---

## 5. 实践 B：开发账号与 Swagger 带 JWT（约 10 分钟）

### 5.1 默认账号（开发环境）

在 API 的 `appsettings.json` → **`DevAdmin`** 中配置（**生产须替换**）：

- 若存在 **`Users`** 数组且**非空**：只使用该列表中的账号。示例：**`admin` / `ChangeMe!`（`Admin`）**、**`viewer` / `ViewOnly!`（`Viewer`）**。
- 若 **`Users`** 为空或省略：使用 **`Username` / `Password`**，角色为 **Admin**。

根目录 [`README.md`](../README.md) 中有「仅开发」说明；契约细节见 **api-conventions §8**。

### 5.2 登录并拿到 token

- **Swagger**：展开 **`POST /api/v1/auth/login`** → Try it out，body 如：`{"username":"admin","password":"ChangeMe!"}` → 从响应 `data` 中复制 **`accessToken`** 与查看 **`role`**。  
- **PowerShell**（将 URL 换为你的 API 根）：

```powershell
$base = "http://localhost:5230"
$body = '{"username":"admin","password":"ChangeMe!"}'
$login = Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/login" -ContentType "application/json" -Body $body
$token = $login.data.accessToken
$login.data.role
```

### 5.3 在 Swagger 中授权

1. 点击页面 **「Authorize」**（锁图标）。  
2. **Value** 处只填 **token 字符串**（不要写 `Bearer ` 前缀，除非 UI 明确要整段 `Bearer xxx`，以你看到的 Swagger 说明为准）。  
3. 之后对需 JWT 的 `GET/POST/...` 会带上身份。

**自测**：用 **`Admin`** 成功调用 **`GET /api/v1/floors`**；换 **`Viewer`** 登录后同样能 `GET`；对 **`POST /api/v1/floors`** 应返回 **403**。

---

## 6. 实践 C：管理端（Blazor Admin）一条龙（约 20 分钟）

> 与 [`demo-runbook.md`](demo-runbook.md) §4 一致，此处按**学习目的**加粗要点。

1. 打开 Admin 地址（如 `http://localhost:5001`），使用 **`admin` / `ChangeMe!`** 登录。  
2. **楼层**：新建一层，填**宽/高 (m)**；可上传**平面图**或填地图 URL。  
3. **信标**：进入该层**信标**页，至少配置 **3 个**信标（与 `MinBeaconsRequired` 默认 **3** 一致），记 **UUID/Major/Minor** 与 **坐标 (m)**。  
4. **设备**：在**设备**页**新建设备**，**立即保存一次性 API Key**（仅一次；勿提交到 git）。有 RSSI 写入后，同页 **「在线」** 列会反映 **Redis 当前点**（无点或过期为否）。  
5. **只读体验**：另开无痕窗口，用 **`viewer` / `ViewOnly!`** 登录，确认能看楼层/设备列表，但**无**「新建楼层/信标/设备」等写入口（与 API 403 行为一致）。  
6. 打开 **实时地图**（`/map-live`），在后续「实践 D」有 RSSI/位姿后可观察动点。  
7. **（可选，阶段 B）** 在楼层表点击 **「围栏」**，为当前楼层添加一条 **alert 规则**（`zonePolygon` 为 **GeoJSON `Polygon`**、坐标 **米**、外环**闭合**；`triggerOn` 0/1/2 含义见 **`api-conventions` §9**）。**Viewer** 可浏览列表，**无**写入口。若与 **会议扩展演示**时间盒对齐，见 [`demo-runbook.md`](demo-runbook.md) **§8**。

**自测**：能向同事口述「楼层、信标、设备」各自存什么、设备上的 **Key** 用于哪类 API；**围栏规则**与 **RSSI/位姿**在业务上的关系（配置 vs 数据流，事件在阶段 C）。

---

## 7. 实践 D：RSSI 上报与位姿/轨迹（约 20 分钟）

1. 阅读 **api-conventions** **§2.2**（`202` 与 `X-Trace-Id`）与 **§6**（`deviceId` 与 `X-Api-Key` 必须一致）。  
2. 使用 **Swagger** `POST /api/v1/rssi/report` 或使用脚本：  
   - [`scripts/demo-simulate-rssi.ps1`](../scripts/demo-simulate-rssi.ps1)  
   - 配置模板：[`doc/demo-simulate-config.example.json`](demo-simulate-config.example.json)（`apiBase`、`deviceId`、`apiKey`、信标、楼层尺寸与 [`demo-seed.md`](demo-seed.md) 约定）。  
3. 上报后等待管道处理，再调：  
   - **`GET /api/v1/devices/{id}/position`**（JWT，成功信封；无点或设备不存在为 **404**）  
   - **`GET /api/v1/devices/{id}/trajectory?startTime=...&endTime=...`**（时间 **ISO 8601 UTC**，见契约）  
4. **（选做，与 `demo-runbook` §8 对齐）** 已配置**围栏**且能解算位姿时，可试 **`GET /api/v1/devices/{id}/geofence-events?...`**（§10）；需要讲**在线/离线**时，可试 **`GET .../presence-events`**（§12），两条均需 JWT 且设备须存在。

**自测**：故意写错 `deviceId` 与 Key 的对应关系，应能解释为何是 **403** 而非 401（见 api-conventions §6）。

---

## 8. 实践 E：SignalR 与 Hub 事件（可选）

- 连接 **`/hubs/positioning`** 时，浏览器 WebSocket 常通过查询参数带 JWT：**`?access_token=<token>`**（与 `Program.cs` 中管道一致）。  
- 调用 **`JoinFloor(floorId)`** 前，楼层须存在。  
- **与楼层订阅相关**（需 `JoinFloor`）：  
  - **`PositionUpdated`** — 位姿更新。  
  - **`GeofenceEvent`** — 进/出区（在 `PositionUpdated` 之后推送，参数见 **`api-conventions` §10.3**）。  
- **全局广播**（**不**依赖楼层组）：**`DevicePresenceEvent`** — 设备上/下线边沿（参数见 **§12**）。  

**会议节奏**：只想讲清「能推什么」时，照上表口播即可；**跟表练**可与 [`demo-runbook.md`](demo-runbook.md) **§8 扩展演示**、[`demo-rehearsal-checklist.md`](demo-rehearsal-checklist.md) **B** 对齐。

---

## 9. 自测表（可打印给培训学员）

- [ ] 能解释 **JWT** 与 **X-Api-Key** 各用于哪些场景。  
- [ ] **Admin** 与 **Viewer** 在管理端与 REST 上表现一致（写操作 403）。  
- [ ] 成功用 Swagger 或脚本完成 **至少一次** 完整 RSSI → **position/trajectory** 查询。  
- [ ] 知道从 **`/health` / `/health/ready`** 与 API 日志排查「无位姿、503、401/403」。  
- [ ] 能打开 `api-conventions` 与 `README`，找到 **信封字段名** 与**演示脚本入口**。  
- [ ] **（选）** 能对照 [`demo-runbook.md`](demo-runbook.md) **§8** 说清：进区 **REST**、**在线/离线**、**Hub 三类事件** 或 **Webhook** 中**至少一样**的用途与配置入口。

---

## 10. 常见问题速查

| 现象 | 处理方向 |
|------|----------|
| Admin 能开但全失败 | 检查 `Api:Base` / `Api__Base` 与浏览器可访问的 API 是否一致。 |
| Swagger 全 401 | 是否先 `Authorize`；token 是否过期。 |
| 写接口 403 | 若角色为 **Viewer** 为预期；需要 **Admin** 执行写操作。 |
| RSSI 401/403 | Key 与设备是否成对、请求体 `deviceId` 是否一致。 |
| 有 RSSI 但无位姿 | 信标数是否 ≥`MinBeaconsRequired`；UUID/Major/Minor 与库是否一致。 |
| Docker 构建报异常 | 尝试英文路径、`DOCKER_BUILDKIT=0`（见根 `README`）。 |

更多演示向排障见 [`demo-runbook.md`](demo-runbook.md) §5。

---

## 11. 进阶与路线图

- **全量产品阶段划分**、各阶段 **DoD**：[`implementation-phases.md`](implementation-phases.md)。  
- **实现进度存档**（团队内部，非对外交付物）：`session-checkpoint.md`。  
- 编码与分层习惯：[`coding-standards.md`](coding-standards.md)。

若你希望将本指南整理为**客户交付 PDF**，建议以本文件 + `api-conventions` + 根 `README` 为打印范围；`session-checkpoint` 可排除。

---

## 12. 版本说明

- 本指南与仓库 **MVP+** 能力同步；**Out of scope** 见 [`mvp-scope.md`](mvp-scope.md)（如独立监控大屏、完整 MAUI 等以文档为准，勿在培训中承诺未实现项）。
