# NuGet 包基线（权威清单）

> 版本：1.0 | 升级次要/补丁版本时更新本表；安全补丁应及时合并。

**原则**：仅添加**有明确用途**的包；版本以 **.NET 8** 兼容的稳定版为准（表中为撰写时的推荐族，实现时取 `dotnet add package` 解析的最新兼容版并锁 `Directory.Packages.props` 可选）。

---

## 1. 宿主与 API（`BlePositioning.API`）

| 包 | 用途 |
|----|------|
| `Microsoft.AspNetCore.OpenApi` | OpenAPI 文档（内置） |
| `Swashbuckle.AspNetCore` | Swagger UI（若需 UI） |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT 认证 |
| `Serilog.AspNetCore` | 请求日志与 enricher |
| `Serilog.Sinks.Console` | 控制台输出 |
| `Serilog.Sinks.Seq` | 对接 Seq（compose） |
| `prometheus-net.AspNetCore` | `/metrics`（与 design-spec 监控节一致） |

---

## 2. 数据访问（`BlePositioning.Infrastructure`）

| 包 | 用途 |
|----|------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core 8 + PostgreSQL |
| `EFCore.NamingConventions` | **`UseSnakeCaseNamingConvention()`**（ADR-007） |
| `Microsoft.EntityFrameworkCore.Design` | 迁移工具（通常 `PrivateAssets=all`） |
| `Dapper` | 可选只读查询加速（Application 约定） |
| `StackExchange.Redis` | Redis |
| `Npgsql` | 直接连接、`COPY` / `BeginBinaryImport`（`ITrajectoryBulkWriter`） |

---

## 3. 横切与测试

| 包 | 用途 |
|----|------|
| （内置） | 限流使用 **`Microsoft.AspNetCore.RateLimiting`**（.NET 8 共享框架），**无需**额外 NuGet |
| `xunit` / `xunit.runner.visualstudio` | 单元测试 |
| `FluentAssertions` | 断言 |
| `Moq` | 替身 |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory` 集成测试 |
| `Testcontainers.PostgreSql` / `Testcontainers.Redis` | 可选集成测试（推荐） |

---

## 4. 前端与移动（按需）

| 项目 | 包 |
|------|-----|
| `BlePositioning.Admin` | `Microsoft.AspNetCore.Components.WebAssembly`（模板自带） |
| `BlePositioning.Mobile` | 平台 workload；BLE 使用平台 API |

---

## 5. 明确首版不引用（除非启用 ADR-009 功能）

| 包 | 说明 |
|----|------|
| `MassTransit` / `MassTransit.RabbitMQ` | 首版可不引用；启用出站消息时再添加 |
