# 生产上线检查表（v1）

> 阶段 G 交付物。部署前由运维/开发共同勾选；与 `doc/security-operations.md` 配套。

## 1. 密钥与身份

- [ ] **`Jwt:SigningKey`**：≥32 字符随机串，经环境变量/密钥管理服务注入，**不**写入镜像或 git
- [ ] **`ApiKey:Pepper`**：独立随机串；轮换见 `security-operations.md`
- [ ] **禁用 `DevAdmin` 明文账号**：生产使用 IdP 或专用用户表（当前版本若仍用配置用户，须改强密码并限制网络）
- [ ] **PostgreSQL / Redis 密码**：非默认；连接串仅环境变量
- [ ] **创建设备 API Key**：仅 HTTPS 传输；客户端安全存储

## 2. 网络与暴露面

- [ ] **仅 HTTPS** 对外（反向代理终止 TLS）；`SecurityHeaders:UseHsts=true`（`appsettings.Production.json` 默认）
- [ ] **关闭公开 Swagger**（`ASPNETCORE_ENVIRONMENT=Production`，勿用 Development 编排）
- [ ] **Admin / API** 经防火墙或内网；SignalR CORS 收紧为已知 Admin 源（非 `IsDevelopment` 全开）
- [ ] **Redis / PostgreSQL** 不对公网暴露

## 3. 限流与防护

- [ ] **`RateLimiting:Enabled=true`**（默认）：RSSI **10 req/s/设备**；普通 API **60 req/min/用户或 IP**
- [ ] 确认 **`POST /api/v1/rssi/report`** 仅 `X-Api-Key`，且 `deviceId` 与 Key 绑定（契约 §6）
- [ ] 反向代理层可选 WAF / 额外限流（登录暴力等）

## 4. 数据与迁移

- [ ] 启动前备份；`MigrateAsync` 或受控迁移作业已演练
- [ ] `position_logs` 分区与保留策略（归档/TTL）已规划
- [ ] 地图上传目录 `maps/` 持久卷与备份

## 5. 可观测性

- [ ] **Serilog** 输出到集中日志（非仅 Console）
- [ ] **`GET /health`**（存活）、**`GET /health/ready`**（PG+Redis）已接入探针
- [ ] **`/metrics`**（Prometheus）仅内网抓取
- [ ] 告警：ready 失败、429 激增、定位管道错误率

## 6. 冒烟（上线后 15 分钟内）

- [ ] `GET /health` → 200
- [ ] `GET /health/ready` → 200
- [ ] JWT 登录 → `GET /api/v1/floors` → 200
- [ ] 创建设备 → `POST /api/v1/rssi/report`（有效 Key）→ **202**
- [ ] `GET /api/v1/devices/{id}/position` → 200 或合理 404（无信标时）
- [ ] （可选）`pwsh -File scripts/verify-mvp.ps1` 或 `demo-simulate-rssi.ps1`

## 7. 回滚

- [ ] 上一版本镜像/配置可快速切回
- [ ] 数据库迁移可逆或已备回滚脚本
- [ ] Redis 键 TTL 设计允许短暂双版本共存（`pos:`、`gfstate:`、`dpl:`）

---

**签署**：环境 ______ | 日期 ______ | 执行人 ______
