/**
 * 语音识别、图片识别
 * 当前语音默认采用录音文件上传转写模型。
 */
import { getBaseUrl, getToken } from './http.js'

const DEV_MODE = true // 与 http.js 一致：本地 HTTP 不上传 SSL 校验
const REALTIME_ASR_CONFIG_STORAGE_KEY = 'iflytek_realtime_asr_config'
const DEFAULT_REALTIME_ASR_CONFIG = {
	// 推荐只配后端 wsUrl，讯飞密钥统一放服务端
	wsUrl: ''
}

const realtimeState = {
	recording: false,
	finalText: '',
	lastTempFilePath: '',
	lastDurationSec: 0,
	recordStartedAt: 0,
	fileOnlyMode: false,
	options: null,
	socketTask: null,
	socketOpen: false,
	recorder: null,
	sendTimer: null,
	firstFrameTimer: null,
	recorderProfileIndex: 0,
	frameQueue: [],
	pcmBuffer: new Uint8Array(0),
	silentFrameCount: 0
}

export function setRealtimeAsrConfig(config = {}) {
	const payload = {
		wsUrl: String(config.wsUrl || '').trim()
	}
	uni.setStorageSync(REALTIME_ASR_CONFIG_STORAGE_KEY, payload)
	return payload
}

export function getRealtimeAsrConfig() {
	const saved = uni.getStorageSync(REALTIME_ASR_CONFIG_STORAGE_KEY) || {}
	return {
		wsUrl: String(saved.wsUrl || DEFAULT_REALTIME_ASR_CONFIG.wsUrl || '').trim()
	}
}

export function isRealtimeRecording() {
	return !!realtimeState.recording
}

function getSpeechWsUrl(explicitUrl) {
	const direct = String(explicitUrl || '').trim()
	if (direct) return direct
	const cfg = getRealtimeAsrConfig()
	if (cfg.wsUrl) return cfg.wsUrl
	let base = String(getBaseUrl() || '').trim()
	if (!base) return ''
	if (base.endsWith('/')) base = base.slice(0, -1)
	if (base.startsWith('https://')) return `wss://${base.slice('https://'.length)}/ws/speech`
	if (base.startsWith('http://')) return `ws://${base.slice('http://'.length)}/ws/speech`
	return `${base}/ws/speech`
}

function appendUint8(a, b) {
	const merged = new Uint8Array(a.length + b.length)
	merged.set(a, 0)
	merged.set(b, a.length)
	return merged
}

function calcPcm16Rms(frame) {
	if (!frame || frame.length < 2) return 0
	let sumSquares = 0
	let count = 0
	for (let i = 0; i + 1 < frame.length; i += 2) {
		let value = frame[i] | (frame[i + 1] << 8)
		if (value & 0x8000) value = value - 0x10000
		const normalized = value / 32768
		sumSquares += normalized * normalized
		count++
	}
	return count ? Math.sqrt(sumSquares / count) : 0
}

function clearSendTimer() {
	if (realtimeState.sendTimer) {
		clearInterval(realtimeState.sendTimer)
		realtimeState.sendTimer = null
	}
}

function clearFirstFrameTimer() {
	if (realtimeState.firstFrameTimer) {
		clearTimeout(realtimeState.firstFrameTimer)
		realtimeState.firstFrameTimer = null
	}
}

function closeSocket() {
	if (realtimeState.socketTask) {
		try {
			realtimeState.socketTask.close({ code: 1000, reason: 'client_stop' })
		} catch (e) {}
	}
	realtimeState.socketTask = null
	realtimeState.socketOpen = false
}

function resetRealtimeState() {
	realtimeState.recording = false
	realtimeState.finalText = ''
	realtimeState.options = null
	realtimeState.frameQueue = []
	realtimeState.pcmBuffer = new Uint8Array(0)
	realtimeState.silentFrameCount = 0
	realtimeState.recorderProfileIndex = 0
	realtimeState.recordStartedAt = 0
	realtimeState.fileOnlyMode = false
	clearSendTimer()
	clearFirstFrameTimer()
}

function getRecorderProfiles() {
	return [
		{ format: 'mp3', encodeBitRate: 64000, frameSize: 1 },
		{ format: 'wav', encodeBitRate: 128000, frameSize: 1 },
		{ format: 'aac', encodeBitRate: 64000, frameSize: 1 }
	]
}

function ensureRecordPermission() {
	// #ifndef APP-PLUS
	return Promise.resolve(true)
	// #endif
	// #ifdef APP-PLUS
	return new Promise((resolve, reject) => {
		if (typeof uni.authorize !== 'function') {
			resolve(true)
			return
		}
		uni.authorize({
			scope: 'scope.record',
			success: () => resolve(true),
			fail: () => reject(new Error('录音权限未授权，请在系统设置中开启麦克风权限'))
		})
	})
	// #endif
}

export function startRealtimeVoiceToText(options = {}) {
	if (realtimeState.recording) {
		return Promise.resolve({ status: true, message: 'already_recording' })
	}
	// #ifndef APP-PLUS
	return Promise.reject(new Error('实时语音转写当前仅支持 App（Android/iOS）'))
	// #endif
	// #ifdef APP-PLUS
	const useFileModel = !!options.useFileModel
	const wsUrl = useFileModel ? '' : getSpeechWsUrl(options.wsUrl)
	if (!useFileModel && !wsUrl) {
		return Promise.reject(new Error('未配置实时识别 wsUrl'))
	}

	return new Promise((resolve, reject) => {
		resetRealtimeState()
		realtimeState.options = options || {}
		realtimeState.fileOnlyMode = useFileModel
		const recorder = uni.getRecorderManager()
		realtimeState.recorder = recorder

		let started = false
		const onError = (message) => {
			const errText = String(message || '实时转写失败')
			if (typeof realtimeState.options.onError === 'function') {
				realtimeState.options.onError(errText)
			}
			if (!started) reject(new Error(errText))
		}

		ensureRecordPermission().then(() => {
			if (realtimeState.fileOnlyMode) {
				recorder.onStart(() => {
					realtimeState.recordStartedAt = Date.now()
					realtimeState.recording = true
					started = true
					if (typeof realtimeState.options.onLog === 'function') {
						realtimeState.options.onLog('文件转写模式：录音器已启动')
					}
					resolve({ status: true, message: 'recording_started_file_mode' })
				})
				recorder.onStop((res) => {
					realtimeState.lastTempFilePath = String((res && res.tempFilePath) || '').trim()
					const elapsed = realtimeState.recordStartedAt > 0 ? (Date.now() - realtimeState.recordStartedAt) / 1000 : 0
					const callbackDuration = Number((res && res.duration ? res.duration : 0))
					realtimeState.lastDurationSec = Math.max(elapsed, callbackDuration > 10 ? callbackDuration / 1000 : callbackDuration, 0)
					if (typeof realtimeState.options.onLog === 'function') {
						realtimeState.options.onLog(`文件转写模式：录音器已停止，文件=${realtimeState.lastTempFilePath || 'N/A'}`)
					}
				})
				recorder.onError((err) => {
					onError((err && err.errMsg) || '录音失败')
				})
				try {
					recorder.start({
						duration: 10 * 60 * 1000,
						sampleRate: 16000,
						numberOfChannels: 1,
						encodeBitRate: 64000,
						format: 'mp3'
					})
				} catch (e) {
					reject(e || new Error('启动录音失败'))
				}
				return
			}

			const socketTask = uni.connectSocket({
				url: wsUrl,
				header: {
					Authorization: getToken() || '',
					uapp: '1'
				},
				success: () => {}
			})
			realtimeState.socketTask = socketTask

			socketTask.onOpen(() => {
				realtimeState.socketOpen = true
				realtimeState.recording = true
				if (typeof realtimeState.options.onLog === 'function') {
					realtimeState.options.onLog('WebSocket已连接，准备启动录音')
				}
				const profiles = getRecorderProfiles()
				const startWithProfile = (idx) => {
					realtimeState.recorderProfileIndex = idx
					const profile = profiles[idx]
					if (!profile) {
						onError('未采集到录音数据，请确认麦克风权限已开启且未被其他应用占用')
						return
					}
					if (typeof realtimeState.options.onLog === 'function') {
						realtimeState.options.onLog(`尝试录音配置: format=${profile.format}, frameSize=${profile.frameSize}`)
					}
					try {
						recorder.start({
							duration: 10 * 60 * 1000,
							sampleRate: 16000,
							numberOfChannels: 1,
							encodeBitRate: profile.encodeBitRate,
							format: profile.format,
							frameSize: profile.frameSize
						})
					} catch (e) {
						startWithProfile(idx + 1)
						return
					}
					clearFirstFrameTimer()
					realtimeState.firstFrameTimer = setTimeout(() => {
						if (!realtimeState.recording || realtimeState.frameQueue.length > 0) return
						try {
							recorder.stop()
						} catch (e) {}
						startWithProfile(idx + 1)
					}, 3000)
				}
				startWithProfile(0)

				realtimeState.sendTimer = setInterval(() => {
					if (!realtimeState.socketOpen || !realtimeState.frameQueue.length) return
					const frame = realtimeState.frameQueue.shift()
					if (!frame) return
					socketTask.send({
						data: frame.buffer.slice(frame.byteOffset, frame.byteOffset + frame.byteLength),
						fail: (e) => onError((e && e.errMsg) || '音频帧发送失败')
					})
				}, 40)

				started = true
				resolve({ status: true, message: 'recording_started' })
			})

			socketTask.onMessage((res) => {
				try {
					const payload = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
					if (!payload || typeof payload !== 'object') return
					if (payload.type === 'result') {
						realtimeState.finalText = String(payload.text || '')
						if (typeof realtimeState.options.onText === 'function') {
							realtimeState.options.onText(realtimeState.finalText, true)
						}
					} else if (payload.type === 'error') {
						onError(payload.message || '服务端识别异常')
					} else if (payload.type === 'log' && typeof realtimeState.options.onLog === 'function') {
						realtimeState.options.onLog(String(payload.message || ''))
					}
				} catch (e) {}
			})

			socketTask.onError((err) => {
				onError((err && err.errMsg) || 'WebSocket连接失败')
			})

			socketTask.onClose(() => {
				realtimeState.socketOpen = false
				if (realtimeState.recording) {
					onError('WebSocket连接已关闭')
				}
			})

			recorder.onFrameRecorded((res) => {
				try {
					const chunk = new Uint8Array(res.frameBuffer)
					if (chunk.length > 0) {
						clearFirstFrameTimer()
					}
					if (chunk.length > 0) {
						realtimeState.frameQueue.push(chunk)
						if (typeof realtimeState.options.onLog === 'function') {
							realtimeState.options.onLog(`采集音频分片: ${chunk.length} bytes`)
						}
					}
				} catch (e) {}
			})

			recorder.onError((err) => {
				onError((err && err.errMsg) || '录音失败')
			})

			recorder.onStart(() => {
				realtimeState.recordStartedAt = Date.now()
				if (typeof realtimeState.options.onLog === 'function') {
					realtimeState.options.onLog('录音器已启动')
				}
			})

			recorder.onStop((res) => {
				realtimeState.lastTempFilePath = String((res && res.tempFilePath) || '').trim()
				const elapsed = realtimeState.recordStartedAt > 0 ? (Date.now() - realtimeState.recordStartedAt) / 1000 : 0
				const callbackDuration = Number((res && res.duration ? res.duration : 0))
				realtimeState.lastDurationSec = Math.max(elapsed, callbackDuration > 10 ? callbackDuration / 1000 : callbackDuration, 0)
				if (typeof realtimeState.options.onLog === 'function') {
					realtimeState.options.onLog(`录音器已停止，临时文件: ${realtimeState.lastTempFilePath || 'N/A'}，时长: ${realtimeState.lastDurationSec.toFixed(2)}秒`)
				}
			})
		}).catch((err) => {
			reject(err || new Error('录音权限校验失败'))
		})
	})
	// #endif
}

export function stopRealtimeVoiceToText() {
	if (!realtimeState.recording) {
		return Promise.resolve(String(realtimeState.finalText || '').trim())
	}
	// #ifndef APP-PLUS
	return Promise.resolve('')
	// #endif
	// #ifdef APP-PLUS
	if (realtimeState.fileOnlyMode) {
		return new Promise((resolve, reject) => {
			const recorder = realtimeState.recorder || uni.getRecorderManager()
			let handled = false
			const finish = (fn) => {
				if (handled) return
				handled = true
				realtimeState.recording = false
				fn()
			}
			recorder.onStop((res) => {
				realtimeState.lastTempFilePath = String((res && res.tempFilePath) || '').trim()
				const elapsed = realtimeState.recordStartedAt > 0 ? (Date.now() - realtimeState.recordStartedAt) / 1000 : 0
				const callbackDuration = Number((res && res.duration ? res.duration : 0))
				realtimeState.lastDurationSec = Math.max(elapsed, callbackDuration > 10 ? callbackDuration / 1000 : callbackDuration, 0)
				recognizeLastRecordedAudio().then((text) => {
					finish(() => resolve(String(text || '').trim()))
				}).catch((err) => {
					finish(() => reject(err || new Error('录音文件识别失败')))
				})
			})
			recorder.onError((err) => {
				finish(() => reject(new Error((err && err.errMsg) || '停止录音失败')))
			})
			try {
				recorder.stop()
			} catch (e) {
				finish(() => reject(e || new Error('停止录音失败')))
			}
		})
	}

	return new Promise((resolve, reject) => {
		clearSendTimer()
		clearFirstFrameTimer()
		try {
			if (realtimeState.recorder) {
				realtimeState.recorder.stop()
			}
		} catch (e) {}
		try {
			if (realtimeState.socketTask && realtimeState.socketOpen) {
				realtimeState.socketTask.send({
					data: JSON.stringify({ type: 'stop' }),
					fail: () => {}
				})
			}
		} catch (e) {}
		setTimeout(() => {
			try {
				closeSocket()
				realtimeState.recording = false
				resolve(String(realtimeState.finalText || '').trim())
			} catch (err) {
				reject(err || new Error('停止实时转写失败'))
			} finally {
				realtimeState.frameQueue = []
				realtimeState.pcmBuffer = new Uint8Array(0)
			}
		}, 300)
	})
	// #endif
}

export function getLastRecordedFilePath() {
	return String(realtimeState.lastTempFilePath || '').trim()
}

export function recognizeLastRecordedAudio() {
	const path = getLastRecordedFilePath()
	if (!path) {
		return Promise.reject(new Error('没有可识别的录音文件'))
	}
	const token = getToken()
	if (!token) {
		return Promise.reject(new Error('请先登录'))
	}
	let url = getBaseUrl()
	if (url.endsWith('/')) url = url.slice(0, -1)
	url += '/api/MessageOperation/voiceRecognize'
	return new Promise((resolve, reject) => {
		const upload = {
			url,
			filePath: path,
			name: 'file',
			header: {
				Authorization: token,
				uapp: '1'
			},
			success: (res) => {
				try {
					const body = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
					if (res.statusCode !== 200) {
						reject(new Error(body.message || body.Message || '服务异常'))
						return
					}
					const text = body?.text ?? body?.Text ?? ''
					const message = body?.message ?? body?.Message ?? ''
					if (message) {
						reject(new Error(message))
						return
					}
					resolve(String(text || ''))
				} catch (e) {
					reject(new Error('解析响应失败'))
				}
			},
			fail: (err) => {
				reject(err || new Error('上传失败'))
			}
		}
		if (DEV_MODE) upload.sslVerify = false
		uni.uploadFile(upload)
	})
}

/**
 * 图片转文字（拍照/选图后识别）
 * @param {string} imagePath 本地图片路径
 * @returns {Promise<string>} 识别出的文字
 */
export function imageToText(imagePath) {
	if (!imagePath) {
		return Promise.reject(new Error('缺少图片文件'))
	}
	const token = getToken()
	if (!token) {
		return Promise.reject(new Error('请先登录'))
	}
	let url = getBaseUrl()
	if (url.endsWith('/')) url = url.slice(0, -1)
	url += '/api/MessageOperation/imageTextRecognize'

	return new Promise((resolve, reject) => {
		const upload = {
			url,
			filePath: imagePath,
			name: 'file',
			header: {
				Authorization: token,
				uapp: '1'
			},
			success: (res) => {
				try {
					const body = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
					if (res.statusCode !== 200) {
						reject(new Error(body.message || body.Message || '服务异常'))
						return
					}
					const text = body?.text ?? body?.Text ?? ''
					const message = body?.message ?? body?.Message ?? ''
					if (message) {
						reject(new Error(message))
						return
					}
					resolve(String(text || ''))
				} catch (e) {
					reject(new Error('解析响应失败'))
				}
			},
			fail: (err) => {
				reject(err || new Error('上传失败'))
			}
		}
		if (DEV_MODE) upload.sslVerify = false
		uni.uploadFile(upload)
	})
}
