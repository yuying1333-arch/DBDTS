<template>
	<view class="operation-page">
		<!-- 接收消息区 -->
		<view class="section">
			<view class="section-title">接收消息</view>
			<view class="row">
				<text class="label">接收来自：</text>
				<radio-group @change="onReceiveTargetChange" class="radio-group">
					<label v-for="item in receiveTargets" :key="'r-' + item.id" class="radio-item">
						<radio :value="String(item.id)" :checked="receiveTargetId === item.id" />
						<text>{{ item.name }}</text>
					</label>
				</radio-group>
			</view>
			<view class="content-box receive-box">
				<scroll-view scroll-y class="msg-scroll" :style="{ height: receiveBoxHeight + 'px' }">
					<view v-for="(msg, idx) in receivedMessages" :key="idx" class="msg-item">
						<text class="msg-time">{{ formatTime(msg.createTime) }}</text>
						<text class="msg-content">{{ msg.content }}</text>
					</view>
					<view v-if="receivedMessages.length === 0" class="empty-tip">暂无消息</view>
				</scroll-view>
			</view>
		</view>

		<!-- 发出消息区 -->
		<view class="section">
			<view class="section-title">发出消息</view>
			<view class="row">
				<text class="label">发消息给：</text>
				<radio-group @change="onSendTargetChange" class="radio-group">
					<label v-for="item in sendTargets" :key="'s-' + item.id" class="radio-item">
						<radio :value="String(item.id)" :checked="sendTargetId === item.id" />
						<text>{{ item.name }}</text>
					</label>
				</radio-group>
			</view>
		</view>

		<!-- 语音识别 -->
		<view class="section">
			<view class="section-title">语音输入</view>
			<view class="voice-row">
				<u-button type="primary" size="small" @click="startVoiceInput" :disabled="voiceRecording">开始录音</u-button>
				<u-button type="error" size="small" @click="stopVoiceInput" v-if="voiceRecording">停止并识别</u-button>
			</view>
			<view class="content-box">
				<textarea class="textarea" v-model="voiceText" placeholder="语音识别内容（待接入API）" disabled />
			</view>
		</view>

		<!-- 手写/图片上传与图片文字识别 -->
		<view class="section">
			<view class="section-title">手写记录图片上传</view>
			<view class="upload-row">
				<u-button type="primary" size="small" @click="chooseImage">选择图片</u-button>
				<view v-if="uploadedImageUrl" class="thumb-wrap">
					<image class="thumb" :src="uploadedImageUrl" mode="aspectFit" @click="previewImage" />
				</view>
			</view>
			<view class="content-box">
				<textarea class="textarea" v-model="imageOcrText" placeholder="图片文字识别内容（待接入API）" disabled />
			</view>
		</view>

		<!-- 合并内容（可编辑） -->
		<view class="section">
			<view class="section-title">合并内容（可修改）</view>
			<view class="content-box">
				<textarea class="textarea merge-text" v-model="mergedContent" placeholder="点击【合并】将语音识别内容与图片识别内容合并到此" />
			</view>
			<view class="btn-row">
				<u-button type="primary" @click="mergeContent">合并</u-button>
			</view>
		</view>

		<!-- 操作按钮 -->
		<view class="action-btns">
			<u-button type="warning" @click="resetAll">重置</u-button>
			<u-button type="success" @click="submitMessage">提交</u-button>
		</view>
	</view>
</template>

<script>
	import http from '@/util/http.js';
	import store from '@/store/index.js';
	import { startRealtimeVoiceToText, stopRealtimeVoiceToText, getRealtimeAsrConfig } from '@/util/recognize.js';

	export default {
		data() {
			return {
				receiveTargets: [],
				sendTargets: [],
				receiveTargetId: null,
				sendTargetId: null,
				receivedMessages: [],
				voiceText: '',
				imageOcrText: '',
				mergedContent: '',
				uploadedImageUrl: '',
				uploadedFilePath: '',
				voiceRecording: false,
				recorderManager: null,
				receiveBoxHeight: 200
			};
		},
		onLoad() {
			const _this = this;
			uni.getSystemInfo({
				success(res) {
					_this.receiveBoxHeight = Math.min(200, res.windowHeight / 3);
				}
			});
			this.loadTargets();
		},
		methods: {
			loadTargets() {
				http.post('api/MessageOperation/getReceiveTargets', {}, true).then(res => {
					this.receiveTargets = Array.isArray(res) ? res : (res.data || []);
					if (this.receiveTargets.length > 0 && !this.receiveTargetId) {
						this.receiveTargetId = this.receiveTargets[0].id;
						this.loadReceivedMessages();
					}
				}).catch(() => {});
				http.post('api/MessageOperation/getSendTargets', {}, false).then(res => {
					this.sendTargets = Array.isArray(res) ? res : (res.data || []);
					if (this.sendTargets.length > 0 && !this.sendTargetId) {
						this.sendTargetId = this.sendTargets[0].id;
					}
				}).catch(() => {});
			},
			onReceiveTargetChange(e) {
				const id = e.detail.value;
				if (id) this.receiveTargetId = parseInt(id, 10);
				this.loadReceivedMessages();
			},
			onSendTargetChange(e) {
				const id = e.detail.value;
				if (id) this.sendTargetId = parseInt(id, 10);
			},
			loadReceivedMessages() {
				if (!this.receiveTargetId) return;
				http.post('api/MessageOperation/getReceivedMessages', { senderId: this.receiveTargetId }, false).then(res => {
					this.receivedMessages = Array.isArray(res) ? res : (res.data || []);
				}).catch(() => {});
			},
			formatTime(str) {
				if (!str) return '';
				const d = new Date(str);
				return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0') + ' ' +
					String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0');
			},
			startVoiceInput() {
				// #ifndef APP-PLUS
				uni.showToast({ icon: 'none', title: '实时转写仅支持App' });
				return;
				// #endif
				const baseText = String(this.voiceText || '').trim();
				const prefix = baseText ? `${baseText}\n` : '';
				const config = getRealtimeAsrConfig();
				startRealtimeVoiceToText({
					...config,
					useFileModel: true,
					onText: (partialText) => {
						const value = String(partialText || '').trim();
						if (!value) return;
						this.voiceText = `${prefix}${value}`;
					}
				}).then(() => {
					this.voiceRecording = true;
					uni.showToast({ icon: 'none', title: '录音中...' });
				}).catch((err) => {
					this.voiceRecording = false;
					uni.showToast({ icon: 'none', title: (err && err.message) || '启动失败' });
				});
			},
			uploadVoiceForRecognize(filePath) {
				uni.showLoading({ title: '转写中...' });
				const token = store.getters.getToken();
				const url = http.ipAddress + 'api/MessageOperation/voiceRecognize';
				uni.uploadFile({
					url,
					filePath,
					name: 'file',
					header: { Authorization: token, uapp: '1' },
					success: (res) => {
						uni.hideLoading();
						if (res.statusCode !== 200) {
							uni.showToast({ icon: 'none', title: '请求失败' });
							return;
						}
						let data = null;
						try {
							data = typeof res.data === 'string' ? JSON.parse(res.data) : res.data;
						} catch (e) {
							uni.showToast({ icon: 'none', title: '解析结果失败' });
							return;
						}
						this.voiceText = (data && data.text) ? data.text : '';
						if (data && data.message) uni.showToast({ icon: 'none', title: data.message });
					},
					fail: () => {
						uni.hideLoading();
						uni.showToast({ icon: 'none', title: '上传失败' });
					}
				});
			},
			stopVoiceInput() {
				if (!this.voiceRecording) return;
				uni.showLoading({ title: '结束中...' });
				stopRealtimeVoiceToText().then((text) => {
					this.voiceRecording = false;
					const value = String(text || '').trim();
					if (!value) {
						uni.showToast({ icon: 'none', title: '未识别到文本' });
						return;
					}
					this.voiceText = value;
				}).catch((err) => {
					this.voiceRecording = false;
					uni.showToast({ icon: 'none', title: (err && err.message) || '停止失败' });
				}).finally(() => {
					uni.hideLoading();
				});
			},
			chooseImage() {
				uni.chooseImage({
					count: 1,
					success: (res) => {
						const path = res.tempFilePaths[0];
						this.uploadedImageUrl = path;
						this.uploadedFilePath = path;
						this.uploadImageForOcr(path);
					}
				});
			},
			uploadImageForOcr(filePath) {
				const token = store.getters.getToken();
				const url = http.ipAddress + 'api/MessageOperation/imageTextRecognize';
				uni.uploadFile({
					url,
					filePath,
					name: 'file',
					header: { Authorization: token, uapp: '1' },
					success: (res) => {
						if (res.statusCode !== 200) {
							uni.showToast({ icon: 'none', title: '识别请求失败' });
							return;
						}
						let data = null;
						try {
							data = typeof res.data === 'string' ? JSON.parse(res.data) : res.data;
						} catch (e) {
							uni.showToast({ icon: 'none', title: '解析结果失败' });
							return;
						}
						this.imageOcrText = (data && data.text) ? data.text : '';
						if (!this.imageOcrText) uni.showToast({ icon: 'none', title: data.message || '图片识别待接入' });
					},
					fail: () => {
						uni.showToast({ icon: 'none', title: '上传失败' });
					}
				});
			},
			previewImage() {
				if (!this.uploadedImageUrl) return;
				uni.previewImage({ urls: [this.uploadedImageUrl] });
			},
			mergeContent() {
				const parts = [];
				if (this.voiceText) parts.push(this.voiceText);
				if (this.imageOcrText) parts.push(this.imageOcrText);
				this.mergedContent = parts.join('\n\n');
				uni.showToast({ icon: 'success', title: '已合并' });
			},
			resetAll() {
				this.receiveTargetId = this.receiveTargets.length > 0 ? this.receiveTargets[0].id : null;
				this.sendTargetId = this.sendTargets.length > 0 ? this.sendTargets[0].id : null;
				this.loadReceivedMessages();
				this.voiceText = '';
				this.imageOcrText = '';
				this.mergedContent = '';
				this.uploadedImageUrl = '';
				this.uploadedFilePath = '';
				uni.showToast({ icon: 'success', title: '已恢复默认' });
			},
			submitMessage() {
				if (!this.sendTargetId) {
					uni.showToast({ icon: 'none', title: '请选择发消息给谁' });
					return;
				}
				const content = (this.mergedContent || '').trim();
				if (!content) {
					uni.showToast({ icon: 'none', title: '请先合并或输入要发送的内容' });
					return;
				}
				http.post('api/MessageOperation/submitMessage', {
					receiverId: this.sendTargetId,
					content
				}, true).then(res => {
					const ok = res && (res.status === true || res.status === 'true');
					if (ok) {
						uni.showToast({ icon: 'success', title: '提交成功' });
						this.mergedContent = '';
					} else {
						uni.showToast({ icon: 'none', title: (res && res.message) || '提交失败' });
					}
				}).catch(() => {});
			}
		}
	};
</script>

<style lang="less" scoped>
	.operation-page {
		padding: 24rpx;
		padding-bottom: 120rpx;
		background: #f5f5f5;
		min-height: 100vh;
	}

	.section {
		background: #fff;
		border-radius: 16rpx;
		padding: 24rpx;
		margin-bottom: 24rpx;
	}

	.section-title {
		font-size: 30rpx;
		font-weight: 600;
		color: #333;
		margin-bottom: 16rpx;
	}

	.row {
		display: flex;
		align-items: flex-start;
		margin-bottom: 16rpx;
	}

	.label {
		width: 160rpx;
		font-size: 28rpx;
		color: #666;
		flex-shrink: 0;
	}

	.radio-group {
		display: flex;
		flex-wrap: wrap;
		gap: 16rpx 24rpx;
	}

	.radio-item {
		display: flex;
		align-items: center;
		font-size: 26rpx;
	}

	.content-box {
		border: 1px solid #eee;
		border-radius: 12rpx;
		padding: 16rpx;
		min-height: 120rpx;
		background: #fafafa;
	}

	.receive-box {
		min-height: 160rpx;
	}

	.msg-scroll {
		width: 100%;
	}

	.msg-item {
		padding: 12rpx 0;
		border-bottom: 1px solid #f0f0f0;
	}

	.msg-item:last-child {
		border-bottom: none;
	}

	.msg-time {
		display: block;
		font-size: 22rpx;
		color: #999;
		margin-bottom: 6rpx;
	}

	.msg-content {
		font-size: 28rpx;
		color: #333;
	}

	.empty-tip {
		text-align: center;
		color: #999;
		font-size: 26rpx;
		padding: 40rpx 0;
	}

	.voice-row,
	.upload-row,
	.btn-row {
		display: flex;
		align-items: center;
		gap: 20rpx;
		margin-bottom: 16rpx;
	}

	.thumb-wrap {
		width: 120rpx;
		height: 120rpx;
		border: 1px solid #eee;
		border-radius: 8rpx;
		overflow: hidden;
	}

	.thumb {
		width: 100%;
		height: 100%;
	}

	.textarea {
		width: 100%;
		min-height: 100rpx;
		font-size: 28rpx;
		color: #333;
		box-sizing: border-box;
	}

	.merge-text {
		min-height: 160rpx;
	}

	.action-btns {
		position: fixed;
		bottom: 0;
		left: 0;
		right: 0;
		display: flex;
		gap: 24rpx;
		padding: 24rpx;
		background: #fff;
		box-shadow: 0 -2rpx 10rpx rgba(0, 0, 0, 0.06);
	}

	.action-btns .u-button {
		flex: 1;
	}
</style>
