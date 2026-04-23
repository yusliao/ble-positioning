# 文档索引（BLE 室内定位）

## 客户 / 培训 / 自学（优先）

| 文件 | 内容 |
|------|------|
| [**customer-learning.md**](./customer-learning.md) | **分步动手实践**、JWT 与角色、Swagger、RSSI→位姿、自测表与排障 |
| [demo-runbook.md](./demo-runbook.md) | 会议演示 **主线** 15–20 分钟 + **§8 扩展**（8～12 分钟，选做） |
| [README.md](../README.md) | 本地与 Docker 启动、端口、脚本入口 |

---

按 **设计与实现阅读顺序**（开工与 Code Review 均适用）：

| 顺序 | 文件 | 内容 |
|------|------|------|
| 1 | [design-spec.md](./design-spec.md) | ADR、DDL、定位管道、API 路径与 SLA |
| 2 | [api-conventions.md](./api-conventions.md) | **HTTP 契约权威**：成功信封、ProblemDetails、`/health`、`X-Trace-Id`、RSSI 安全、**§8 JWT**、**§9 围栏规则**、**§10 进/出区**、**§11 Webhook**、**§12 在线/离线** |
| 3 | [mvp-scope.md](./mvp-scope.md) | MVP 定义、实施顺序 P0–P7、Out of scope |
| 4 | [packages.md](./packages.md) | NuGet 基线 |
| 5 | [coding-standards.md](./coding-standards.md) | 目录结构、DI、EF、Redis、SignalR、安全 |
| 6 | [database-selection.md](./database-selection.md) | PostgreSQL 选型与本地连接 |
| 7 | [CLAUDE.md](./CLAUDE.md) | AI 总上下文、领域模型摘要 |
| 8 | [implementation-phases.md](./implementation-phases.md) | **全量分阶段实现路线图、DoD、阶段交接与省 token 提示** |
| — | [demo-runbook.md](./demo-runbook.md) | 客户演示：环境、主线、**扩展时间盒**、故障处理 |
| — | [demo-seed.md](./demo-seed.md) | 演示种子数据与坐标约定（无密钥） |
| — | [demo-rehearsal-checklist.md](./demo-rehearsal-checklist.md) | 口播与计时：**主线 / 扩展** 分表 |
| — | [.cursorrules](./.cursorrules) | Cursor 生成约束 |

**冲突处理**：同一主题以 **列表中靠上** 的文件为优先（**`api-conventions.md` 优先于 `design-spec.md` §3 中关于 HTTP 形状的叙述**）。

**与当前代码同步（MVP+，含阶段 C–F）**：楼层与信标 **REST CRUD**、**`alert_rules` CRUD**、**`geofence_events`**、**`device_presence_events` 与在线判定**（见 `api-conventions` §9–§12）、**可选 Webhook 出站**（§11）、**可选卡尔曼平滑**（`Positioning:*`，**`design-spec` §2.3 / ADR-008**）、**`GET /api/v1/devices`**（**`isOnline`**）、**Blazor Admin**、信标部分唯一索引、**轨迹** 与 **RSSI 管道** 已反映于根 `README.md`、`design-spec`（§3.3–§3.6）、`mvp-scope.md`、`api-conventions.md`、**[`customer-learning.md`](./customer-learning.md)**。
