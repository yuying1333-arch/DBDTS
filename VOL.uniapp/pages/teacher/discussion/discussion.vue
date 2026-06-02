<template>
	<view class="teacher-discussion">
		<view class="top">
			<view class="title">现场讨论</view>
			<view class="back" @click="goBack">返回控制台</view>
		</view>
		<view class="node-line">当前节点：{{ currentNodeName || currentNodeCode || '-' }}</view>

		<scroll-view scroll-y class="list">
			<view v-for="item in discussionThreads" :key="item.root.id" class="thread">
				<view
					class="msg"
					:class="isTeacherMessage(item.root) ? 'teacher-msg' : 'student-msg'"
				>
					<view class="meta" :class="{ teacherMeta: isTeacherMessage(item.root) }">
						<text class="name">{{ resolveDisplayName(item.root) }}</text>
						<text class="role">{{ getRoleText(item.root) }}</text>
						<text class="time">{{ item.root.createDate || '' }}</text>
					</view>
					<view class="content" :class="{ teacherBubble: isTeacherMessage(item.root) }">{{ item.root.content || '' }}</view>
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
			<view v-if="!discussionThreads.length" class="empty">当前节点暂无讨论内容</view>
		</scroll-view>

		<view class="actions">
			<view class="btn" @click="loadMessages">刷新</view>
		</view>
	</view>
</template>

<script>
import { getMembers, getMessages, getState } from '@/util/drillTeacherApi.js'

export default {
	data() {
		return {
			projectId: 0,
			messages: [],
			currentNodeCode: '',
			currentNodeName: '',
			memberRoleMap: {},
			memberNameMap: {},
			pollingTimer: null,
			redirectingToReview: false
		}
	},
	computed: {
		discussionThreads() {
			const rows = this.messages || []
			const roots = []
			const replyMap = {}
			const idSet = new Set(rows.map(x => Number(x.id || 0)))
			rows.forEach((m) => {
				const pid = Number(m.parentMessageId || 0)
				if (pid > 0 && idSet.has(pid)) {
					if (!replyMap[pid]) replyMap[pid] = []
					replyMap[pid].push(m)
					return
				}
				roots.push(m)
			})
			return roots.map((root) => ({
				root,
				replies: (replyMap[Number(root.id)] || [])
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
	},
	methods: {
		shouldGotoReview(stateData) {
			const status = Number((stateData && (stateData.status != null ? stateData.status : stateData.Status)) || 0)
			const stage = String((stateData && (stateData.currentStage || stateData.CurrentStage)) || '').toLowerCase()
			return status === 3 || stage === 'review' || stage === 'end'
		},
		redirectToReview() {
			if (!this.projectId || this.redirectingToReview) return
			this.redirectingToReview = true
			uni.redirectTo({ url: `/pages/teacher/review/review?projectId=${this.projectId}` })
		},
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
			const switched = await this.loadState()
			if (switched) return
			await this.loadMessages()
		},
		async loadState() {
			if (!this.projectId) return
			const res = await getState(this.projectId, false)
			if (!res.status) return
			const stateData = (res && res.data) || {}
			this.currentNodeCode = stateData.currentNodeCode || stateData.CurrentNodeCode || ''
			this.currentNodeName = stateData.currentNodeName || stateData.CurrentNodeName || ''
			if (this.shouldGotoReview(stateData)) {
				this.redirectToReview()
				return true
			}
			return false
		},
		async loadMessages() {
			if (!this.projectId) return
			const res = await getMessages(this.projectId, 'discussion', false, this.currentNodeCode || '')
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
		goBack() {
			uni.navigateBack()
		}
	}
}
</script>

<style lang="less" scoped>
.teacher-discussion {
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
	.empty { text-align: center; color: rgba(255,255,255,0.6); margin-top: 80rpx; }
	.actions { padding-top: 10rpx; }
	.btn { background: #1890ff; border-radius: 10rpx; text-align: center; padding: 16rpx 0; font-size: 26rpx; }
}
</style>
