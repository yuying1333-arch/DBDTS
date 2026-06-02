<template>
	<view class="teacher-control">
		<view class="top">
			<view class="title">教师控制台</view>
			<view class="logout" @click="logout">退出</view>
		</view>

		<view class="card">
			<view class="label">当前项目</view>
			<picker mode="selector" :range="projectLabels" :value="projectIndex" @change="onProjectChange">
				<view class="picker-value">{{ currentProjectText }}</view>
			</picker>
		</view>

		<view class="card status">
			<view>状态：{{ stateText }}</view>
			<view>阶段：{{ stageText }}</view>
			<view>当前节点：{{ currentNodeText }}</view>
		</view>
		<view class="card status">
			<view class="section-title">任务书开关</view>
			<view class="switch-row">
				<text>任务书显示</text>
				<switch :checked="taskBookVisible" @change="onToggleTaskBookVisible" />
			</view>
		</view>

		<view class="btn-row">
			<view class="btn primary" @click="onStart">开始</view>
			<view class="btn" @click="onPauseResume">{{ pauseResumeText }}</view>
			<view class="btn danger" @click="onEnd">结束</view>
		</view>
		<view class="btn-row">
			<view class="btn review" @click="onReview">复盘</view>
		</view>
		<view class="btn-row">
			<view class="btn" @click="onPrevNode">上一节点</view>
			<view class="btn warning" @click="onNextNode">下一节点</view>
		</view>

		<view class="card">
			<view class="section-title">节点列表（点击切换）</view>
			<scroll-view scroll-y class="node-list">
				<view
					v-for="n in orderedNodes"
					:key="n.nodeCode"
					class="node-item"
					:class="{ active: n.nodeCode === state.currentNodeCode }"
					@click="switchNode(n)"
				>
					<view>{{ n.nodeName || n.nodeCode }}</view>
					<view class="node-range">{{ n.videoStartSeconds || 0 }}s - {{ n.videoEndSeconds == null ? '结束' : `${n.videoEndSeconds}s` }}</view>
				</view>
			</scroll-view>
		</view>

		<view class="btn-row">
			<view class="btn" @click="goDiscussion">现场讨论</view>
			<view class="btn" @click="goSubmissions">学生提交</view>
			<view class="btn" @click="refreshAll">刷新</view>
		</view>
	</view>
</template>

<script>
import {
	getProjects,
	getState,
	start,
	pause,
	resume,
	end,
	setStage,
	nextNode,
	setNode,
	getFlowNodes,
	saveSettings
} from '@/util/drillTeacherApi.js'

const PROJECT_KEY = 'teacher_mobile_project_id'

export default {
	data() {
		return {
			projects: [],
			projectId: 0,
			projectIndex: 0,
			state: {},
			nodes: [],
			timer: null,
			taskBookVisible: true
		}
	},
	computed: {
		projectLabels() {
			return (this.projects || []).map(x => `${x.code || x.id} - ${x.name || '-'}`)
		},
		currentProjectText() {
			const p = this.projects[this.projectIndex]
			if (!p) return '请选择项目'
			return `${p.code || p.id} - ${p.name || '-'}`
		},
		orderedNodes() {
			return [...(this.nodes || [])].sort((a, b) => {
				const ao = Number(a.orderNo || 0)
				const bo = Number(b.orderNo || 0)
				if (ao !== bo) return ao - bo
				return Number(a.id || 0) - Number(b.id || 0)
			})
		},
		stateText() {
			const s = Number(this.state.status || 0)
			return s === 1 ? '运行中' : s === 2 ? '已暂停' : s === 3 ? '已结束' : '未开始'
		},
		stageText() {
			const key = String(this.state.currentStage || '').toLowerCase()
			const map = { scene: '事故场景', report: '事故报告', recovery: '灾后恢复', review: '演练复盘', end: '演练结束' }
			return map[key] || this.state.currentStage || '-'
		},
		currentNodeText() {
			const code = this.state.currentNodeCode
			if (!code) return '-'
			const hit = this.orderedNodes.find(x => x.nodeCode === code)
			return hit?.nodeName || code
		},
		pauseResumeText() {
			return Number(this.state.status || 0) === 2 ? '恢复' : '暂停'
		}
	},
	onShow() {
		const user = this.$store.getters.getUserInfo()
		if (!user?.token) {
			uni.redirectTo({ url: '/pages/login/login' })
			return
		}
		const roleName = String(user.roleName || user.RoleName || '')
		if (!roleName.includes('教师')) {
			uni.redirectTo({ url: '/pages/student/initial/initial' })
			return
		}
		this.loadProjects().then(() => this.refreshAll())
		if (this.timer) clearInterval(this.timer)
		this.timer = setInterval(() => this.refreshAll(false), 3000)
	},
	onHide() {
		if (this.timer) clearInterval(this.timer)
		this.timer = null
	},
	methods: {
		async loadProjects() {
			const res = await getProjects(false)
			if (!res.status) return
			this.projects = (res.data || []).map(x => ({
				id: x.id ?? x.Id,
				name: x.name ?? x.Name,
				code: x.code ?? x.Code
			}))
			const saved = Number(uni.getStorageSync(PROJECT_KEY) || 0)
			const defaultId = saved || Number(this.$store.getters.getUserInfo()?.projectId || 0) || Number(this.projects[0]?.id || 0)
			const idx = this.projects.findIndex(x => Number(x.id) === Number(defaultId))
			this.projectIndex = idx >= 0 ? idx : 0
			this.projectId = Number(this.projects[this.projectIndex]?.id || 0)
			if (this.projectId) uni.setStorageSync(PROJECT_KEY, this.projectId)
		},
		onProjectChange(e) {
			const idx = Number(e?.detail?.value || 0)
			this.projectIndex = idx
			this.projectId = Number(this.projects[idx]?.id || 0)
			uni.setStorageSync(PROJECT_KEY, this.projectId)
			this.refreshAll()
		},
		async refreshAll(showLoading = true) {
			if (!this.projectId) return
			const [stateRes, nodeRes] = await Promise.all([
				getState(this.projectId, false),
				getFlowNodes(this.projectId, false)
			])
			if (stateRes?.status) this.state = stateRes.data || {}
			this.syncSettingsFromState()
			if (nodeRes?.status) {
				this.nodes = (nodeRes.data || []).map(x => ({
					id: x.id ?? x.Id,
					nodeCode: x.nodeCode ?? x.NodeCode,
					nodeName: x.nodeName ?? x.NodeName,
					orderNo: x.orderNo ?? x.OrderNo ?? 0,
					videoStartSeconds: x.videoStartSeconds ?? x.VideoStartSeconds ?? 0,
					videoEndSeconds: x.videoEndSeconds ?? x.VideoEndSeconds ?? null
				}))
			}
			if (showLoading) this.$toast('已刷新')
		},
		async onStart() {
			if (!this.projectId) return this.$toast('请先选择项目')
			const res = await start(this.projectId, '启动中...')
			if (!res.status) return this.$toast(res.message || '操作失败')
			this.$toast('已开始')
			this.refreshAll(false)
		},
		async onPauseResume() {
			if (!this.projectId) return this.$toast('请先选择项目')
			const fn = Number(this.state.status || 0) === 2 ? resume : pause
			const res = await fn(this.projectId, '处理中...')
			if (!res.status) return this.$toast(res.message || '操作失败')
			this.$toast('操作成功')
			this.refreshAll(false)
		},
		async onEnd() {
			if (!this.projectId) return this.$toast('请先选择项目')
			const res = await end(this.projectId, '结束中...')
			if (!res.status) return this.$toast(res.message || '操作失败')
			await setStage(this.projectId, 'review', false)
			this.$toast('已结束')
			this.refreshAll(false)
			this.goReview()
		},
		async onReview() {
			if (!this.projectId) return this.$toast('请先选择项目')
			const status = Number(this.state.status || 0)
			if (status !== 3) {
				const endRes = await end(this.projectId, '结束中...')
				if (!endRes.status) return this.$toast(endRes.message || '结束失败')
			}
			const stageRes = await setStage(this.projectId, 'review', false)
			if (stageRes && stageRes.status === false) return this.$toast(stageRes.message || '切换复盘失败')
			this.$toast('已进入复盘')
			this.goReview()
		},
		goReview() {
			if (!this.projectId) return
			uni.navigateTo({ url: `/pages/teacher/review/review?projectId=${this.projectId}` })
		},
		syncSettingsFromState() {
			let settings = {}
			try {
				const s = this.state?.settingsJson || this.state?.SettingsJson || '{}'
				settings = s ? JSON.parse(s) : {}
			} catch (e) {}
			this.taskBookVisible = settings.taskBookVisible !== false
		},
		async onToggleTaskBookVisible(e) {
			const visible = !!e?.detail?.value
			this.taskBookVisible = visible
			let settings = {}
			try {
				const s = this.state?.settingsJson || this.state?.SettingsJson || '{}'
				settings = s ? JSON.parse(s) : {}
			} catch (err) {}
			settings.taskBookVisible = visible
			const res = await saveSettings(this.projectId, settings, '保存中...')
			if (!res.status) return this.$toast(res.message || '保存失败')
			this.$toast(visible ? '已开启任务书显示' : '已关闭任务书显示')
			this.refreshAll(false)
		},
		async onNextNode() {
			if (!this.projectId) return this.$toast('请先选择项目')
			const res = await nextNode(this.projectId, '切换中...')
			if (!res.status) return this.$toast(res.message || '操作失败')
			this.$toast('已进入下一节点')
			this.refreshAll(false)
		},
		async onPrevNode() {
			const code = this.state.currentNodeCode
			if (!code) return this.$toast('当前无节点')
			const idx = this.orderedNodes.findIndex(x => x.nodeCode === code)
			if (idx <= 0) return this.$toast('已经是第一个节点')
			const prevCode = this.orderedNodes[idx - 1].nodeCode
			const res = await setNode(this.projectId, prevCode, '切换中...')
			if (!res.status) return this.$toast(res.message || '切换失败')
			this.$toast('已切换上一节点')
			this.refreshAll(false)
		},
		async switchNode(node) {
			if (!node?.nodeCode) return
			const res = await setNode(this.projectId, node.nodeCode, '切换中...')
			if (!res.status) return this.$toast(res.message || '切换失败')
			this.$toast('节点已切换')
			this.refreshAll(false)
		},
		goDiscussion() {
			if (!this.projectId) return this.$toast('请先选择项目')
			uni.navigateTo({ url: `/pages/teacher/discussion/discussion?projectId=${this.projectId}` })
		},
		goSubmissions() {
			if (!this.projectId) return this.$toast('请先选择项目')
			uni.navigateTo({ url: `/pages/teacher/submissions/submissions?projectId=${this.projectId}` })
		},
		logout() {
			if (this.timer) clearInterval(this.timer)
			this.timer = null
			this.$store.commit('clearUserInfo')
			uni.reLaunch({ url: '/pages/login/login' })
		}
	}
}
</script>

<style lang="less" scoped>
.teacher-control {
	min-height: 100vh;
	background: linear-gradient(180deg, #0d1f3c 0%, #0a1628 100%);
	padding: 24rpx;
	color: #fff;
	.top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16rpx; }
	.title { font-size: 34rpx; font-weight: 700; }
	.logout { color: #9ec8ff; font-size: 24rpx; }
	.card {
		background: rgba(255,255,255,0.08);
		border: 1rpx solid rgba(255,255,255,0.12);
		border-radius: 14rpx;
		padding: 18rpx;
		margin-bottom: 14rpx;
	}
	.label { color: rgba(255,255,255,0.75); font-size: 24rpx; margin-bottom: 8rpx; }
	.picker-value { color: #fff; font-size: 28rpx; }
	.status { line-height: 1.8; font-size: 24rpx; }
	.btn-row { display: flex; gap: 12rpx; margin-bottom: 12rpx; }
	.btn {
		flex: 1;
		text-align: center;
		padding: 14rpx 0;
		border-radius: 10rpx;
		background: rgba(255,255,255,0.16);
		font-size: 26rpx;
	}
	.btn.primary { background: #1890ff; }
	.btn.review { background: #722ed1; }
	.btn.warning { background: #faad14; color: #111; }
	.btn.danger { background: #f56c6c; }
	.section-title { font-size: 26rpx; margin-bottom: 12rpx; font-weight: 600; }
	.switch-row {
		display: flex;
		justify-content: space-between;
		align-items: center;
		font-size: 24rpx;
	}
	.node-list { max-height: 460rpx; }
	.node-item {
		padding: 14rpx;
		border-radius: 10rpx;
		background: rgba(0,0,0,0.18);
		border: 1rpx solid rgba(255,255,255,0.1);
		margin-bottom: 10rpx;
	}
	.node-item.active {
		border-color: rgba(24,144,255,0.9);
		background: rgba(24,144,255,0.25);
	}
	.node-range { margin-top: 6rpx; font-size: 22rpx; color: rgba(255,255,255,0.75); }
}
</style>
