# MVP 交付范围与实施顺序（零歧义开工）

> 版本：1.0 | 与 `design-spec.md`、`api-conventions.md` 配套；后续迭代在本文件追加 Phase 2+。

**客户 / 培训**：在理解「范围」与「不在范围」前，若需**上机照做**（登录、Swagger、RSSI、自测表），请先看 [**`customer-learning.md`**](customer-learning.md)；本文件不重复操作步骤。

---

## 1. 目标（MVP Definition of Done）

在**本地 docker-compose** 下可重复执行：

1. `docker compose up` 启动 postgres、redis、（rabbitmq、seq 按 compose）、**API**。
2. API 暴露 **`GET /health`** 与 **`GET /health/ready`**（路径与行为以 `api-conventions.md` §5 为权威，**不**放在 `/api/v1` 下）；liveness **200** + `{ "status": "Healthy" }`；readiness 失败 **503** + ProblemDetails。
3. 执行 EF 迁移后，数据库存在 **§2.4.2** 表结构（含 **`tracked_devices.api_key_hash`**）。
4. **管理端 JWT** 可登录（或种子用户）并 **CRUD 至少一层 Floor + 该层 Beacons**（最小写路径；**REST API** 与 **Blazor Admin** 均已提供写能力，见根目录 `README.md` 与 `design-spec.md` §3.4）。
5. **创建设备**后得到 **明文 API Key 仅一次**；使用该 Key 调用 **`POST /api/v1/rssi/report`** 返回 **202**；**500ms 内** Redis 可读到 `pos:{deviceId}`（集成测试可放宽轮询间隔）。
6. **`GET /api/v1/devices/{id}/position`**（JWT）返回 **§api-conventions** 成功信封 + 与 Redis 一致的数据（或 404 规则见 api-conventions）。

满足以上即 **MVP 里程碑关闭**。

---

## 2. 实施顺序（行业常见：风险前置 + 垂直切片）

| 顺序 | 交付物 | 说明 |
|------|--------|------|
| P0 | 解决方案骨架、`docker-compose.yml`、`BlePositioning.*` 项目、**CI 可 `dotnet build`** | 见 `packages.md` |
| P1 | PostgreSQL 迁移（DDL §2.4.2）、**snake_case**、种子数据（可选） | ADR-005、ADR-007 |
| P2 | **全局异常 + ProblemDetails**、**成功信封**、**TraceId**（见 `api-conventions.md`） | 所有后续 API 依赖 |
| P3 | **JWT** 签发/刷新、**ApiKey** 认证、`tracked_devices` 写入 hash | ADR-006、§2.4.2.1 |
| P4 | **Channel + `PositioningPipelineService`**、定位（路径损耗 + 质心/裁剪 + **α 平滑**；**可选** `KalmanFilter2DMath` + Redis/内存状态，见阶段 F）、**Redis `pos:`** | ADR-002、ADR-004、ADR-008 |
| P5 | **`ITrajectoryBulkWriter`**（Npgsql COPY）、管道批量刷轨迹 | ADR-003 |
| P6 | **SignalR Hub**、`JoinFloor`、推送 `PositionUpdated` | 与 design-spec 一致 |
| P7 | **Blazor Admin**：楼层 + 信标列表与 **CRUD UI**（亦可通过 API/Swagger） | 可晚于 P4 |

**RabbitMQ / MassTransit**：MVP **不强制**实现消费者；compose 保留服务即可（ADR-009）。

---

## 3. 明确不在 MVP 范围内（Out of Scope）

以下**不得**作为 MVP 阻塞项；若实现须单独开 Phase：

- **独立 Web Dashboard** 仓库或项目（监控大屏）；角色保留在愿景中，**MVP 不交付**第三前端。
- **生产 K8s** 清单、HPA、Patroni（仅保留 `k8s/` 占位或后续 PR）。
- **工单/审批级围栏告警闭环**（当前已有 `alert_rules`、**`geofence_events`**、可选 **Webhook** 出站，见 `implementation-phases` C–D；不含工单系统）。
- **卡尔曼 Redis 外置**：**已实现为可选配置**（**`Positioning:StoreKalmanStateInRedis`**，阶段 F；默认 `false`，单实例可用进程内 `InMemoryKalmanStateStore`，见 ADR-008）。
- **MAUI** 完整 BLE 体验可与 API **并行**，不作为 API MVP 门禁。

---

## 4. 变更与细化流程（行业最佳实践）

- **架构 / 契约变更**：先改 **`design-spec.md`** 或 **`api-conventions.md`**，再改代码；评审以文档为权威。
- **依赖版本**：以 **`packages.md`** 为权威；升级走 PR 并更新该文件。
- **安全与密钥**：仅存 **hash**；**pepper** 使用 User Secrets / 环境变量，**不入库**。

---

## 5. 文档索引（AI 与人工必读顺序）

1. `doc/README.md` — 本索引  
2. `doc/design-spec.md` — ADR、DDL、管道、SLA  
3. `doc/api-conventions.md` — HTTP、错误体、信封、健康检查  
4. `doc/mvp-scope.md` — MVP 与实施顺序（本文件）  
5. `doc/packages.md` — NuGet 清单  
6. `doc/coding-standards.md` — 目录、DI、EF、Redis、SignalR  
7. `doc/database-selection.md` — 连接与本地 PG  
8. `CLAUDE.md` — 上下文总览  
9. `doc/.cursorrules` — Cursor 生成约束  
