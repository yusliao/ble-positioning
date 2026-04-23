# 关系型数据库选型与部署假设（PostgreSQL）

> 版本：1.3 | 与 `design-spec.md` 中 **ADR-005** 及 ADR-006～009 配套使用；契约见 `api-conventions.md`、`mvp-scope.md`  
> **已确定**：开发与生产环境均使用 **PostgreSQL 16+**。

---

## 1. 结论摘要（给评审用）

- **定位算法、Redis、SignalR、消息队列与数据库品牌无关**；本项目在关系型存储上 **统一为 PostgreSQL**，降低本地与生产环境漂移。
- **轨迹高写入**：通过 **`ITrajectoryBulkWriter`** 默认实现 **`NpgsqlTrajectoryBulkWriter`（`COPY` / Binary import）**，不使用 EF Core `SaveChanges` 热路径。
- **EF Core**：使用 **Npgsql.EntityFrameworkCore.PostgreSQL** 与对应迁移；DDL 以 `design-spec.md` **§2.4.2** 为准。
- **SQL Server**：文档 **§2.4.3** 仅保留语义对照，**不作为当前实现与 Docker 默认**。

---

## 2. 选型检查清单（归档 / 变更时再填）

以下问题在「从 PG 迁出或引入第二套数据库」时重新评估即可：

| 编号 | 问题 | 选项 | 备注 |
|------|------|------|------|
| Q1 | 主要生产云 / 机房 | Azure / AWS / GCP / 私有 IDC / 混合 | 决定托管 PostgreSQL SKU |
| Q2 | 托管产品 | RDS / Azure Database for PostgreSQL / Cloud SQL / 自建 Patroni 等 | 与备份、PITR、监控告警对齐 |
| Q3 | 高可用形态 | 单实例 / 多 AZ / 读写分离 | 与 `design-spec.md` K8s 表资源估算联动 |

---

## 3. 典型托管对照（PostgreSQL）

| 部署画像 | 常见托管选项 | 说明 |
|----------|----------------|------|
| AWS | Amazon RDS for PostgreSQL / Aurora PostgreSQL | 与 COPY 批量写入、分区维护脚本兼容 |
| Azure | Azure Database for PostgreSQL | 与 .NET 生态集成方便 |
| GCP | Cloud SQL for PostgreSQL | |
| 私有 IDC | 自建 PG + Patroni / 厂商集群 | 需自建备份、监控与分区自动化 |

---

## 4. 与实现文档的对应关系

| 主题 | 文档位置 |
|------|----------|
| ADR-001（Clean Architecture + 轻量 CQS） | `design-spec.md` §1；`coding-standards.md` §1～§2 |
| ADR-005（已锁定 PostgreSQL） | `design-spec.md` §1 |
| ADR-006（RSSI：`X-Api-Key` + 方案 `ApiKey`） | `design-spec.md` §1；`coding-standards.md` §12 |
| ADR-007（snake_case 物理名与 EF） | `design-spec.md` §1；`coding-standards.md` §5.2；`CLAUDE.md` EF 约定 |
| ADR-008（卡尔曼：单实例内存 / 多实例 Redis） | `design-spec.md` §1；`CLAUDE.md` 定位算法、Redis 键 |
| ADR-009（RabbitMQ 首版边界） | `design-spec.md` §1、§5 |
| **PostgreSQL DDL**（权威） | `design-spec.md` §2.4.2 |
| SQL Server DDL（对照，不采用） | `design-spec.md` §2.4.3 |
| `ITrajectoryBulkWriter` 与 `NpgsqlTrajectoryBulkWriter` | `coding-standards.md` §2、§5.1、附录 |
| AI 技术栈表 | `CLAUDE.md` |
| MVP 范围与实施顺序 | `mvp-scope.md` |
| HTTP 成功/错误体与健康检查 | `api-conventions.md` |
| NuGet 基线 | `packages.md` |

---

## 5. 本地开发建议

- **Docker Compose**：使用 **`postgres:16-alpine`（或组织标准镜像）**，与 `design-spec.md` §5 示例一致。
- **连接字符串（Npgsql）**：  
  `Host=postgres;Port=5432;Database=blepositioning;Username=postgres;Password=<本地密码>`  
  密码勿提交仓库；团队可用 **User Secrets** / **.env（不纳入版本控制）** 覆盖示例中的 `Dev_Postgres_ChangeMe`。
- **分区**：本地可只建 **当前月 + 下一月** 子分区即可跑通；完整自动化见运维文档（与 90 天保留策略对齐）。
