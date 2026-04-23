# 演示用种子数据（手填、不入库密文）

本文件**不包含**任何真实 API Key。仅说明推荐字段与**坐标约定**，与 [`demo-simulate-config.example.json`](demo-simulate-config.example.json) 一起使用。

## 坐标约定（与「实时地图」页一致）

为让地图上红点与信标/算法坐标一致，演示请在 Admin 中采用：

- **X**：自楼层区域**左**边界向右为正（米）。  
- **Y**：自楼层区域**上**边界向下为正（米），与常见建筑平面图/网页图片 `img` 左上角为原点、向下为增一致。

若历史数据曾用「Y 自地面向上」数学系，需换算后再录入或只用于后台对比，**本演示以自上而下为准**。

## 推荐手顺（最短）

1. 创建楼层：例如 20m × 10m，上传任意平面图（便于观众理解；无图也可演示数值）。  
2. 设 3 个信标（UUID/Major/Minor 需与 `demo-simulate-config` 中一致），坐标示例可沿用 example JSON 中三点。  
3. 创建设备，保存 Key，将 **deviceId**、**apiKey** 填入 `demo-simulate-config.json`（本地、勿提交）。  
4. 将 `apiBase` 设为浏览器可访问的 API 根（compose 下通常为 `http://localhost:5000`）。  
5. 启模拟脚本后，在 **实时地图** 选同一楼层与设备。

## 与脚本的 `path` 段

- `from` / `to` 为上述坐标系下的 (X,Y) 米值，模拟沿直线往返移动，驱动 RSSI 变化。  
- 将 `roundTripSeconds` 调大可使点移动更慢、便于口播。
