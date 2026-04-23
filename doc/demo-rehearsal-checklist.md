# 演示彩排清单（口播 + 时间）

> 与 [`demo-runbook.md`](demo-runbook.md) 配合：会前用 Runbook 搭环境，会前 15 分钟用本表走**主线**；有客户关心再备课 **「扩展」** 段。

## A. 主线（15～20 分钟）

| 时间 | 步骤 | 口播要点 |
|------|------|----------|
| 0:00–1:00 | 打开 Admin、登录 | 管理端用 JWT，生产可接 IdP。 |
| 1:00–4:00 | 楼层：名称、宽高(米)、上传地图 | 尺寸与信标/位姿的**米制**坐标系一致。 |
| 4:00–7:00 | 信标：至少 3 个，配 UUID/Major/Minor 与 (X,Y) | 与真机标签一致时即可接真实信号；**合成 RSSI** 演示不依赖硬件。 |
| 7:00–8:00 | 设备：新建设备，**立刻保存并复制 API Key** | 密钥仅一次、仅存**哈希**；**设备面**用 `X-Api-Key` 上 RSSI。 |
| 8:00–9:00 | 设备表看 **「在线」** 列（有 RSSI 后） | **在线**= Redis 里 `pos:设备` 未过期，与 `PositionTtlSeconds` 一致，见 `api-conventions` §12。无上报时为否。 |
| 9:00–10:00 | 终端跑 `scripts/demo-simulate-rssi.ps1` | 无蓝牙硬件，按路径损耗反推合成 RSSI。 |
| 10:00–14:00 | **实时地图**，选楼层+设备，看点移动 | 轮询 + 可讲同楼层 **SignalR** `PositionUpdated`（`api-conventions` §8）。 |
| 14:00–16:00 | （可选）**设备轨迹**页，选时间窗 | 落库 `position_logs`、时间桶聚合，审计与回溯。 |
| 16:00–20:00 | Q&A 预留 | 见下 **Q&A 备忘**；不展开进区/Webhook/卡尔曼 除非客户问。 |

**预计主线合计**：**约 16～20 分钟**（可压缩信标/轨迹/「在线」口播 1 分钟）。

## B. 扩展（选做，8～12 分钟，与 Runbook §8 对齐）

> 在主线 A 已跑通后，按客户兴趣**从表中选行**，不必全做；**合计**可压在 **8～12 分钟**（若不讲「等 TTL 看离线」可再省 1～2 分钟）。

| 时间盒 | 步骤 | 口播要点 |
|--------|------|----------|
| **0:00–2:30** | 楼层行 **「围栏」** → 加一条**启用**规则；`zonePolygon` **包住**模拟轨迹；保持 RSSI 跑 | 边沿时落库 `geofence_events`；**Swagger** 查 `GET .../geofence-events`；Hub **`GeofenceEvent`** 在 `PositionUpdated` 之后。 |
| **2:30–4:30** | 停掉模拟；口播**超过 TTL 后**在线变否；或**会后**自证 | 另有 **`device_presence_events`** 与 `GET .../presence-events`；Hub 有 **`DevicePresenceEvent`（广播）**（§12）。**会议内可不真等 60s**。 |
| **4:30–6:00** | 技术听众可接 SignalR 看**多方法名** | 除 `PositionUpdated`、`GeofenceEvent` 外，全连接可见 **`DevicePresenceEvent`**。 |
| **6:00–8:00** | **Webhook** 有准备时：开 `GeofenceWebhook:Enabled`，展示收到 POST 与 HMAC 头 | 尽力而为、失败不回滚；**无准备就跳过**（§11）。 |
| **8:00–10:00** | **默认**质心+α 平滑；**可选**开 `Positioning:UseKalmanFilter` 与多实例时 **Redis 卡尔曼状态** | 改配置需**重启**；`design-spec` §2.3、ADR-008。 |

**扩展后总时长**：**约 25～32 分钟**（主线 + 扩展 + 短 Q&A）。只加 **「围栏 + Swagger 查一条进区」** 约 **+4 分钟**。

## Q&A 备忘

1. **精度与算法**：**默认**为加权**质心** + **一阶低通**；可在配置中**可选** **2D 卡尔曼**与（水平扩展时）**Redis 外置**滤波状态，仍要说明非室外 GNSS 级、MVP+ 不承诺产线 NLOS 补偿。详见 [`design-spec.md`](design-spec.md) §2.3、ADR-008 与 `PositioningOptions`。  
2. **安全与隐私**：设备 Key 仅存哈希、管理查询走 **JWT**；进区/在线等**事件**同样受 `api-conventions` 约束。生产需 **HTTPS、密钥轮换、保留策略**（阶段 G 可补 Runbook 级检查表）。  
3. **集成与消息**：RSSI 热路径**不**经 Rabbit（ADR-002/009）。**进区/出站**可对事件 URL 做 **HTTP Webhook**（§11，可选 HMAC）；**Rabbit / MassTransit** 仍为**后续**集成方式，可对接出站队列等，见 [`mvp-scope.md`](mvp-scope.md) 与 `implementation-phases` 阶段 D 备注。
