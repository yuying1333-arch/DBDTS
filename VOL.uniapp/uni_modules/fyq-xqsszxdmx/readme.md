# fyq-xqsszxdmx 讯飞实时录音转写大模型

基于讯飞 SparkChain SDK 的实时录音转写大模型插件，支持 Android SDK 和 iOS WebSocket 方式。通过麦克风实时采集音频，将语音转写为文本，适用于语音输入、语音笔记等场景。

**版本：** 1.0.1

## 平台支持

| 平台 | 最低版本 |
|------|----------|
| Android | API 21（Android 5.0） |
| iOS | 13.0 |

- HBuilderX >= 3.6.8
- uni-app >= 5.05

## 导入方式

本插件为 UTS 插件，在 uni-app（非 uni-app x）项目中，通过 ES module 方式导入：

```javascript
import { checkAudioPermissions, startRecording, stopRecording } from '@/uni_modules/fyq-xqsszxdmx'
```

> **注意：** `uni.checkAudioPermissions()`、`uni.startRecording()` 等 `uni.xxx()` 形式仅在 uni-app x 项目中可用，普通 uni-app 项目请使用上述 import 方式。

## API 参考

### checkAudioPermissions(options)

检查录音权限状态。

**参数（CheckPermissionsOptions）：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| success | `(res: CheckPermissionsResult) => void` | 否 | 成功回调 |
| fail | `(res: any) => void` | 否 | 失败回调 |
| complete | `(res: any) => void` | 否 | 完成回调 |

**success 回调参数（CheckPermissionsResult）：**

| 字段 | 类型 | 说明 |
|------|------|------|
| code | number | 权限状态码，见下表 |
| msg | string | 状态描述 |

**权限状态码（code）：**

| 值 | 说明 |
|----|------|
| 0 | 已授权录音权限 |
| 1 | 录音权限未授权（可请求授权） |
| 2 | 录音权限已被拒绝，需用户手动在设置中开启 |

---

### startRecording(options)

开始实时录音转写。Android 端基于讯飞 SparkChain SDK，iOS 端基于 WebSocket 连接。

**参数（StartRecordingOptions）：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| appId | string | 是 | 讯飞开放平台 appId |
| apiKey | string | 是 | 讯飞 APIKey（accessKeyId） |
| apiSecret | string | 是 | 讯飞 APISecret（accessKeySecret） |
| lang | string | 否 | 语言类型，默认 `"autodialect"`（自动识别方言） |
| onTextReceived | `(text: string, isFinal: boolean) => void` | 否 | 转写文本回调 |
| onError | `(errMsg: string) => void` | 否 | 错误回调 |
| success | `(res: StartRecordingResult) => void` | 否 | 成功回调 |
| fail | `(res: any) => void` | 否 | 失败回调 |
| complete | `(res: any) => void` | 否 | 完成回调 |

**onTextReceived 回调参数：**

| 参数 | 类型 | 说明 |
|------|------|------|
| text | string | 转写得到的文本片段 |
| isFinal | boolean | `true` 表示最终确认结果，`false` 表示中间临时结果 |

**音频格式：** PCM 16-bit little-endian，单声道，采样率 16000Hz。

---

### stopRecording(options)

停止当前录音并断开转写连接。

**参数（StopRecordingOptions）：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| success | `(res: StopRecordingResult) => void` | 否 | 成功回调 |
| fail | `(res: any) => void` | 否 | 失败回调 |
| complete | `(res: any) => void` | 否 | 完成回调 |

## 错误码

| 错误码 | 说明 |
|--------|------|
| 9040001 | 录音权限被拒绝 |
| 9040002 | 录音初始化失败 |
| 9040003 | SDK 初始化失败 |
| 9040004 | 转写启动失败 |
| 9040005 | 音频写入失败 |
| 9040006 | 未在录音状态 |

错误通过 `fail` 回调返回，格式为 `{ errCode: number, errMsg: string }`。运行时错误通过 `onError` 回调以字符串形式返回。

## 完整示例

```vue
<template>
  <view>
    <button @click="handleVoiceTap">
      {{ isRecording ? '停止录音' : '开始录音' }}
    </button>
    <textarea v-model="inputText" placeholder="语音转写结果..." />
  </view>
</template>

<script>
import { checkAudioPermissions, startRecording, stopRecording } from '@/uni_modules/fyq-xqsszxdmx'

export default {
  data() {
    return {
      isRecording: false,
      inputText: '',
      confirmedText: ''
    }
  },
  beforeDestroy() {
    // 组件销毁时停止录音，避免资源泄漏
    if (this.isRecording) {
      this.stopVoice()
    }
  },
  methods: {
    handleVoiceTap() {
      if (this.isRecording) {
        this.stopVoice()
      } else {
        this.startVoice()
      }
    },
    startVoice() {
      // 重置文本
      this.inputText = ''
      this.confirmedText = ''

      // 检查权限
      checkAudioPermissions({
        success: (res) => {
          if (res.code === 2) {
            uni.showToast({
              title: '录音权限已被拒绝，请在设置中开启',
              icon: 'none'
            })
            return
          }
        }
      })

      this.isRecording = true

      // 开始录音转写
      startRecording({
        appId: 'your_app_id',
        apiKey: 'your_api_key',
        apiSecret: 'your_api_secret',
        lang: 'autodialect',
        onTextReceived: (text, isFinal) => {
          if (isFinal) {
            // 最终结果：累积到已确认文本
            this.confirmedText += text
            this.inputText = this.confirmedText
          } else {
            // 中间结果：已确认文本 + 当前识别内容
            this.inputText = this.confirmedText + text
          }
        },
        onError: (errMsg) => {
          console.error('转写错误:', errMsg)
          uni.showToast({ title: errMsg, icon: 'none' })
          this.isRecording = false
        },
        success: (res) => {
          console.log('录音转写已启动:', res)
        }
      })
    },
    stopVoice() {
      if (!this.isRecording) return
      this.isRecording = false
      this.confirmedText = ''
      stopRecording({
        success: (res) => {
          console.log('停止录音:', res)
        }
      })
    }
  }
}
</script>
```

## 注意事项

1. **生命周期管理** — 在组件的 `beforeDestroy` 中必须调用 `stopRecording()` 停止录音，否则会导致录音资源无法释放、回调泄漏等问题。建议同时监听应用隐藏事件（如 `uni.$on('app-hide', ...)`），在应用切到后台时也停止录音。

2. **权限配置**
   - **Android：** 插件已通过 `AndroidManifest.xml` 自动声明 `RECORD_AUDIO` 和 `INTERNET` 权限，无需手动配置。
   - **iOS：** 需要在项目的 `Info.plist` 中配置 `NSMicrophoneUsageDescription`（麦克风使用说明），否则应用会崩溃。示例：
     ```xml
     <key>NSMicrophoneUsageDescription</key>
     <string>需要使用麦克风进行语音转写</string>
     ```

3. **录音状态** — 插件内部维护全局录音状态，同一时间只能有一个录音会话。在未停止当前录音时再次调用 `startRecording` 会导致未定义行为。

4. **累积文本模式** — `onTextReceived` 回调中 `isFinal` 为 `false` 时返回的是临时中间结果（可能被修正），`isFinal` 为 `true` 时返回的是最终确认结果。推荐使用"累积文本"策略：将 `isFinal === true` 的文本追加到已确认文本，将 `isFinal === false` 的文本作为实时预览拼接显示。
