# 全量实现路线图与阶段交接

> 目的：在 **本仓库** 内分阶段把「MVP+」补全到可交付的完整产品形态；**每阶段结束** 更新本文件与 `doc/session-checkpoint.md`，**新开聊天** 时只把「当前阶段 + 本文件 + checkpoint」给 AI 即可，减少 token。

## 「完整实现」的边界（避免无限范围）

- **在仓库内做**：与 `design-spec.md` / `api-conventions.md` 一致的 API、Admin、域服务、EF、测试、CI、运维说明。
- **不强制在本仓库**（与 `mvp-scope.md` 一致，除非业务强制）：独立「监控大屏」新前端仓库、**MAUI** 完整 BLE 应用、**生产 K8s 全套**（可只做占位+文档）。
- **多实例/卡尔曼进 Redis** 见 **阶段 F**，在确实要水平扩 API 时再做，避免早优化。

## 各阶段总览

| 阶段 | 代号 | 目标（可验收） | 建议顺序（可并行时标注） |
|------|------|----------------|--------------------------|
| 0 | 基线 | 已具备：MVP 链路、演示脚本、RSSI 集成测、alert-rules 只读、无 Docker CI | 已完成，保持不回归 |
| A | 权限与账号 | 多角色 JWT（至少 Admin / Viewer）、策略与 Admin 端菜单/页面分级 | 建议最先（降低后续每阶段返工） |
| B | 围栏规则写与 Admin | 规则 CRUD、校验 `ZonePolygon`、启用/禁用、列表与楼层关联 | 依赖 A 写接口授权 |
| C | 进出区判定 + 事件 | 定位结果与多边形/围栏几何判定、**事件落库**、查询 API、SignalR/现有 Hub 可推送 | 核心业务能力 |
| D | 通知与消息 | Outbound：Webhook/邮件择一+抽象；**可选** MassTransit+Rabbit 消费者（与 ADR-009 对齐） | 可与 C2 分步 |
| E | 设备生命周期 | 在线/离线规则（RSSI/Redis TTL）、`DeviceOffline` 类事件、与 D 可联动 | 可部分早做 |
| F | 定位与多实例 | 卡尔曼、ADR-008 Redis 外置**或**单实例写清文档/配置开关 | 按部署需求启动 |
| G | 质量与交付 | 全 CI（含 docker 用例的 job 策略）、安全清单、性能基线、Runbook 生产版 | 收尾 |

---

## 阶段 0（基线）— 当前状态

- **状态**：**已完成**（以 `doc/session-checkpoint.md` 与测试条数为准）。
- **每版本完成后**：`dotnet test` 全绿；更新 `session-checkpoint` 日期与条数。

---

## 阶段 A：权限与账号安全

- **本阶段实现摘要（2026-04-23）**：`JwtTokenIssuer` 含 `role` claim；`DevAdmin:Users` 多账户 + 空表回退单一 Admin；`Floors`/`Devices` 写端点 `Admin` 授权；`LoginResponse` 增 `role`；Admin 按 `IsAdmin` 隐藏新建/编辑；测试见 `AuthorizationRolesApiTests` + 既有 docker 登录断言 `Role`。
- **目标**：`Role` 或等效 Claim；API `[Authorize(Roles=...)]` 与 **Admin 导航/按钮** 一致；`Viewer` 只读、无法创建设备/信标/规则。
- **主要改动**：`JwtTokenIssuer` 签发含 role；种子或配置多用户；`Program` / policy；Admin `AuthorizeView` 或重定向；测试：策略单测+集成。
- **文档**：`api-conventions.md` 增加认证小节；`session-checkpoint` 记阶段完成。
- **Out of scope 本段**：国密/双因子（可后补）。

**完成定义（DoD）**：

- [x] 至少两个角色、两条集成路径（只读/管理）有自动化覆盖或清晰手工清单。
- [x] 文档与 checkpoint 已更新（见下「每阶段必做」）。

---

## 阶段 B：围栏规则 CRUD + Admin

- **本阶段实现摘要（2026-04-23）**：`IFloorService` 增建/改/删；`ZonePolygonValidator` 校验 GeoJSON `Polygon`（闭合外环、米制 `[x,y]`）；`POST/PUT/DELETE` 在 `FloorsController` 上 **Admin**；**Admin** 页 `AlertRules.razor`（`/floors/{id}/alert-rules`）；测试 `AlertRulesCrudApiTests` + `ZonePolygonValidatorTests`；契约 **api-conventions §9**、**design-spec §3.5**。
- **目标**：`POST/PUT/DELETE` 等规则 API（与 `alert_rules` 一致），Admin 在楼层下管理规则，字段含名称、多边形/JSON 校验、启用、TriggerOn 枚举说明。
- **主要改动**：`FloorService` / 新 `IAlertService`、Controller、迁移若有新列、Blazor 页。
- **文档**：`design-spec` 或 `api-conventions` 中规则 DTO 形状；OpenAPI 描述。

**DoD**：

- [x] 规则可创建并能在 `GET .../alert-rules` 中列出；非法几何返回 400。
- [x] Admin 可操作；文档 + checkpoint 更新。

---

## 阶段 C：进出区判定 + 事件存储

- **本阶段实现摘要（2026-04-23）**：`geofence_events` 表 + `PointInPolygon` / `ZonePolygonRingParser`；`IGeofenceEvaluationService` 在管道内 **`PositionUpdated` 之后** 执行；`RedisGeofenceStateStore` + `GET /api/v1/devices/{id}/geofence-events`；Hub **`GeofenceEvent`**；测试 **`GeofenceEvaluationServiceTests`**（三位置跨边 → 2 条边沿）+ **`PointInPolygonTests`**。
- **目标**：每次定位更新后，与**本楼层**已启用规则做**点是否在多边形内**判定，产生**进/出区事件**（新表如 `geofence_events` 或约定现有表名），支持按设备/时间查询。
- **主要改动**：Application 服务从管道或缓存更新点调用**领域/几何**；**禁止**在 Controller 里堆几何；可抽 `IGeofenceEvaluator`；迁移。
- **文档**：事件契约、时序、与 `PositionUpdated` 关系。

**DoD**：

- [x] 集成测：3 点模拟移动跨越边界至少产生 1 条事件。
- [x] 文档 + checkpoint 更新。

---

## 阶段 D：通知与异步（Webhook → 可选 Rabbit）

- **本阶段实现摘要（2026-04-23）**：`GeofenceWebhook` 配置节；`HttpGeofenceWebhookPublisher` + `CompositeGeofenceEventPublisher`（与 `SignalRGeofenceEventPublisher` 串联）；HMAC 头 **`sha256=`**、5xx/408/网络重试、4xx 非重试；`GeofenceWebhookPublisherTests` + Composite 顺序用例。契约 **api-conventions §11**。MassTransit/Rabbit 未接（可选、ADR-009 后续）。
- **目标**：**先** 配置级 Web URL + 重试+签名（HMAC 可选）推送事件 JSON；**再** 可选接 MassTransit，把「必须送达」与「尽力而为」分开（ADR-009）。
- **文档**：环境变量、失败策略、与阶段 C 事件格式版本。

**DoD**：

- [x] 本地可收到测试 HTTP 容器的 Request；或单测 mock HttpClient 工厂。

---

## 阶段 E：设备在线/离线

- **本阶段实现摘要（2026-04-23）**：`pos:` 未过期即 **`isOnline`**（`PositionTtlSeconds`）；**`dpl:`** Redis 状态 + **`DevicePresenceSweeperService`**；表 **`device_presence_events`**；**`GET .../presence-events`**；Hub **`DevicePresenceEvent`**；配置 **`DevicePresence`** + 测试 **`DevicePresenceOptionsTests`**。契约 **api-conventions §12**。
- **目标**：统一「最后 RSSI/最后位置时间」、Redis TTL 与 `IsOnline` 一致；**离线事件**进入与 C 相同或并行通道。
- **文档**：`api-conventions` 中在线语义。

**DoD**：

- [x] 时间可调的配置项+测试。

---

## 阶段 F：定位算法与多实例

- **本阶段实现摘要（2026-04-23）**：**`KalmanFilter2DMath`**（2D 位置随机游走 + `IKalmanPositionFilter`）；**`Positioning:UseKalmanFilter`** / **`StoreKalmanStateInRedis`** / 噪声与 TTL；进程内 **`InMemoryKalmanStateStore`** 或 Redis **`device:{id}:kalman`**（**`KalmanStateTtlSeconds`**）；管道在质心+裁剪后分支（卡尔曼 vs 原 α 平滑）。**`KalmanFilter2DMathTests`**。单例 `IKalmanStateStore` 在启动时按配置二选一，**切换存储需重启进程**。
- **目标**：`KalmanFilter2D` 可插拔；`PositioningOptions` 开关；多实例时 Redis 存滤波状态（ADR-008）或文档明确**仅单实例**。
- **DoD**：

- [x] 可重复单元测试+配置说明。

---

## 阶段 G：质量与生产就绪

- **目标**：CI 策略（linux docker job / nightly）；安全（headers、限流、密钥轮换说明）；`README` 生产节；SLO/日志字段对齐。
- **DoD**：

- [ ] 团队认可的「可上线检查表」+ checkpoint 定稿为 v1。

---

## 每阶段结束后必做（减上下文、省 token）

1. **改 `doc/session-checkpoint.md`**：日期、本阶段 2～5 条要点、**当前全局测试数**、仍待大项。  
2. **改本文件** `doc/implementation-phases.md`：在对应阶段表下打勾 **DoD**，并写**一行本阶段实现摘要**（给下一个 AI/会话用）。  
3. **若改契约**（含 URL、DTO）：同步 `api-conventions.md` 或 `design-spec.md`（以约定为准）。  
4. **新开聊天前**：执行下方「**给下一段的提示语模板**」；在 Cursor 中 **新会话** 或按产品说明 **清上下文**。

### 给下一段的提示语模板（复制后替换尖括号内容）

```text
请阅读 D:\MyDomain\src\AI\ble-positioning\doc\implementation-phases.md
与 doc\session-checkpoint.md。当前要开发的是 <阶段 X：代号>。
本阶段目标是：<一句话>。请先列任务清单，再改代码；完成后更新上述两个文档
并勾选 DoD。不要扩大 Out of scope 范围。
```

---

## 建议的「下一步」

若尚未开始阶段 A，**下一段建议专注阶段 A（权限与账号）**；若业务强制「先有告警」，可 **B→A** 但会面临接口授权返工，仅在排期极紧时采用。
