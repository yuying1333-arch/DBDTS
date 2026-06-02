<template>
	<view class="student-initial">
		<view class="header">
			<view class="logout" @click="onLogout">退出登录</view>
			<view class="header-title">{{ projectName }}</view>
			<view class="header-user">
				<view class="user-name">{{ userDisplay }}</view>
			</view>
		</view>
		<view class="main">
			<view class="content">
				<view class="block">
					<view class="block-title">我的角色</view>
					<view class="role-me" v-if="roleInfo">
					<view class="role-marker" v-if="roleMarkerText">{{ roleMarkerText }}</view>
						<view class="role-me-text">
							<view class="role-name">{{ roleInfo.roleName }}</view>
							<view class="role-no" v-if="roleInfo.roleNo">角色编号：{{ roleInfo.roleNo }}</view>
						</view>
					</view>
					<view class="role-me-loading" v-else>加载中...</view>
				</view>
				<view class="block task-book" v-if="roleInfo && !isEndPhase">
					<view class="block-title">任务</view>
					<view class="task-tip">当前节点：{{ getNodeDisplayName(nodeName, nodeCode) }}，勾选并提交任务</view>
					
					<checkbox-group v-if="nodeTasks.length" class="taskbook-box" :value="checkedItemIds" @change="onChecklistChange">
						<label class="task-item" v-for="t in nodeTasks" :key="t.id">
							<checkbox :value="String(t.id)" />
							<text class="task-text">{{ t.taskTitle }}</text>
						</label>
					</checkbox-group>
					
					<view v-else class="file-empty">当前节点暂无任务</view>
					
					
					
					<view class="task-items custom-task-items">
						<view class="task-item" v-for="(c, idx) in customChecklist" :key="`custom_${idx}`">
							<checkbox :checked="!!c.checked" @tap.stop="onToggleCustomItem(c, !c.checked)" />
							<input
								v-model="c.text"
								class="custom-input"
								:disabled="!c.checked"
								placeholder="勾选后可编辑内容"
								@tap.stop
								@input="onCustomItemInput(c)"
							/>
						</view>
					</view>

					<view class="submit-row custom-task-items">
						<view class="submit-btn" :class="{ disabled: isNodeSubmitted || taskSubmitting }" @click="submitTaskbookSelections">
							{{ isNodeSubmitted ? '任务已提交' : (taskSubmitting ? '提交中...' : '提交任务') }}
						</view>
					</view>
				</view>
				<view class="block text-block" v-if="!isEndPhase">
					<view class="block-title">讨论框</view>
					<view class="submit-row">
						<picker mode="selector" :range="speechModeOptions" :value="speechModeIndex" @change="onSpeechModeChange">
							<view class="selector-box">发言模式：{{ speechModeOptions[speechModeIndex] }}</view>
						</picker>
					</view>
					<textarea v-model="editText" class="edit-area" placeholder="识别结果将显示在此..." />
					<view class="submit-row">
						<view class="submit-btn" @click="onSave">提交讨论</view>
					</view>
				</view>
				<view class="block text-block" v-if="!isEndPhase">
					<view class="block-title">向其他身份组对话</view>
					<view class="submit-row">
						<picker mode="selector" :range="dialogRoleTargets" :value="dialogRoleIndex" @change="onDialogRoleChange">
							<view class="selector-box">发送给：{{ dialogRoleTargets[dialogRoleIndex] || '请选择身份组' }}</view>
						</picker>
					</view>
					<textarea v-model="dialogText" class="edit-area" placeholder="输入要发送的消息..." />
					<view class="submit-row">
						<view class="submit-btn" @click="sendRoleDialog">发送对话</view>
					</view>
				</view>
				<view class="block report-block" v-if="isEndPhase">
					<view class="block-title">演练总结</view>
					<textarea v-model="summaryForm.content" class="edit-area" :disabled="hasSubmittedSummary" placeholder="请填写演练总结..." />
					<view class="submit-row">
						<view class="submit-status">总结图片</view>
						<view class="submit-btn" :class="{ disabled: hasSubmittedSummary || reportImageUploading }" @click="chooseSummaryImages">
							{{ reportImageUploading ? '上传中...' : '上传图片' }}
						</view>
					</view>
					<view v-if="summaryImageUrls.length" class="summary-image-list">
						<view class="summary-image-item" v-for="(url, idx) in summaryImageUrls" :key="`${url}_${idx}`">
							<image class="summary-image" :src="resolveUploadUrl(url)" mode="aspectFill" @click="previewSummaryImage(url)" />
							<view v-if="!hasSubmittedSummary" class="summary-image-remove" @click="removeSummaryImage(idx)">删除</view>
						</view>
					</view>
					<view class="submit-row">
						<view class="submit-status">{{ reportStatusText() }}</view>
						<view class="submit-btn report-submit-btn" :class="{ submitting: summarySubmitting, done: hasSubmittedSummary }" @click="submitFinalReport()">
							{{ hasSubmittedSummary ? '已提交' : (summarySubmitting ? '提交中...' : '提交演练总结') }}
						</view>
					</view>
				</view>
			</view>
			<view class="actions">
				<view class="action-row">
					<view class="tool-btn" @click="onWalkieTalkie">{{ recording ? '结束语音识别' : '语音识别' }}</view>
					<view class="tool-btn" @click="onCamera">图片识别</view>
				</view>
				<view class="action-row primary">
					<view class="action-btn" :class="{ done: signed }" @click="onSignIn">{{ signed ? '已签到' : '签到' }}</view>
				<view class="action-btn" @click="onNext">{{ isEndPhase ? '演练总结阶段' : '下一步' }}</view>
					<view class="action-btn" @click="onRefresh">刷新</view>
				</view>
			</view>
		</view>
		<view class="prompt" v-if="!signed">请先完成签到</view>
	</view>
</template>

<script>
	import { studentSignIn, getRoleMe, sendDiscussionMessage, getNodeTasks, submitNodeTask, submitTaskbook, getDialogRoleTargets, sendDialogMessage, getDialogInbox, getDrillState, submitStudentReport, getMyReports, uploadStudentReportImage } from '@/util/studentApi.js'
	import { imageToText, startRealtimeVoiceToText, stopRealtimeVoiceToText, getRealtimeAsrConfig } from '@/util/recognize.js'
	import { getBaseUrl } from '@/util/http.js'

	export default {
		data() {
			return {
				signed: false,
			roles: [],
				selectedRoles: [],
				roleInfo: null,
				tasks: [],
				nodeCode: '',
				nodeName: '',
				nodeTasks: [],
				checkedItemIds: [],
				editText: '',
				recording: false,
				recorderManager: null,
				drillStatus: 0,
				isEndPhase: false,
				summaryForm: { content: '' },
			myReports: [],
			summaryImageUrls: [],
			reportImageUploading: false,
			summarySubmitting: false,
			taskBookVisible: true,
			customChecklist: [{ checked: false, text: '' }],
			speechModeOptions: ['普通发言', '自主发言'],
			speechModeIndex: 0,
			nodeRoleAllowed: true,
			dialogRoleTargets: [],
			dialogRoleIndex: 0,
			dialogText: '',
			dialogTimer: null,
			lastDialogId: 0,
				taskSubmitting: false
			}
		},
		computed: {
			projectId() {
				const u = this.$store.getters.getUserInfo()
				return u?.projectId || 0
			},
			userDisplay() {
				const u = this.$store.getters.getUserInfo()
				return u?.userName || u?.userTrueName || '学员'
			},
			projectName() {
				const u = this.$store.getters.getUserInfo()
				return u?.projectName || u?.projectCode || '-'
			},
			roleDisplay() {
				return this.roleInfo?.roleName || this.$store.getters.getUserInfo()?.roleName || (this.selectedRoles[0] || '-')
			},
			roleMarkerText() {
				return this.roleInfo?.marker || ''
			},
			totalItemsCount() {
				let c = 0
				;(this.tasks || []).forEach(t => {
					c += (t.items || []).length
				})
				return c
			},
			allChecked() {
				if (!this.taskBookVisible) return true
				return this.totalItemsCount > 0 && (this.checkedItemIds || []).length === this.totalItemsCount
			},
			isNodeSubmitted() {
				return (this.nodeTasks || []).some(x => x.done)
			},
			hasSubmittedSummary() {
				const row = (this.myReports || []).find(x => {
					const t = String(x.ReportType || x.reportType || '').toLowerCase()
					return t === 'summary' || t === 'report' || t === 'recovery'
				})
				return !!row
			}
		},
		onLoad() {
			const u = this.$store.getters.getUserInfo()
			if (!u?.token) {
				uni.redirectTo({ url: '/pages/login/login' })
				return
			}
			const roleName = String(u.roleName || u.RoleName || '')
			if (roleName.includes('教师')) {
				uni.redirectTo({ url: '/pages/teacher/control/control' })
				return
			}
			if (u.roleName) {
				this.selectedRoles = [u.roleName]
				this.roles = [u.roleName]
			}
			this.loadRoleMe()
			this.loadDrillState()
			this.loadMyReports()
			this.loadDialogRoleTargets()
			this.initDialogPolling()
		},
		onHide() {
			if (this.recording) {
				stopRealtimeVoiceToText().catch(() => {})
				this.recording = false
			}
			if (this.dialogTimer) clearInterval(this.dialogTimer)
			this.dialogTimer = null
		},
		onUnload() {
			if (this.recording) {
				stopRealtimeVoiceToText().catch(() => {})
				this.recording = false
			}
			if (this.dialogTimer) clearInterval(this.dialogTimer)
			this.dialogTimer = null
		},
		methods: {
			getNodeDisplayName(nodeName, nodeCode) {
				if (nodeName) return nodeName
				const code = String(nodeCode || '').trim().toLowerCase()
				if (!code) return '-'
				const map = {
					scene: '事故场景',
					report: '演练总结',
					recovery: '演练总结',
					end: '演练结束'
				}
				return map[code] || nodeCode
			},
			parseTaskBook(taskBookJson) {
				if (!taskBookJson) return []
				try {
					const obj = typeof taskBookJson === 'string' ? JSON.parse(taskBookJson) : taskBookJson
					return obj?.tasks || []
				} catch (e) {
					return []
				}
			},
			loadRoleMe() {
				if (!this.projectId) return
				getRoleMe(this.projectId, false).then(res => {
					if (!res.status) return this.$toast(res.message || '获取任务失败')
					this.roleInfo = res.data || {}
					this.signed = !!this.roleInfo?.signedAt
					this.tasks = this.parseTaskBook(this.roleInfo?.taskBookJson)
					this.taskBookVisible = this.roleInfo?.taskBookVisible !== false
					this.loadNodeTasks()
					this.loadDialogRoleTargets()
					this.checkedItemIds = []
				}).catch(() => {
					this.roleInfo = null
					this.tasks = []
					this.nodeTasks = []
					this.checkedItemIds = []
				})
			},
			parseSteps(stepsJson) {
				if (!stepsJson) return []
				try {
					const arr = typeof stepsJson === 'string' ? JSON.parse(stepsJson) : stepsJson
					if (!Array.isArray(arr)) return []
					return arr.map((s, i) => ({
						id: String(s.id || `s${i + 1}`),
						text: String(s.text || ''),
						checked: !!s.checked
					}))
				} catch (e) {
					return []
				}
			},
			loadNodeTasks() {
				if (!this.projectId) return
				getNodeTasks(this.projectId, '', false).then(res => {
					if (!res.status) return
					this.nodeCode = res.data?.nodeCode || ''
					this.nodeName = res.data?.nodeName || ''
					this.nodeRoleAllowed = res.data?.roleAllowed !== false
					const list = res.data?.tasks || []
					this.nodeTasks = list.map(t => ({
						...t,
						submitting: false,
						steps: this.parseSteps(t.stepsJson)
					}))
					this.checkedItemIds = []
				}).catch(() => {
					this.nodeTasks = []
				})
			},
			onToggleCustomItem(item, checked) {
				const idx = this.customChecklist.indexOf(item)
				if (idx < 0) return
				if (checked) {
					this.customChecklist[idx].checked = true
					return
				}
				this.customChecklist[idx].checked = false
				this.customChecklist[idx].text = ''
				this.customChecklist = this.customChecklist.slice(0, idx + 1)
				if (!this.customChecklist.length) this.customChecklist = [{ checked: false, text: '' }]
			},
			onCustomItemInput(item) {
				const idx = this.customChecklist.indexOf(item)
				if (idx < 0 || !item.checked) return
				const val = String(item.text || '').trim()
				const isLast = idx === this.customChecklist.length - 1
				if (val && isLast) {
					this.customChecklist.push({ checked: false, text: '' })
				}
				// 始终只保留一个“新增空选项”
				let firstEmptyIdx = -1
				this.customChecklist = this.customChecklist.filter((x, i) => {
					if (x.checked || String(x.text || '').trim()) return true
					if (firstEmptyIdx < 0) {
						firstEmptyIdx = i
						return true
					}
					return false
				})
			},
			submitTaskbookSelections() {
				if (!this.projectId) return this.$toast('缺少项目信息')
				if (this.isNodeSubmitted) return this.$toast('当前节点任务已提交')
				if (this.taskSubmitting) return
				const selectedTaskTexts = []
				const map = new Map()
				;(this.nodeTasks || []).forEach((t) => {
					map.set(String(t.id), String(t.taskTitle || '').trim())
				})
				;(this.checkedItemIds || []).forEach((id) => {
					const txt = map.get(String(id))
					if (txt) selectedTaskTexts.push(txt)
				})
				const customItems = (this.customChecklist || [])
					.filter(x => x.checked && String(x.text || '').trim())
					.map(x => String(x.text || '').trim())
				const allItems = [...selectedTaskTexts, ...customItems]
				if (!allItems.length) return this.$toast('请至少勾选一项')
				this.taskSubmitting = true
				submitTaskbook(this.projectId, {
					nodeCode: this.nodeCode || '',
					selectedItems: allItems,
					customItems
				}, '提交中...').then((res) => {
					this.taskSubmitting = false
					if (!res.status) return this.$toast(res.message || '提交失败')
					this.$toast('任务已提交')
					this.loadNodeTasks()
				}).catch((e) => {
					this.taskSubmitting = false
					this.$toast(e?.message || '提交失败')
				})
			},
			onSpeechModeChange(e) {
				this.speechModeIndex = Number(e?.detail?.value || 0)
			},
			onDialogRoleChange(e) {
				this.dialogRoleIndex = Number(e?.detail?.value || 0)
			},
			loadDialogRoleTargets() {
				if (!this.projectId) return
				getDialogRoleTargets(this.projectId, false).then((res) => {
					if (!res.status) return
					this.dialogRoleTargets = (res.data || []).filter(Boolean)
					if (this.dialogRoleIndex >= this.dialogRoleTargets.length) this.dialogRoleIndex = 0
				})
			},
			sendRoleDialog() {
				if (!this.projectId) return this.$toast('缺少项目信息')
				const toRoleName = this.dialogRoleTargets[this.dialogRoleIndex]
				if (!toRoleName) return this.$toast('请选择目标身份组')
				const content = String(this.dialogText || '').trim()
				if (!content) return this.$toast('请输入对话内容')
				sendDialogMessage(this.projectId, { toRoleName, content }, '发送中...').then((res) => {
					if (!res.status) return this.$toast(res.message || '发送失败')
					this.dialogText = ''
					this.$toast('发送成功')
				})
			},
			initDialogPolling() {
				if (!this.projectId) return
				const key = `student_dialog_last_${this.projectId}`
				this.lastDialogId = Number(uni.getStorageSync(key) || 0)
				if (this.dialogTimer) clearInterval(this.dialogTimer)
				this.dialogTimer = setInterval(() => {
					getDialogInbox(this.projectId, this.lastDialogId, false).then((res) => {
						if (!res.status) return
						const rows = res.data || []
						if (!rows.length) return
						const last = rows[rows.length - 1]
						this.lastDialogId = Number(last.id || 0)
						uni.setStorageSync(key, this.lastDialogId)
						const first = rows[0]
						uni.showModal({
							title: `${first.fromRoleName || '身份组'} 发来消息`,
							content: String(first.text || ''),
							showCancel: false
						})
					})
				}, 3000)
			},
			loadDrillState() {
				if (!this.projectId) return
				getDrillState(this.projectId, false).then(res => {
					if (!res.status) return
					const s = res.data?.status ?? 0
					this.drillStatus = s
					const nowEnd = s === 3
					if (nowEnd && !this.isEndPhase) {
						this.$toast('教师已结束演练，请填写演练总结')
					}
					this.isEndPhase = nowEnd
				})
			},
			loadMyReports() {
				if (!this.projectId) return
				getMyReports(this.projectId, '', false).then(res => {
					if (!res.status) return
					this.myReports = res.data || []
					const summary = this.myReports.find(x => {
						const t = String(x.ReportType || x.reportType || '').toLowerCase()
						return t === 'summary' || t === 'report' || t === 'recovery'
					})
					this.summaryForm.content = summary?.Content || summary?.content || ''
					this.summaryImageUrls = this.parseSummaryImages(summary?.ExtraJson || summary?.extraJson || '')
				})
			},
			resolveUploadUrl(url) {
				const raw = String(url || '').trim()
				if (!raw) return ''
				if (/^https?:\/\//i.test(raw)) return raw
				let base = String(getBaseUrl() || '').trim()
				if (base.endsWith('/')) base = base.slice(0, -1)
				return `${base}${raw.startsWith('/') ? '' : '/'}${raw}`
			},
			parseSummaryImages(extraJson) {
				if (!extraJson) return []
				try {
					const obj = typeof extraJson === 'string' ? JSON.parse(extraJson) : extraJson
					const rows = Array.isArray(obj?.imageUrls) ? obj.imageUrls : []
					return rows.map(x => String(x || '').trim()).filter(Boolean)
				} catch (e) {
					return []
				}
			},
			chooseSummaryImages() {
				if (this.hasSubmittedSummary) return this.$toast('已提交后不可再上传')
				if (this.reportImageUploading) return
				uni.chooseImage({
					count: 9,
					success: (res) => {
						const paths = (res?.tempFilePaths || []).filter(Boolean)
						if (!paths.length) return this.$toast('未选择图片')
						this.uploadSummaryImages(paths)
					}
				})
			},
			async uploadSummaryImages(paths) {
				if (!Array.isArray(paths) || !paths.length) return
				this.reportImageUploading = true
				uni.showLoading({ title: '上传中...' })
				try {
					const uploaded = []
					for (const p of paths) {
						const url = await uploadStudentReportImage(p)
						const value = String(url || '').trim()
						if (value) uploaded.push(value)
					}
					if (uploaded.length) {
						this.summaryImageUrls = [...this.summaryImageUrls, ...uploaded]
						this.$toast(`已上传${uploaded.length}张`)
					}
				} catch (e) {
					this.$toast(e?.message || '上传失败')
				} finally {
					this.reportImageUploading = false
					uni.hideLoading()
				}
			},
			removeSummaryImage(index) {
				if (this.hasSubmittedSummary) return
				const i = Number(index)
				if (i < 0 || i >= this.summaryImageUrls.length) return
				const next = [...this.summaryImageUrls]
				next.splice(i, 1)
				this.summaryImageUrls = next
			},
			previewSummaryImage(url) {
				const current = this.resolveUploadUrl(url)
				if (!current) return
				const urls = (this.summaryImageUrls || []).map(x => this.resolveUploadUrl(x)).filter(Boolean)
				uni.previewImage({
					current,
					urls: urls.length ? urls : [current]
				})
			},
			reportStatusText() {
				const row = (this.myReports || []).find(x => {
					const t = String(x.ReportType || x.reportType || '').toLowerCase()
					return t === 'summary' || t === 'report' || t === 'recovery'
				})
				if (!row) return '未提交'
				const status = row.SubmitStatus ?? row.submitStatus ?? 1
				if (status === 2) {
					const score = row.ReviewScore ?? row.reviewScore
					return score == null ? '已批阅' : `已批阅（评分:${score}）`
				}
				return '已提交，待批阅'
			},
			submitFinalReport() {
				if (!this.projectId) return this.$toast('缺少项目信息')
				if (!this.isEndPhase) return this.$toast('请等待教师结束演练后再提交')
				if (this.hasSubmittedSummary) return this.$toast('演练总结仅允许提交一次')
				if (this.summarySubmitting) return
				const content = this.summaryForm.content || ''
				if (!String(content).trim()) return this.$toast('请先填写内容')
				const title = '演练总结'
				this.summarySubmitting = true
				submitStudentReport(this.projectId, {
					reportType: 'summary',
					title,
					content: String(content).trim(),
					extraJson: JSON.stringify({
						imageUrls: (this.summaryImageUrls || []).map(x => String(x || '').trim()).filter(Boolean)
					})
				}, '提交中...').then(res => {
					this.summarySubmitting = false
					if (!res.status) return this.$toast(res.message || '提交失败')
					this.$toast('提交成功')
					this.loadMyReports()
				}).catch((e) => {
					this.summarySubmitting = false
					this.$toast(e?.message || '提交失败')
				})
			},
			onNodeStepChange(task, step, checked) {
				const i = (task.steps || []).findIndex(x => x.id === step.id)
				if (i >= 0) task.steps[i].checked = checked
			},
			submitNodeTaskAction(task) {
				if (!this.projectId) return this.$toast('缺少项目信息')
				if (!task?.id) return this.$toast('任务参数异常')
				if (task.submitting) return
				task.submitting = true
				const payload = {
					nodeCode: this.nodeCode || task.nodeCode || '',
					assignmentId: task.id,
					taskTitle: task.taskTitle || '',
					stepResult: {
						steps: (task.steps || []).map(s => ({ id: s.id, text: s.text, checked: !!s.checked }))
					},
					textContent: this.editText || ''
				}
				submitNodeTask(this.projectId, payload, '提交任务中...').then(res => {
					task.submitting = false
					if (!res.status) return this.$toast(res.message || '提交失败')
					this.$toast('任务提交成功')
					task.done = true
				}).catch((e) => {
					task.submitting = false
					this.$toast(e?.message || '提交失败')
				})
			},
			onChecklistChange(e) {
				const v = e?.detail?.value || []
				this.checkedItemIds = (v || []).map(x => String(x))
			},
			toggleRole(r) {
				const i = this.selectedRoles.indexOf(r)
				if (i >= 0) this.selectedRoles.splice(i, 1)
				else this.selectedRoles.push(r)
			},
			onSignIn() {
				if (!this.projectId) return this.$toast('缺少项目信息')
				studentSignIn(this.projectId, '签到中...').then(res => {
					if (!res.status) return this.$toast(res.message || '签到失败')
					this.$toast('签到成功')
					this.signed = true
					this.loadRoleMe()
					this.loadNodeTasks()
				})
			},
			onNext() {
				if (this.isEndPhase) return this.$toast('请填写并提交总结报告')
				if (!this.projectId) return this.$toast('缺少项目信息')
				const oldNode = this.nodeCode || ''
				this.waitTeacherNext(oldNode)
			},
			waitTeacherNext(oldNode) {
				this.loadDrillState()
				if (this.isEndPhase) return this.$toast('演练已结束，请填写总结报告')
				getNodeTasks(this.projectId, '', false).then(res => {
					if (!res.status) return this.$toast(res.message || '获取节点状态失败')
					const newNode = res.data?.nodeCode || ''
					if (!newNode || newNode === oldNode) {
						return this.$toast('请等待教师下一步指示')
					}
					this.nodeCode = newNode
					this.nodeName = res.data?.nodeName || ''
					const list = res.data?.tasks || []
					this.nodeTasks = list.map(t => ({
						...t,
						submitting: false,
						steps: this.parseSteps(t.stepsJson)
					}))
					this.$toast(`已进入下一节点：${this.getNodeDisplayName('', newNode)}`)
				}).catch(() => {
					this.$toast('请等待教师下一步指示')
				})
			},
			onSave() {
				if (!this.projectId) return this.$toast('缺少项目信息')
				if (!this.editText?.trim()) return this.$toast('请先编辑内容')
				const selfInitiated = this.speechModeIndex === 1
				if (!selfInitiated && this.nodeRoleAllowed === false) {
					return this.$toast('当前节点未激活你的身份组发言权限，请选择自主发言')
				}
				sendDiscussionMessage(this.projectId, this.editText.trim(), {
					nodeCode: this.nodeCode || '',
					selfInitiated
				}, '提交中...').then(res => {
					if (!res.status) return this.$toast(res.message || '提交失败')
					this.$toast('已提交到现场讨论')
				}).catch((e) => {
					this.$toast(e?.message || '提交失败')
				})
			},
			onRefresh() {
				this.loadRoleMe()
				this.loadNodeTasks()
				this.loadDrillState()
				this.loadMyReports()
				this.loadDialogRoleTargets()
				this.initDialogPolling()
				this.$toast('已刷新')
			},
			pickImageFile() {
				uni.chooseImage({
					count: 1,
					success: (res) => {
						const path = res?.tempFilePaths?.[0]
						if (!path) return this.$toast('未选择图片')
						uni.showLoading({ title: '识别中...' })
						imageToText(path).then(t => {
							uni.hideLoading()
							if (t) this.editText += t
							else this.$toast('未识别到文字')
						}).catch((e) => {
							uni.hideLoading()
							this.$toast(e?.message || '识别失败')
						})
					},
					fail: () => this.$toast('选择图片失败')
				})
			},
			onWalkieTalkie() {
				if (this.recording) {
					uni.showLoading({ title: '识别中...' })
					stopRealtimeVoiceToText().then((text) => {
						uni.hideLoading()
						this.recording = false
						const value = String(text || '').trim()
						if (value) {
							this.editText = this.editText ? `${this.editText}\n${value}` : value
							this.$toast('语音识别完成')
							return
						}
						this.$toast('未识别到文字')
					}).catch((err) => {
						uni.hideLoading()
						this.recording = false
						this.$toast(err?.message || '停止识别失败')
					})
					return
				}
				// #ifndef APP-PLUS
				this.$toast('语音识别当前仅支持 App（Android/iOS）')
				return
				// #endif
				const config = getRealtimeAsrConfig()
				const baseText = String(this.editText || '').trim()
				const prefix = baseText ? `${baseText}\n` : ''
				startRealtimeVoiceToText({
					...config,
					useFileModel: true,
					onText: (partialText) => {
						const value = String(partialText || '').trim()
						if (!value) return
						this.editText = `${prefix}${value}`
					}
				}).then(() => {
					this.recording = true
					this.$toast('录音中，再次点击识别按钮结束录音')
				}).catch((err) => {
					this.recording = false
					this.$toast(err?.message || '启动录音失败')
				})
			},
			onCamera() {
				this.pickImageFile()
			},
			onLogout() {
				this.$store.commit('clearUserInfo')
				uni.reLaunch({ url: '/pages/login/login' })
			}
		}
	}
</script>

<style lang="less" scoped>
	.student-initial {
		min-height: 100vh;
		background: linear-gradient(180deg, #0d1f3c 0%, #0a1628 100%);
		display: flex;
		flex-direction: column;
		padding-bottom: env(safe-area-inset-bottom);
		.header {
			position: relative;
			background: rgba(24,144,255,0.25);
			padding: 10rpx 24rpx 12rpx;
			padding-top: 8rpx;
			.header-title {
				text-align: center;
				color: #fff;
				font-size: 42rpx;
				font-weight: 700;
				line-height: 1.25;
				padding: 0 88rpx 0;
			}
			.header-user {
				margin-top: 4rpx;
				text-align: center;
			}
			.user-name { color: rgba(255,255,255,0.95); font-size: 30rpx; font-weight: 600; }
			.logout {
				position: absolute;
				right: 22rpx;
				top: 50rpx;
				color: #ff4d4f;
				font-size: 28rpx;
				font-weight: 700;
			}
		}
		.main {
			flex: 1;
			display: flex;
			flex-direction: column;
			padding: 16rpx 20rpx 20rpx;
			gap: 24rpx;
			overflow: hidden;
		}
		.content { flex: 1; min-height: 0; display: flex; flex-direction: column; gap: 20rpx; }
		.block {
			background: rgba(255,255,255,0.06);
			border-radius: 16rpx;
			padding: 24rpx;
			border: 1rpx solid rgba(255,255,255,0.08);
			.block-title { color: rgba(255,255,255,0.7); font-size: 24rpx; margin-bottom: 16rpx; }
		}
		.role-tags { display: flex; flex-wrap: wrap; gap: 16rpx; }
		.role-tag {
			padding: 14rpx 24rpx;
			background: rgba(255,255,255,0.1);
			color: #fff;
			font-size: 26rpx;
			border-radius: 12rpx;
		}
		.role-tag.active { background: #1890ff; }
		.file-empty { color: rgba(255,255,255,0.5); font-size: 24rpx; padding: 12rpx 0; }
		.node-task-list { display: flex; flex-direction: column; gap: 12rpx; }
		.node-task-actions { margin-top: 12rpx; display: flex; align-items: center; justify-content: space-between; }
		.node-task-state { color: rgba(255,255,255,0.7); font-size: 24rpx; }
		.node-task-state.done { color: #52c41a; }
		.text-block { flex: 1; min-height: 160rpx; }
		.edit-area {
			width: 100%;
			min-height: 140rpx;
			background: rgba(0,0,0,0.2);
			border: 1rpx solid rgba(255,255,255,0.15);
			border-radius: 12rpx;
			color: #fff;
			padding: 20rpx;
			font-size: 28rpx;
			box-sizing: border-box;
		}
		.actions {
			display: flex;
			flex-direction: column;
			gap: 24rpx;
			flex-shrink: 0;
		}
		.action-row {
			display: flex;
			justify-content: center;
			gap: 20rpx;
		}
		.action-row.primary { gap: 24rpx; }
		.tool-btn {
			flex: 1;
			max-width: 260rpx;
			height: 86rpx;
			background: rgba(24,144,255,0.25);
			border: 1rpx solid rgba(24,144,255,0.85);
			border-radius: 12rpx;
			display: flex;
			align-items: center;
			justify-content: center;
			color: #fff;
			font-size: 28rpx;
			font-weight: 600;
		}
		.action-btn {
			flex: 1;
			padding: 28rpx;
			background: #1890ff;
			color: #fff;
			text-align: center;
			border-radius: 12rpx;
			font-size: 28rpx;
		}
		.action-btn.done { background: #52c41a; opacity: 0.9; }
		.prompt {
			padding: 20rpx;
			color: #faad14;
			font-size: 26rpx;
			text-align: center;
		}
		.role-me {
			display: flex;
			align-items: center;
			gap: 20rpx;
		}
		.role-marker {
			min-width: 110rpx;
			height: 110rpx;
			padding: 0 20rpx;
			border-radius: 18rpx;
			background: rgba(24,144,255,0.4);
			color: #fff;
			display: flex;
			align-items: center;
			justify-content: center;
			font-size: 28rpx;
			font-weight: 700;
		}
		.role-me-text {
			display: flex;
			flex-direction: column;
			gap: 10rpx;
		}
		.role-name {
			color: #fff;
			font-size: 30rpx;
			font-weight: 700;
		}
		.role-no {
			color: rgba(255,255,255,0.75);
			font-size: 24rpx;
		}
		.role-me-loading {
			color: rgba(255,255,255,0.7);
			font-size: 24rpx;
		}
		.task-tip {
			color: rgba(255,255,255,0.7);
			font-size: 24rpx;
			margin-bottom: 16rpx;
		}
		.taskbook-box {
			display: flex;
			flex-direction: column;
			gap: 16rpx;
		}
		.task-block {
			padding: 18rpx;
			border-radius: 14rpx;
			background: rgba(0,0,0,0.18);
			border: 1rpx solid rgba(255,255,255,0.10);
		}
		.task-title {
			color: #fff;
			font-weight: 700;
			font-size: 26rpx;
			margin-bottom: 12rpx;
		}
		.task-items {
			display: flex;
			flex-direction: column;
			gap: 10rpx;
		}
		.custom-task-items {
			margin-top: 16rpx;
		}
		.task-item {
			display: flex;
			align-items: center;
			gap: 12rpx;
		}
		.custom-input {
			flex: 1;
			min-width: 0;
			background: rgba(255,255,255,0.12);
			border-radius: 8rpx;
			padding: 8rpx 10rpx;
			color: #fff;
			font-size: 22rpx;
		}
		.task-label {
			display: flex;
			align-items: center;
			gap: 12rpx;
		}
		.task-text {
			color: rgba(255,255,255,0.92);
			font-size: 24rpx;
		}
		.report-block .submit-row {
			margin-top: 14rpx;
			display: flex;
			justify-content: space-between;
			align-items: center;
			gap: 16rpx;
		}
		.summary-image-list {
			margin-top: 12rpx;
			display: flex;
			flex-wrap: wrap;
			gap: 12rpx;
		}
		.summary-image-item {
			width: 166rpx;
		}
		.summary-image {
			width: 166rpx;
			height: 166rpx;
			border-radius: 10rpx;
			border: 1rpx solid rgba(255,255,255,0.15);
			background: rgba(0,0,0,0.2);
		}
		.summary-image-remove {
			margin-top: 6rpx;
			text-align: center;
			color: #ffccc7;
			font-size: 22rpx;
		}
		.submit-status {
			color: rgba(255,255,255,0.8);
			font-size: 24rpx;
		}
		.selector-box {
			padding: 12rpx 14rpx;
			border-radius: 10rpx;
			border: 1rpx solid rgba(255,255,255,0.2);
			background: rgba(255,255,255,0.08);
			color: #fff;
			font-size: 24rpx;
			min-width: 240rpx;
		}
		.submit-btn {
			background: #13c2c2;
			color: #fff;
			padding: 12rpx 22rpx;
			border-radius: 10rpx;
			font-size: 24rpx;
		}
		.submit-btn.disabled {
			background: #4b5563;
			color: rgba(255,255,255,0.8);
		}
		.report-submit-btn.submitting {
			background: #faad14;
			color: #111;
		}
		.report-submit-btn.done {
			background: #52c41a;
			color: #fff;
		}
	}
</style>
