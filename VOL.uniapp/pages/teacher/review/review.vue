<template>
	<view class="teacher-review">
		<view class="top">
			<view class="title">演练复盘</view>
			<view class="back" @click="goBack">返回控制台</view>
		</view>
		<view class="node-line">当前节点：{{ currentNodeName || currentNodeCode || '-' }}</view>
		<scroll-view scroll-x class="node-switch" show-scrollbar="false">
			<view class="node-switch-inner">
				<view
					class="node-chip all-chip"
					:class="{ active: selectedDiscussionNodeCode === allDiscussionNode }"
					@click="selectAllDiscussions"
				>
					全部
				</view>
				<view
					v-for="n in orderedNodes"
					:key="n.nodeCode"
					class="node-chip"
					:class="{ active: selectedDiscussionNodeCode === n.nodeCode, current: currentNodeCode === n.nodeCode }"
					@click="selectNode(n)"
				>
					{{ n.nodeName || n.nodeCode }}
				</view>
			</view>
		</scroll-view>

		<scroll-view scroll-y class="list">
			<view v-for="item in discussionThreads" :key="item.root.id" class="thread">
				<view
					class="msg"
					:class="isTeacherMessage(item.root) ? 'teacher-msg' : 'student-msg'"
					@click="onClickRoot(item.root)"
				>
					<view class="meta" :class="{ teacherMeta: isTeacherMessage(item.root) }">
						<text class="name">{{ resolveDisplayName(item.root) }}</text>
						<text class="role">{{ getRoleText(item.root) }}</text>
						<text class="time">{{ item.root.createDate || '' }}</text>
					</view>
					<view class="content" :class="{ teacherBubble: isTeacherMessage(item.root) }">{{ item.root.content || '' }}</view>
					<view v-if="!isTeacherMessage(item.root)" class="tap-tip">点击可点评</view>
				</view>

				<view v-for="reply in item.replies" :key="reply.id" class="msg teacher-msg review-reply-msg">
					<view class="reply-title">
						{{ resolveDisplayName(reply) }}【教师点评】
					</view>
					<view class="reply-card">
						<view class="reply-origin">{{ truncateDiscussionContent(item.root && item.root.content, 8) }}</view>
						<view class="content teacherBubble">{{ reply.content || '' }}</view>
					</view>
				</view>
			</view>
			<view v-if="!discussionThreads.length" class="empty">当前筛选下暂无讨论内容</view>
		</scroll-view>

		<view class="actions">
			<view class="btn" @click="loadMessages">刷新</view>
		</view>

		<view v-if="reviewVisible" class="review-mask" @click="closeReviewPopup">
			<view class="review-panel" @click.stop>
				<view class="review-title">点评学员发言</view>
				<view class="review-target">
					{{ resolveDisplayName(reviewTarget) }} - {{ getRoleText(reviewTarget) }}
				</view>
				<view class="review-origin">{{ (reviewTarget && reviewTarget.content) ? reviewTarget.content : '' }}</view>
				<textarea
					v-model="reviewText"
					class="review-input"
					placeholder="请输入点评内容"
					maxlength="1000"
				/>
				<view class="review-actions">
					<view class="voice-btn" :class="{ recording }" @click="toggleVoiceRecognize">
						{{ recording ? '结束录音并识别' : '语音识别' }}
					</view>
					<view class="action-row">
						<view class="ghost-btn" @click="closeReviewPopup">取消</view>
						<view class="primary-btn" @click="submitReview">提交点评</view>
					</view>
				</view>
			</view>
		</view>
	</view>
</template>

<script>
import { getFlowNodes, getMembers, getMessages, getState, sendMessage } from '@/util/drillTeacherApi.js'
import { startRealtimeVoiceToText, stopRealtimeVoiceToText, getRealtimeAsrConfig } from '@/util/recognize.js'

const ALL_DISCUSSION_NODE = '__ALL__'

export default {
	data() {
		return {
			allDiscussionNode: ALL_DISCUSSION_NODE,
			projectId: 0,
			messages: [],
			currentNodeCode: '',
			currentNodeName: '',
			flowNodes: [],
			selectedDiscussionNodeCode: ALL_DISCUSSION_NODE,
			memberRoleMap: {},
			memberNameMap: {},
			pollingTimer: null,
			reviewVisible: false,
			reviewTarget: null,
			reviewText: '',
			recording: false,
			recognizingVoice: false
		}
	},
	computed: {
		orderedNodes() {
			return [...(this.flowNodes || [])].sort((a, b) => {
				const ao = Number(a.orderNo || 0)
				const bo = Number(b.orderNo || 0)
				if (ao !== bo) return ao - bo
				return Number(a.videoStartSeconds || 0) - Number(b.videoStartSeconds || 0)
			})
		},
		discussionsBySelectedNode() {
			if (this.selectedDiscussionNodeCode === ALL_DISCUSSION_NODE) return this.messages || []
			return (this.messages || []).filter((m) => String(m.nodeCode || '') === String(this.selectedDiscussionNodeCode || ''))
		},
		discussionThreads() {
			const rows = this.discussionsBySelectedNode || []
			const allRows = this.messages || []
			const roots = []
			const replyMap = {}
			const idSet = new Set(allRows.map(x => Number(x.id || 0)))
			allRows.forEach((m) => {
				const pid = Number(m.parentMessageId || 0)
				if (pid > 0 && idSet.has(pid)) {
					if (!replyMap[pid]) replyMap[pid] = []
					replyMap[pid].push(m)
					return
				}
			})
			rows.forEach((m) => {
				if (Number(m.parentMessageId || 0) > 0) return
				roots.push(m)
			})
			return roots.map((root) => ({
				root,
				replies: (replyMap[Number(root.id)] || []).filter(x => this.isTeacherMessage(x))
			}))
		}
	},
	onLoad(query) {
		const user = this.$store.getters.getUserInfo()
		if (!user?.token) {
			uni.redirectTo({ url: '/pages/login/login' })
			return
		}
		this.projectId = Number((query && query.projectId) || uni.getStorageSync('teacher_mobile_project_id') || 0)
		if (!this.projectId) {
			this.$toast('缺少项目')
			return
		}
		this.selectedDiscussionNodeCode = ALL_DISCUSSION_NODE
		this.loadMemberRoles()
		this.refreshAll()
		this.pollingTimer = setInterval(() => {
			this.refreshAll()
		}, 3000)
	},
	onHide() {
		if (this.pollingTimer) clearInterval(this.pollingTimer)
		this.pollingTimer = null
	},
	onUnload() {
		if (this.pollingTimer) clearInterval(this.pollingTimer)
		this.pollingTimer = null
		if (this.recording) {
			stopRealtimeVoiceToText().catch(() => {})
			this.recording = false
		}
	},
	methods: {
		isTeacherMessage(message) {
			if (Number((message && message.parentMessageId) || 0) > 0) return true
			const role = this.resolveRoleName(message).toLowerCase()
			const name = String((message && message.userName) || '').toLowerCase()
			return role.includes('教师') || role.includes('老师') || role.includes('teacher') || name.includes('teacher')
		},
		resolveRoleName(message) {
			const direct = String((message && message.roleName) || '').trim()
			if (direct) return direct
			const uid = Number((message && message.userId) || 0)
			if (uid > 0 && this.memberRoleMap && this.memberRoleMap[`id_${uid}`]) return this.memberRoleMap[`id_${uid}`]
			const userName = String((message && message.userName) || '').trim()
			if (userName && this.memberRoleMap && this.memberRoleMap[`name_${userName}`]) return this.memberRoleMap[`name_${userName}`]
			if (userName && this.memberRoleMap && this.memberRoleMap[`login_${userName}`]) return this.memberRoleMap[`login_${userName}`]
			return ''
		},
		resolveDisplayName(message) {
			const direct = String((message && message.userName) || '').trim()
			if (direct) return direct
			const uid = Number((message && message.userId) || 0)
			if (uid > 0 && this.memberNameMap && this.memberNameMap[`id_${uid}`]) return this.memberNameMap[`id_${uid}`]
			return this.isTeacherMessage(message) ? '教师' : '匿名'
		},
		getRoleText(message) {
			if (this.isTeacherMessage(message)) return '教师'
			return this.resolveRoleName(message) || '学员'
		},
		truncateDiscussionContent(content, maxLen) {
			const max = Number(maxLen || 8)
			const text = String(content || '').trim()
			if (!text) return '-'
			if (text.length <= max) return `${text}`
			return `${text.slice(0, max)}...`
		},
		async loadMemberRoles() {
			if (!this.projectId) return
			const res = await getMembers(this.projectId, null, false)
			if (!res.status) return
			const map = {}
			const nameMap = {}
			;(res.data || []).forEach((row) => {
				const role = String((row && (row.roleName != null ? row.roleName : row.RoleName)) || '').trim()
				const account = String((row && (row.userName != null ? row.userName : row.UserName)) || '').trim()
				const trueName = String((row && (row.userTrueName != null ? row.userTrueName : row.UserTrueName)) || '').trim()
				const userId = Number((row && (row.userId != null ? row.userId : row.UserId)) || 0)
				if (userId > 0) {
					if (role) map[`id_${userId}`] = role
					if (trueName) nameMap[`id_${userId}`] = trueName
					else if (account) nameMap[`id_${userId}`] = account
				}
				if (role && account) map[`login_${account}`] = role
				if (role && trueName) map[`name_${trueName}`] = role
				if (account && trueName) nameMap[`login_${account}`] = trueName
			})
			this.memberRoleMap = map
			this.memberNameMap = nameMap
		},
		async refreshAll() {
			await this.loadState()
			await this.loadNodes()
			await this.loadMessages()
		},
		async loadState() {
			if (!this.projectId) return
			const res = await getState(this.projectId, false)
			if (!res.status) return
			const stateData = (res && res.data) || {}
			this.currentNodeCode = stateData.currentNodeCode || stateData.CurrentNodeCode || ''
			this.currentNodeName = stateData.currentNodeName || stateData.CurrentNodeName || ''
		},
		async loadNodes() {
			if (!this.projectId) return
			const res = await getFlowNodes(this.projectId, false)
			if (!res.status) return
			this.flowNodes = (res.data || []).map(x => ({
				nodeCode: x.nodeCode ?? x.NodeCode,
				nodeName: x.nodeName ?? x.NodeName,
				orderNo: Number(x.orderNo ?? x.OrderNo ?? 0),
				videoStartSeconds: Number(x.videoStartSeconds ?? x.VideoStartSeconds ?? 0),
				videoEndSeconds: (x.videoEndSeconds ?? x.VideoEndSeconds) == null ? null : Number(x.videoEndSeconds ?? x.VideoEndSeconds)
			}))
		},
		async loadMessages() {
			if (!this.projectId) return
			const res = await getMessages(this.projectId, 'discussion', false)
			if (!res.status) return this.$toast(res.message || '加载失败')
			this.messages = (res.data || []).map(x => ({
				id: x.id != null ? x.id : x.Id,
				userId: x.userId != null ? x.userId : (x.UserId != null ? x.UserId : null),
				content: x.content != null ? x.content : x.Content,
				userName: x.userName != null ? x.userName : x.UserName,
				roleName: x.roleName != null ? x.roleName : x.RoleName,
				createDate: x.createDate != null ? x.createDate : x.CreateDate,
				parentMessageId: x.parentMessageId != null ? x.parentMessageId : (x.ParentMessageId != null ? x.ParentMessageId : null),
				nodeCode: x.nodeCode != null ? x.nodeCode : (x.NodeCode != null ? x.NodeCode : '')
			}))
		},
		selectAllDiscussions() {
			this.selectedDiscussionNodeCode = ALL_DISCUSSION_NODE
		},
		selectNode(node) {
			if (!node?.nodeCode) return
			this.selectedDiscussionNodeCode = node.nodeCode
		},
		onClickRoot(message) {
			if (!message || this.isTeacherMessage(message)) return
			this.reviewTarget = message
			this.reviewText = ''
			this.reviewVisible = true
		},
		closeReviewPopup() {
			this.reviewVisible = false
			this.reviewTarget = null
			this.reviewText = ''
			if (this.recording) {
				stopRealtimeVoiceToText().catch(() => {})
				this.recording = false
			}
		},
		toggleVoiceRecognize() {
			if (this.recognizingVoice) return
			if (this.recording) {
				this.recognizeVoiceFile()
				return
			}
			// #ifndef APP-PLUS
			this.$toast('语音识别当前仅支持 App（Android/iOS）')
			return
			// #endif
			const baseText = String(this.reviewText || '').trim()
			const prefix = baseText ? `${baseText}\n` : ''
			const config = getRealtimeAsrConfig()
			startRealtimeVoiceToText({
				...config,
				useFileModel: true,
				onText: (partialText) => {
					const value = String(partialText || '').trim()
					if (!value) return
					this.reviewText = `${prefix}${value}`
				}
			}).then(() => {
				this.recording = true
				this.$toast('录音中，再点一次结束')
			}).catch((err) => {
				this.recording = false
				this.$toast((err && err.message) || '启动录音失败')
			})
		},
		recognizeVoiceFile() {
			if (!this.recording || this.recognizingVoice) return
			this.recognizingVoice = true
			uni.showLoading({ title: '结束中...' })
			stopRealtimeVoiceToText().then((text) => {
				const value = String(text || '').trim()
				if (!value) {
					this.$toast('未识别到文本')
					return
				}
				this.reviewText = this.reviewText ? `${this.reviewText}\n${value}` : value
			}).catch((err) => {
				this.$toast((err && err.message) || '语音识别失败')
			}).finally(() => {
				this.recording = false
				this.recognizingVoice = false
				uni.hideLoading()
			})
		},
		async submitReview() {
			const content = String(this.reviewText || '').trim()
			const targetId = Number((this.reviewTarget && this.reviewTarget.id) || 0)
			if (!targetId) return this.$toast('请选择要点评的发言')
			if (!content) return this.$toast('请输入点评内容')
			const options = {
				parentMessageId: targetId,
				nodeCode: (this.reviewTarget && this.reviewTarget.nodeCode) || this.currentNodeCode || ''
			}
			const res = await sendMessage(this.projectId, 'discussion', content, options, '提交中...')
			if (!res.status) return this.$toast(res.message || '提交失败')
			this.$toast('点评已提交')
			this.closeReviewPopup()
			this.loadMessages()
		},
		goBack() {
			uni.navigateBack()
		}
	}
}
</script>

<style lang="less" scoped>
.teacher-review {
	min-height: 100vh;
	background: linear-gradient(180deg, #0d1f3c 0%, #0a1628 100%);
	padding: 24rpx;
	color: #fff;
	display: flex;
	flex-direction: column;
	.top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8rpx; }
	.title { font-size: 34rpx; font-weight: 700; }
	.back { color: #9ec8ff; font-size: 24rpx; }
	.node-line { font-size: 24rpx; color: rgba(255,255,255,0.8); margin-bottom: 12rpx; }
	.node-switch {
		white-space: nowrap;
		margin-bottom: 12rpx;
	}
	.node-switch-inner {
		display: inline-flex;
		gap: 10rpx;
		padding-right: 20rpx;
	}
	.node-chip {
		display: inline-flex;
		align-items: center;
		justify-content: center;
		height: 56rpx;
		padding: 0 18rpx;
		border-radius: 28rpx;
		font-size: 22rpx;
		border: 1rpx solid rgba(255,255,255,0.28);
		background: rgba(255,255,255,0.1);
		color: rgba(255,255,255,0.95);
	}
	.node-chip.active {
		background: #1890ff;
		border-color: #1890ff;
	}
	.node-chip.current {
		border-color: #faad14;
	}
	.node-chip.active.current {
		background: #faad14;
		border-color: #faad14;
		color: #111;
	}
	.all-chip {
		font-weight: 600;
	}
	.list { flex: 1; min-height: 0; }
	.thread { margin-bottom: 16rpx; }
	.msg {
		border-radius: 12rpx;
		padding: 14rpx;
		margin-bottom: 8rpx;
		max-width: 88%;
	}
	.student-msg {
		background: #fff;
		color: #222;
		margin-right: auto;
	}
	.teacher-msg {
		background: #67c23a;
		color: #111;
		margin-left: auto;
	}
	.review-reply-msg {
		background: transparent;
	}
	.reply-title {
		font-size: 24rpx;
		color: #fff;
		font-weight: 600;
		margin-bottom: 8rpx;
		text-align: right;
	}
	.reply-card {
		background: #67c23a;
		border: 1rpx solid #5daf34;
		border-radius: 10rpx;
		overflow: hidden;
	}
	.reply-origin {
		margin: 10rpx 12rpx 8rpx;
		padding: 8rpx 10rpx;
		border-radius: 8rpx;
		border: 1rpx solid #dcdfe6;
		background: #fff;
		color: #303133;
		font-size: 22rpx;
	}
	.meta {
		display: flex;
		gap: 12rpx;
		font-size: 22rpx;
		color: rgba(0,0,0,0.55);
		margin-bottom: 8rpx;
	}
	.teacherMeta { justify-content: flex-end; }
	.name { font-weight: 600; color: #222; }
	.content {
		font-size: 25rpx;
		line-height: 1.6;
		white-space: pre-wrap;
		word-break: break-word;
	}
	.teacherBubble { color: #111; }
	.tap-tip { margin-top: 8rpx; font-size: 20rpx; color: #999; }
	.empty { text-align: center; color: rgba(255,255,255,0.6); margin-top: 80rpx; }
	.actions { padding-top: 10rpx; }
	.btn { background: #1890ff; border-radius: 10rpx; text-align: center; padding: 16rpx 0; font-size: 26rpx; }
}

.review-mask {
	position: fixed;
	inset: 0;
	background: rgba(0,0,0,0.45);
	display: flex;
	align-items: flex-end;
	z-index: 999;
}
.review-panel {
	width: 100%;
	background: #fff;
	border-radius: 20rpx 20rpx 0 0;
	padding: 20rpx 24rpx 28rpx;
}
.review-title {
	font-size: 30rpx;
	color: #111;
	font-weight: 700;
}
.review-target {
	font-size: 24rpx;
	color: #666;
	margin-top: 10rpx;
}
.review-origin {
	margin-top: 8rpx;
	padding: 12rpx;
	border-radius: 10rpx;
	background: #f5f7fa;
	color: #333;
	font-size: 24rpx;
	line-height: 1.5;
	max-height: 200rpx;
	overflow: auto;
}
.review-input {
	margin-top: 12rpx;
	width: 100%;
	height: 180rpx;
	box-sizing: border-box;
	border: 1rpx solid #dcdfe6;
	border-radius: 10rpx;
	padding: 12rpx;
	font-size: 24rpx;
	color: #222;
}
.review-actions { margin-top: 14rpx; }
.voice-btn {
	height: 72rpx;
	border-radius: 10rpx;
	background: #e6a23c;
	color: #fff;
	display: flex;
	align-items: center;
	justify-content: center;
	font-size: 24rpx;
}
.voice-btn.recording {
	background: #f56c6c;
}
.action-row {
	margin-top: 12rpx;
	display: flex;
	gap: 12rpx;
}
.ghost-btn,
.primary-btn {
	flex: 1;
	height: 72rpx;
	border-radius: 10rpx;
	display: flex;
	align-items: center;
	justify-content: center;
	font-size: 25rpx;
}
.ghost-btn {
	background: #f2f2f2;
	color: #333;
}
.primary-btn {
	background: #409eff;
	color: #fff;
}
</style>
