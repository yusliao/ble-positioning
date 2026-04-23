# HTTP API 约定（成功体、错误体、健康检查）

> 版本：1.0 | **权威**：与 `design-spec.md` §3 冲突时以本文件为准（本文件专门消除歧义）。

---

## 1. 总体原则

- **版本前缀**：所有 REST JSON 资源路径 **`/api/v1/...`**（与现有设计一致）。
- **时间**：请求/响应体中的时间均为 **ISO 8601 UTC**（带 `Z` 或显式偏移）。
- **Content-Type**：JSON 请求/响应均为 **`application/json`**；错误体使用 **`application/problem+json`**（ASP.NET Core `ProblemDetails` 默认）。
- **关联 ID**：每个 HTTP 请求在响应中携带 **`traceId`**（与成功信封字段名一致）；实现上推荐 **`System.Diagnostics.Activity.Current?.Id`** 或 W3C **`traceparent`** 的短形式，须与日志 **Serilog** `TraceId` 对齐。

---

## 2. 成功响应（2xx，除 202 空体）

### 2.1 信封（强制）

凡返回 **JSON 业务体** 的 **2xx**（**不含** `202 Accepted` 且无体的端点），响应体 **必须**为：

```json
{
  "success": true,
  "data": { },
  "error": null,
  "traceId": "00-xxxxxxxxxxxxxxxxxxxxxxxx-xxxxxxxxxxxxxxxx-00"
}
```

- **`data`**：与端点相关的 DTO；无附加数据时用 **`null`** 或 **`{}`** 须在 OpenAPI 中固定（**推荐**无内容用 **`null`**）。
- **`traceId`**：字符串，非空；与日志关联。

### 2.2 `202 Accepted`（RSSI 上报）

- **体**：**空**（零字节体，`Content-Length: 0`）；**不**使用成功信封。
- **响应头（固定）**：**`X-Trace-Id`**，值为当前请求的 trace id（与 `Activity.Id` 或团队选定的 W3C trace 格式一致，且须与 Serilog 关联字段对齐）。

**零歧义选择（固定）**：**`202` 响应体为空**；**必须**包含 **`X-Trace-Id`**。

---

## 3. 错误响应（4xx / 5xx）

### 3.1 RFC 7807 ProblemDetails（强制）

- **Content-Type**：`application/problem+json`（RFC 7807；使用 ASP.NET Core **`ProblemDetails`** / **`ValidationProblemDetails`** 序列化）。
- **字段**：至少 `type`（URI 或相对路径）、`title`、`status`（与 HTTP 状态码一致）、`detail`（可对用户展示的安全描述）、`instance`（可选，请求路径）。
- **扩展**：将 **`traceId`**（camelCase）放入 **`extensions`**，与成功信封字段同名，便于客户端统一读取。

**业务规则违反**（如设备不存在）：使用 **404** + ProblemDetails，**不**使用 200 + `success: false` 信封。

**验证失败**：**400** + **`ValidationProblemDetails`**（`errors` 字段）。

### 3.2 与 `Result<T>`（Application 层）

- Application 层仍可使用 **`Result<T>`** 表达业务失败；**API 边界**负责映射为 **HTTP 状态码 + ProblemDetails**，**禁止**将业务失败以 **200 + success:false** 对外暴露（避免缓存、监控、客户端分支混乱）。

---

## 4. 常用状态码映射（建议表）

| 场景 | HTTP | 体 |
|------|------|-----|
| 成功有载荷 | 200 | 成功信封 |
| 已接受异步处理 | 202 | 空体 + `X-Trace-Id` |
| 验证失败 | 400 | ValidationProblemDetails |
| 未认证 | 401 | ProblemDetails |
| 已认证无权限 / 设备与 Key 不匹配 | 403 | ProblemDetails |
| 资源不存在 | 404 | ProblemDetails |
| 冲突（重复 `device_code`、**未软删信标**相同 **(uuid, major, minor)** 等） | 409 | ProblemDetails |
| 限流 | 429 | ProblemDetails，可含 `retry-after` |
| 未实现的 MVP 外功能 | 501 | ProblemDetails（可选） |
| 服务器内部错误 | 500 | ProblemDetails，`detail` 对用户模糊，日志含异常 |

---

## 5. 健康检查（零歧义）

| 端点 | 方法 | 认证 | 行为 |
|------|------|------|------|
| **`/health`** | GET | **无** | **Liveness**：进程存活即 **200**，JSON **`{ "status": "Healthy" }`**（小写 `Healthy` 固定）。**不**检查 PG/Redis。 |
| **`/health/ready`** | GET | **无** | **Readiness**：PG 与 Redis 均可连时 **200** 同上；任一失败 **503**，ProblemDetails，`detail` 不含敏感连接串。 |

- **Swagger**：可将 `/health` 标为 **Ignore** 或单独分组；**不得**要求 JWT。

---

## 6. RSSI 与设备身份一致性（安全）

- 请求体 **`deviceId`**（GUID）**必须**与 **`X-Api-Key` 解析出的设备主键一致**。
- **不一致**：返回 **403 Forbidden** + ProblemDetails（**不**返回 401，避免与无效 Key 混淆）。
- **无效 / 缺失 Key**：**401**。

---

## 7. OpenAPI

- 安全方案：**`Bearer`**（JWT）、**`ApiKey`**（header **`X-Api-Key`**，与 ADR-006 一致）。
- 全局响应组件：声明 **ProblemDetails**、成功信封 schema（可生成 C# DTO `ApiResponse<T>`）。

---

## 8. 认证与角色（JWT / DevAdmin）

- **登录**：`POST /api/v1/auth/login`，体为 `{ "username", "password" }`（camelCase）。成功时 `data` 含 **`accessToken`**、**`expiresAtUtc`**、**`role`**（字符串：`Admin` 或 `Viewer`）。
- **JWT**：访问受保护 API 时 Header **`Authorization: Bearer <accessToken>`**。令牌内包含 **`role` claim**（与 `data.role` 一致），服务端以 **`[Authorize(Roles = ...)]`** 约束写操作。
- **角色约定**（与 `BlePositioning.Application.Security.BlePositioningRoles` 一致）：
  - **`Admin`**：楼层/信标/地图的 **创建、更新、删除**；**创建设备**（`POST /api/v1/devices`）等写接口。
  - **`Viewer`**：**只读**（如 `GET /api/v1/floors`、`GET /api/v1/devices`、轨迹与位姿查询等）；写接口返回 **403 Forbidden**（ProblemDetails）。
- **账号来源**：开发环境由 **`DevAdmin`** 配置。若配置 **`Users`** 数组（非空），则**仅**使用该列表中的账号；若 **`Users`** 为空，则回退为单一字段 **`Username`** / **`Password`**，且该账号角色为 **`Admin`**。详见 API `appsettings.json` 中示例（`admin` / `viewer`）。

**SignalR**（`/hubs/positioning`）：需 Bearer（查询参数 `access_token` 与 Header 等效）。**`Admin`** 与 **`Viewer`** 均可连接并订阅楼层，写操作不经过 Hub（仍走 REST）。

**注意**：RSSI 上报等仍使用 **`X-Api-Key`**（设备身份），**不**受上述 JWT 角色约束。

---

## 9. 围栏规则 `alert_rules`（REST）

**路径前缀**（同 §1）：均位于 **`/api/v1/floors/...`**。成功体仍用 **§2.1 信封**；业务错误用 **400 / 404 / 403** 与 **ProblemDetails**（见 §3）。

| 方法 | 路径 | JWT | 说明 |
|------|------|-----|------|
| `GET` | `/floors/{floorId}/alert-rules` | Bearer（**Admin** 与 **Viewer** 均可） | 列出该楼下全部规则。楼层不存在或已软删 → **404**。 |
| `POST` | `/floors/{floorId}/alert-rules` | **仅 Admin** | 创建规则。成功 → **201**，`Location` 指向 `GET` 同楼层列表。 |
| `PUT` | `/floors/{floorId}/alert-rules/{ruleId}` | **仅 Admin** | 更新。规则或楼层不存在 → **404**；校验失败 → **400**。 |
| `DELETE` | `/floors/{floorId}/alert-rules/{ruleId}` | **仅 Admin** | 物理删除。不存在 → **404**。 |

**请求体 DTO**（`application/json`；字段 **camelCase** 与 C# 记录一致）：

- **创建** `CreateAlertRuleRequest`：`name`（string，1～100 字符，trim 后非空），`zonePolygon`（string），`triggerOn`（**smallint**），`isEnabled`（bool，可省略，默认 `true`）。
- **更新** `UpdateAlertRuleRequest`：同上，**`isEnabled` 必填**（无“部分更新”，整包替换）。

**`zonePolygon`（权威校验）**：

- 为 **JSON 文本**，长度 ≤ **100_000** 字符。  
- 语义为 **GeoJSON `Polygon`** 子集，且坐标为**楼层局部平面、单位米**（与信标/位姿 `x`/`y` 同系）：  
  - 根对象须含 **`"type": "Polygon"`**（必须精确写 `Polygon`）。  
  - **`coordinates`** 为**线性环的数组**；**第一环**为外环，**至少 4 个**位置（闭合环）。  
  - 每一位置为 **`[x, y]`**，均为**有限**数。  
  - **外环须闭合**：第一与最后一个位置在 **ε=1e-6** 内视为相等。  
- **不自检**自交、方向等高级几何；错误描述以服务端返回的 `detail` 为准。  
- 实现名：`ZonePolygonValidator`（`Application` 层供 API 与测试复用）。

**`triggerOn`** 与枚举 **`AlertTriggerKind`（`short`）** 一致，**仅**允许 **0、1、2**：

- **0** — 仅**进入**边沿时产生 `geofence_events` 行。  
- **1** — 仅**离开**边沿时产生。  
- **2** — **进入**与**离开**边沿均可产生（各判一次）。

**注意**：`GET .../alert-rules` 无分页；MVP+ 可后续加查询参数。

**进/出区事件与在线判定**（阶段 C 已实现）：见 **§10**；规则维护（本段）不替代事件表与管道。

---

## 10. 进/出区事件 `geofence_events`

### 10.1 产生时机

- 在 **RSSI 管道** 成功解算位姿、写入 **轨迹** 与 **Redis 当前点** 之后、与 **`PositionUpdated`（SignalR）** 的关系为：**先** 推送 `PositionUpdated`，**再** 执行围栏判定；事件落库后再向 **同楼层分组** 推送 `GeofenceEvent`（见 §10.3）。
- 仅处理 **本楼层** 且 **`is_enabled = true`** 的 `alert_rules`；`zone_polygon` 须能解析为与 §9 一致的外环，否则**跳过**该条规则并打日志。

### 10.2 状态与 `trigger_on`

- 服务端在 **Redis** 中按 `gfstate:{deviceId}:{ruleId}` 保存上一时刻点是否在多边形**内**（**无键视为外**），用于边沿检测；**TTL** 见配置 `GeofenceState:Ttl`（默认约 7 天）。
- **`eventKind`**（库列 `event_kind`）：**0 = Enter**（由外进内），**1 = Exit**（由内出外）。与 `alert_rules.trigger_on` 关系：**0** 仅发进入边沿、**1** 仅发离开边沿、**2** 两者皆可发（各自独立判定）。

### 10.3 查询与 SignalR

**HTTP**（成功信封，JWT **Admin/Viewer 均可**）：

```
GET /api/v1/devices/{deviceId}/geofence-events?startTime=...&endTime=...
```

- `startTime` / `endTime` 为 **ISO 8601**；**未**标 `Z` 时按 **UTC** 解释。`endTime >= startTime`；单设备最多 **10000** 条，按 `occurredAtUtc` 升序。
- 设备不存在或已软删 → **404**；时间域非法 → **400**。

**SignalR**（同 §8，`/hubs/positioning`，**Bearer** 与楼层分组 `floor:{floorId}` 不变）：服务端方法名 **`GeofenceEvent`**，**参数顺序**为：

`deviceId`（Guid）, `floorId`（Guid）, `alertRuleId`（Guid）, `eventKind`（short）, `x`（double）, `y`（double）, `occurredAtUtc`（`DateTime`，UTC）.

> **注意**：RSSI/设备面仍仅用 **`X-Api-Key`**，本表事件与 JWT 的授权查询相互独立；事件中的 `deviceId` 即追踪设备主键。

---

## 11. 进/出区事件 Webhook 出站（阶段 D）

在 **§10.1** 产生并落库进/出区事件、且 **SignalR** 已按同一次通知推送之后，服务端可再向**配置的 URL** 发送 **`POST`（`application/json`）**，与 §10.3 的 Hub 方法负载字段同源（**camelCase** JSON）：

- **`schemaVersion`**（string，当前为 **`"1.0"`**，与 `GeofenceWebhook:SchemaVersion` 可配置，默认 1.0）  
- **`deviceId`**, **`floorId`**, **`alertRuleId`**, **`eventKind`**, **`x`**, **`y`**, **`occurredAtUtc`**

**配置**（`appsettings` 节 **`GeofenceWebhook`**，环境变量可覆盖，如 `GeofenceWebhook__Enabled`）：

| 键 | 说明 |
|----|------|
| **`Enabled`** | **`false`（默认）** 时完全不发送。 |
| **`Url`** | 绝对 **HTTPS/HTTP** 地址；未配置或 `Enabled=false` 时跳过。 |
| **`Secret`** | 非空时：对 **请求体原始 UTF-8 字节** 计算 **HMAC-SHA256**；头 **`X-Ble-Webhook-Signature`**（可用 **`SignatureHeaderName`** 覆盖）的值为 **`sha256=`** + 小写十六进制。 |
| **`RequestTimeout`** | 单次 HTTP 尝试超时。 |
| **`MaxAttempts`**, **`RetryBaseDelay`** | 5xx/408/网络错误时指数回退重试，4xx 其它码视为**非重试**；用尽仍失败则仅记日志。 |

**语义**：**尽力而为**；Webhook 失败**不**回滚已落库事件与 SignalR。  
**Rabbit/Email/MassTransit** 等不属本节；可选后续阶段 **ADR-009** 扩展。

---

## 12. 设备在线语义与在/离线事件（阶段 E）

### 12.1 «在线» 判定（与 `GET .../position`、设备列表一致）

- Redis 中 **`pos:{deviceId}`** 当前**存在且未过期**时，设备视为 **在线**；过期删除后即视为**可作为离线边沿**的前提（与 **`Positioning:PositionTtlSeconds`** 一致：每次成功定位写入会刷新该键的 TTL）。  
- **`GET /api/v1/devices`** 列表项与 **`GET /api/v1/devices/{id}/position`** 响应中的 **`isOnline`** 均基于上一条规则。  
- **从未上报过**定位的设备：`pos:` 与 **`dpl:{deviceId}`** 生命状态均未建立时，**不**产生离线事件；首次成功写入后仅将 **`dpl`** 标为**在线**（不产生「上线」事件，除非此前已判过**离线**——见下）。

### 12.2 在/离线边沿与 `device_presence_events`

- Redis **`dpl:{deviceId}`** 存 `on` / `off`（若存在），表示**业务上**最近是否已判过**在线**或**离线**，TTL 见 **`DevicePresence:StateKeyTtl`**（默认 7 天，应大于 `PositionTtlSeconds`）。  
- **后台扫频**按 **`DevicePresence:SweepInterval`**（默认 5s）遍历非软删设备：若 **`pos:`** 已过期而 **`dpl` 为 on**，则落库 **离线**；若 **`pos:`** 存在而 **`dpl` 为 off**（曾离线），则落库 **恢复在线**。  
- **定位管道**在上报成功写 **`pos:`** 后也会参与：若 **`dpl` 为 off**，则落库 **恢复在线** 并刷新 **`dpl=on`**。  
- 事件存表 **`device_presence_events`**，`event_kind`：**0 = Online**，**1 = Offline**。

### 12.3 REST 与 SignalR

**HTTP**（成功信封，JWT **Admin/Viewer** 均可）：

```
GET /api/v1/devices/{deviceId}/presence-events?startTime=...&endTime=...
```

- 规则与 **§10.3** 设备查询相同（时间窗口、`endTime >= startTime`、单设备条数上限见 **`DevicePresence:QueryMaxEvents`**，默认 10000）。  
- 设备不存在或已软删 → **404**；超限 → **400**。

**SignalR**（`/hubs/positioning`，**Bearer**）：服务器方法名 **`DevicePresenceEvent`**，参数顺序：

`deviceId`（Guid）, `eventKind`（short，`0`/`1`）, `occurredAtUtc`（`DateTime`，UTC）。  
向**当前已连接 Hub 的全部客户端**推送（与楼层订阅独立；管理端可据此刷新列表「在线」列）。

