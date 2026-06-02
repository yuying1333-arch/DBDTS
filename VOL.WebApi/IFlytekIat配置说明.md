# 讯飞语音听写（流式版）配置说明

## 配置文件位置

- **开发环境**：`appsettings.Development.json`（需设置 `ASPNETCORE_ENVIRONMENT=Development`）
- **主配置**：`appsettings.json`（密钥为空时从 Development 合并）

## 必填配置

```json
"IFlytekIat": {
  "AppId": "从讯飞控制台获取",
  "APIKey": "从讯飞控制台获取",
  "APISecret": "从讯飞控制台获取"
}
```

## 讯飞控制台检查

1. 登录 [讯飞开放平台](https://console.xfyun.cn/)
2. 创建/选择 **WebAPI 平台应用**
3. 在应用中添加 **「语音听写（流式版）」** 服务（必须，非普通听写）
4. 在服务详情页获取 AppID、APIKey、APISecret（均为 32 位）

## 音频格式要求

- 采样率：**16k 或 8k**
- 位长：**16bit**
- 声道：**单声道**
- 格式：PCM、WAV、MP3（MP3 仅支持中文普通话和英文）

录音设备若为 44.1k/48k，需先转换为 16k 才能正确识别。

## 调试模式

在 `appsettings.Development.json` 中设置 `"Debug": "true"`，可在控制台看到讯飞返回的原始 JSON，便于排查。

## 常见问题

| 现象 | 可能原因 |
|------|----------|
| 未配置讯飞语音听写 | 未设置 `ASPNETCORE_ENVIRONMENT=Development` 或 appsettings 中密钥为空 |
| 未识别到文字 | 1) 音频采样率非 16k/8k 2) 控制台未添加「语音听写流式版」 3) IP 白名单限制 |
| 鉴权失败 | APIKey/APISecret 错误，或应用未开通流式版服务 |
