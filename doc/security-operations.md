# 安全运维：密钥轮换与配置

> 阶段 G。契约细节仍以 **`api-conventions.md`** 为准。

## 1. JWT 签名密钥（`Jwt:SigningKey`）

| 项 | 说明 |
|----|------|
| 存储 | 环境变量 `Jwt__SigningKey` 或密钥库 |
| 轮换 | 部署新密钥 → 重启所有 API 实例；旧 Token 在过期前仍有效（默认 `AccessTokenMinutes`） |
| 建议 | 生产 `AccessTokenMinutes` ≤ 15；Admin 依赖刷新或重新登录 |

## 2. API Key Pepper（`ApiKey:Pepper`）

| 项 | 说明 |
|----|------|
| 算法 | `SHA-256(pepper + plaintext)` hex，见 `design-spec` §2.4.2.1 |
| 轮换影响 | **更改 pepper 会使所有已存 hash 失效**；须计划：维护窗口 + 为每台设备重新签发 Key（`RotateApiKey` 或删建） |
| 流程 | 1）新 pepper 并行验证（若未来实现双 pepper）或 2）批量轮换设备 Key 后切换 pepper |

## 3. 设备 API Key

- 创建时明文 **仅返回一次**；丢失则 Admin 重新创建设备或实现轮换端点（当前 MVP 以新建为主）。
- 禁止将 Key 写入日志、演示脚本提交到 git（见 `demo-seed.md`）。

## 4. Webhook 签名（`GeofenceWebhook:Secret`）

- HMAC 头 `sha256=`；轮换时更新订阅方校验密钥，与 API 配置同日切换。

## 5. 限流（`RateLimiting`）

```json
"RateLimiting": {
  "Enabled": true,
  "RssiPermitLimit": 10,
  "RssiWindowSeconds": 1,
  "GeneralPermitLimit": 60,
  "GeneralWindowMinutes": 1
}
```

- 超限返回 **429** + ProblemDetails，可含 **`Retry-After`**（`api-conventions` §4）。
- 压测前可临时调高配额；生产勿长期关闭 `Enabled`。

## 6. 响应安全头（`SecurityHeaders`）

| 头 | 作用 |
|----|------|
| `X-Content-Type-Options: nosniff` | 降低 MIME 嗅探风险 |
| `X-Frame-Options: DENY` | 降低点击劫持 |
| `Referrer-Policy` | 控制 Referer 泄漏 |
| `Strict-Transport-Security` | 生产 HTTPS 下开启（`UseHsts: true`） |

## 7. 生产环境变量示例（勿照抄密钥）

```bash
ASPNETCORE_ENVIRONMENT=Production
Jwt__SigningKey=<from-secret-store>
ApiKey__Pepper=<from-secret-store>
ConnectionStrings__Default=Host=...;Password=...
ConnectionStrings__Redis=...
RateLimiting__Enabled=true
SecurityHeaders__UseHsts=true
```

## 8. 日志与隐私

- 生产 **Default** 级别 Information；勿在 Information 打印完整 RSSI 数组。
- ProblemDetails 的 `detail` 对用户模糊；`traceId` 用于关联日志。
