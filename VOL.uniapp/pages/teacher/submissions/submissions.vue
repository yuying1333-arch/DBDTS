<template>
	<view class="teacher-submissions">
		<view class="top">
			<view class="title">学生提交内容</view>
			<view class="back" @click="goBack">返回控制台</view>
		</view>
		<scroll-view scroll-y class="list">
			<view v-for="a in actions" :key="a.id" class="item">
				<view class="line">
					<text class="node">{{ a.nodeName || '未命名节点' }}</text>
					<text class="time">{{ a.occurAt || '' }}</text>
				</view>
				<view class="line">
					<text>{{ a.roleName || '-' }}</text>
					<text>{{ a.userName || '-' }}</text>
				</view>
				<view class="task">{{ a.taskTitle || '-' }}</view>
				<view class="content">{{ a.textContent || '（无文本）' }}</view>
			</view>
			<view v-if="!actions.length" class="empty">暂无学生提交内容</view>
		</scroll-view>
		<view class="actions-bar">
			<view class="btn" @click="loadActions">刷新</view>
		</view>
	</view>
</template>

<script>
import { getFlowActions, getFlowNodes } from '@/util/drillTeacherApi.js'

export default {
	data() {
		return {
			projectId: 0,
			actions: [],
			nodeNameMap: {}
		}
	},
	onLoad(query) {
		this.projectId = Number(query?.projectId || uni.getStorageSync('teacher_mobile_project_id') || 0)
		if (!this.projectId) {
			this.$toast('缺少项目')
			return
		}
		this.loadActions()
	},
	methods: {
		async loadNodeMap() {
			if (!this.projectId) return
			const res = await getFlowNodes(this.projectId, false)
			if (!res.status) return
			const map = {}
			;(res.data || []).forEach((row) => {
				const code = String(row.nodeCode ?? row.NodeCode ?? '').trim()
				const name = String(row.nodeName ?? row.NodeName ?? '').trim()
				if (code && name) map[code] = name
			})
			this.nodeNameMap = map
		},
		async loadActions() {
			if (!this.projectId) return
			if (!Object.keys(this.nodeNameMap || {}).length) {
				await this.loadNodeMap()
			}
			const res = await getFlowActions(this.projectId, '', false)
			if (!res.status) return this.$toast(res.message || '加载失败')
			this.actions = (res.data || []).map(x => ({
				id: x.id ?? x.Id,
				nodeName: this.nodeNameMap[String(x.nodeCode ?? x.NodeCode ?? '').trim()] || String(x.nodeName ?? x.NodeName ?? '').trim(),
				roleName: x.roleName ?? x.RoleName,
				taskTitle: x.taskTitle ?? x.TaskTitle,
				userName: x.userName ?? x.UserName,
				textContent: x.textContent ?? x.TextContent,
				occurAt: x.occurAt ?? x.OccurAt
			}))
		},
		goBack() {
			uni.navigateBack()
		}
	}
}
</script>

<style lang="less" scoped>
.teacher-submissions {
	min-height: 100vh;
	background: linear-gradient(180deg, #0d1f3c 0%, #0a1628 100%);
	padding: 24rpx;
	color: #fff;
	display: flex;
	flex-direction: column;
	.top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16rpx; }
	.title { font-size: 34rpx; font-weight: 700; }
	.back { color: #9ec8ff; font-size: 24rpx; }
	.list { flex: 1; min-height: 0; }
	.item {
		background: rgba(255,255,255,0.08);
		border: 1rpx solid rgba(255,255,255,0.12);
		border-radius: 12rpx;
		padding: 14rpx;
		margin-bottom: 10rpx;
	}
	.line { display: flex; justify-content: space-between; font-size: 22rpx; color: rgba(255,255,255,0.75); margin-bottom: 6rpx; }
	.node { color: #fff; font-weight: 600; }
	.task { font-size: 24rpx; margin-bottom: 6rpx; }
	.content { font-size: 24rpx; line-height: 1.6; white-space: pre-wrap; color: rgba(255,255,255,0.92); }
	.empty { text-align: center; color: rgba(255,255,255,0.6); margin-top: 80rpx; }
	.actions-bar { padding-top: 10rpx; }
	.btn { background: #1890ff; border-radius: 10rpx; text-align: center; padding: 16rpx 0; font-size: 26rpx; }
}
</style>
