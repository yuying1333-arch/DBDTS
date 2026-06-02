<template>
  <div class="scene-page">
    <div class="scene-toolbar" @mousemove="pokeToolbar">
      <div class="toolbar-left">
        <el-tag v-if="state" size="small" :type="stateTagType">{{ stateText }}</el-tag>
        <el-tag v-if="state?.currentNodeCode" size="small" type="warning">节点：{{ getNodeDisplayText(state?.currentNodeCode, state?.currentNodeName) }}</el-tag>
        <span class="time" v-if="elapsedText">{{ elapsedText }}</span>
      </div>
      <div class="toolbar-right" :class="{ hidden: !toolbarVisible }">
        <el-button size="small" @click="goProjectSelect">切换项目</el-button>
        <el-button size="small" type="danger" plain @click="logout">退出</el-button>
        <el-button size="small" type="primary" @click="onStart">开始</el-button>
        <el-button size="small" @click="onPauseResume">{{ pauseResumeText }}</el-button>
        <el-button size="small" type="danger" @click="onEnd" :disabled="ending || state?.status === 3 || state?.Status === 3">结束</el-button>
        <el-button size="small" type="warning" @click="onNextNode">下一节点</el-button>
        <el-button size="small" @click="goReview">复盘</el-button>
        <el-button size="small" @click="openSettings">设置</el-button>
      </div>
    </div>

    <div class="scene-content" :class="{ collapsed: panelCollapsed }" @mousemove="pokeToolbar">
      <div class="video-panel">
        <video
          ref="videoRef"
          class="scene-video"
          :src="currentVideoUrl"
          controls
          preload="metadata"
          @timeupdate="onVideoTimeUpdate"
        />
        <div v-if="!currentVideoUrl" class="video-empty-tip">请在“编辑项目”中配置并绑定项目视频资源</div>
      </div>
      <div class="panel-shell">
        <div class="panel-toggle" @click="panelCollapsed = !panelCollapsed">
          <span>{{ panelCollapsed ? '◀' : '▶' }}</span>
        </div>
        <div v-show="!panelCollapsed" class="right-panel">
          <div class="panel-cards">
            <div
              v-for="card in panelCards"
              :key="card.key"
              class="panel-card"
              :class="{ active: activePanel === card.key }"
              @click="switchPanel(card.key)"
            >
              {{ card.label }}
            </div>
          </div>

          <div class="panel-body">
            <template v-if="activePanel === 'scene'">
              <div class="panel-discussion">
                <div class="scene-card-header">
                  <div class="scene-card-title">
                    当前节点：{{ getNodeDisplayText(state?.currentNodeCode, state?.currentNodeName) }}
                  </div>
                  <div class="scene-node-toggle" @click.stop="nodeSectionCollapsed = !nodeSectionCollapsed">
                    <span>{{ nodeSectionCollapsed ? '▶' : '▼' }}</span>
                  </div>
                </div>
                <div v-show="!nodeSectionCollapsed" class="timeline-items clickable">
                  <el-tag
                    v-for="n in nodeTimeline"
                    :key="n.nodeCode"
                    size="small"
                    :type="getNodeTagType(n.nodeCode)"
                    class="node-tag"
                    @click="selectNodeSegment(n)"
                  >
                    {{ n.nodeName }}
                  </el-tag>
                </div>
                <div class="messages" ref="listRef">
                  <div v-for="item in discussionThreads" :key="item.root.id" class="thread">
                    <div class="msg" :class="isTeacherMessage(item.root) ? 'msg-teacher' : 'msg-student'">
                      <div class="meta" :class="{ 'meta-teacher': isTeacherMessage(item.root) }">
                        <span class="name">{{ resolveDisplayName(item.root) }}</span>
                        <span class="role">{{ getRoleMetaText(item.root) }}</span>
                        <span class="time">{{ item.root.createDate }}</span>
                      </div>
                      <div class="bubble" :class="resolveDiscussionBubbleClass(item.root)">{{ getDisplayMessageContent(item.root.content) }}</div>
                    </div>
                    <div v-for="reply in item.replies" :key="reply.id" class="msg msg-teacher">
                      <div class="meta meta-teacher">
                        <span class="name">{{ resolveDisplayName(reply) }}</span>
                        <span class="role">教师</span>
                        <span class="time">{{ reply.createDate }}</span>
                      </div>
                      <div class="bubble bubble-teacher">{{ reply.content }}</div>
                    </div>
                  </div>
                </div>
                <el-input v-model="text" type="textarea" :rows="3" placeholder="输入讨论内容…" />
                <div class="actions">
                  <el-button @click="refreshDiscussion">刷新</el-button>
                  <el-button type="primary" :disabled="!canSend" @click="sendDiscussion">发送</el-button>
                </div>
              </div>
            </template>

            <template v-else-if="activePanel === 'tasks'">
              <div class="task-panel">
                <div class="task-toolbar">
                  <el-select v-model="selectedNodeCode" filterable clearable placeholder="筛选节点" style="width:220px" @change="refreshFlowData">
                    <el-option v-for="n in flowNodes" :key="n.id" :label="`${n.nodeName}`" :value="n.nodeCode" />
                  </el-select>
                  <el-select v-model="selectedTaskRole" clearable placeholder="按身份组筛选" style="width:220px">
                    <el-option v-for="r in taskRoleOptions" :key="r" :label="r" :value="r" />
                  </el-select>
                  <el-button @click="refreshFlowData">刷新</el-button>
                </div>
                <el-empty v-if="!taskNodeHasActivatedRoles" description="当前节点未配置身份组任务" />
                <div v-else ref="taskTableWrapRef" class="task-table-wrap">
                  <el-table
                    v-if="filteredTaskProgressRows.length"
                    class="task-progress-table"
                    :data="filteredTaskProgressRows"
                    size="small"
                    border
                    :style="{ width: `${taskTableLayout.tableWidth}px` }"
                  >
                    <el-table-column
                      prop="roleName"
                      label="身份组"
                      :width="taskTableLayout.roleName"
                      :resizable="false"
                      show-overflow-tooltip
                    />
                    <el-table-column
                      prop="studentName"
                      label="学员"
                      :width="taskTableLayout.studentName"
                      :resizable="false"
                      show-overflow-tooltip
                    />
                    <el-table-column label="状态" :width="taskTableLayout.status" :resizable="false">
                      <template #default="{ row }">
                        <el-tag v-if="row.noMember" size="small" type="info">暂无学员</el-tag>
                        <el-tag v-else size="small" :type="row.submitted ? 'success' : 'info'">{{ row.submitted ? '已提交' : '待提交' }}</el-tag>
                      </template>
                    </el-table-column>
                  </el-table>
                  <el-empty v-if="!filteredTaskProgressRows.length" description="暂无匹配数据" />
                </div>
              </div>
            </template>

            <template v-else>
              <div class="evidence-toolbar">
                <el-select v-model="selectedEvidenceRole" clearable placeholder="按身份组筛选" style="width:220px">
                  <el-option v-for="r in evidenceRoleOptions" :key="r" :label="r" :value="r" />
                </el-select>
              </div>
              <div class="evidence-list">
                <div v-for="row in filteredStudentActions" :key="row.id" class="evidence-card">
                  <div class="evidence-header">{{ row.userName || '-' }} - {{ row.roleName || '-' }}</div>
                  <div class="evidence-items">
                    <div v-for="(txt, idx) in parseSubmittedItems(row.textContent)" :key="`${row.id}_${idx}`" class="evidence-item">
                      {{ txt }}
                    </div>
                  </div>
                </div>
                <el-empty v-if="!filteredStudentActions.length" description="暂无学生提交内容" />
              </div>
            </template>
          </div>
        </div>
      </div>
    </div>

    <el-dialog v-model="settingsVisible" title="设置" width="520px">
      <el-form label-width="110px">
        <el-form-item label="允许角色发言">
          <el-switch v-model="settings.allowSpeak" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="settings.note" type="textarea" :rows="3" placeholder="用于教师端记录" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="settingsVisible=false">取消</el-button>
        <el-button type="primary" @click="saveSettings">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useStore } from 'vuex'
import { ElMessage } from 'element-plus'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'

const router = useRouter()
const store = useStore()
const { projectId } = useTeacherProject()

const state = ref(null)
const timer = ref(null)
const pollingTimer = ref(null)
const elapsedSecondsLocal = ref(0)
const ending = ref(false)
const activePanel = ref('scene')
const panelCollapsed = ref(false)
const nodeSectionCollapsed = ref(false)

const messages = ref([])
const memberRoleMap = ref({})
const memberNameMap = ref({})
const text = ref('')
const listRef = ref(null)
const flowNodes = ref([])
const flowActions = ref([])
const projectMembers = ref([])
const drillRoleTaskMap = ref({})
const projectResources = ref([])
const selectedNodeCode = ref('')
const selectedTaskRole = ref('')
const selectedEvidenceRole = ref('')
const taskTableWrapRef = ref(null)
const taskTableContainerWidth = ref(360)
let taskTableResizeObserver = null
const videoRef = ref(null)
const currentSegmentEnd = ref(null)
const currentSegmentStart = ref(0)
const syncedNodeCode = ref('')
const previewNodeCode = ref('')
const pendingPlayback = ref(null)
const playbackSeq = ref(0)
const panelCards = [
  { key: 'scene', label: '事故现场展示' },
  { key: 'tasks', label: '身份组任务推进' },
  { key: 'evidence', label: '学生提交内容' }
]
const nodeTimeline = computed(() => (flowNodes.value || [])
  .filter(x => Number.isFinite(Number(x.videoStartSeconds)))
  .map(x => ({
    ...x,
    resourceId: x.resourceId ? Number(x.resourceId) : null,
    resourceName: getResourceName(x.resourceId),
    videoStartSeconds: Number(x.videoStartSeconds || 0),
    videoEndSeconds: x.videoEndSeconds == null ? null : Number(x.videoEndSeconds || 0)
  }))
  .sort((a, b) => {
    const aOrder = Number(a.orderNo ?? 0)
    const bOrder = Number(b.orderNo ?? 0)
    if (aOrder !== bOrder) return aOrder - bOrder
    return Number(a.id ?? 0) - Number(b.id ?? 0)
  }))
const currentVideoUrl = computed(() => {
  const code = state.value?.currentNodeCode || previewNodeCode.value || selectedNodeCode.value
  const node = (flowNodes.value || []).find(x => x.nodeCode === code)
  const byNode = getResourceVideoUrl(node?.resourceId)
  if (byNode) return byNode
  const active = (projectResources.value || []).find(x => x.enable === 1) || (projectResources.value || [])[0]
  return active?.videoUrl || ''
})

const toolbarVisible = ref(true)
let toolbarTimer = null
function pokeToolbar() {
  toolbarVisible.value = true
  if (toolbarTimer) clearTimeout(toolbarTimer)
  toolbarTimer = setTimeout(() => { toolbarVisible.value = false }, 1800)
}

const stateText = computed(() => {
  const s = state.value?.status ?? 0
  return s === 1 ? '运行中' : s === 2 ? '已暂停' : s === 3 ? '已结束' : '未开始'
})
const stateTagType = computed(() => {
  const s = state.value?.status ?? 0
  return s === 1 ? 'success' : s === 2 ? 'warning' : s === 3 ? 'danger' : 'info'
})

function formatElapsedSeconds(seconds) {
  const diff = Math.max(0, Number(seconds) || 0)
  const hh = String(Math.floor(diff / 3600)).padStart(2, '0')
  const mm = String(Math.floor((diff % 3600) / 60)).padStart(2, '0')
  const ss = String(Math.floor(diff % 60)).padStart(2, '0')
  return `已运行 ${hh}:${mm}:${ss}`
}

const elapsedText = computed(() => {
  const s = state.value?.status ?? state.value?.Status ?? 0
  if (s === 0 && elapsedSecondsLocal.value <= 0) return ''
  return formatElapsedSeconds(elapsedSecondsLocal.value)
})

const pauseResumeText = computed(() => (state.value?.status === 2 ? '恢复' : '暂停'))
const canSend = computed(() => projectId.value > 0 && text.value.trim().length > 0)
const studentActions = computed(() => (flowActions.value || []).filter(x => (x.roleName || '').length > 0))
const evidenceRoleOptions = computed(() => Array.from(new Set(studentActions.value.map(x => x.roleName).filter(Boolean))))
const filteredStudentActions = computed(() => {
  const role = String(selectedEvidenceRole.value || '').trim()
  if (!role) return studentActions.value
  return studentActions.value.filter(x => String(x.roleName || '') === role)
})
const discussionThreads = computed(() => {
  const rows = messages.value || []
  const roots = []
  const rootSet = new Set()
  const replyMap = new Map()
  const idSet = new Set(rows.map(x => Number(x.id)))
  rows.forEach((m) => {
    const pid = Number(m.parentMessageId || 0)
    if (pid > 0 && idSet.has(pid)) {
      const list = replyMap.get(pid) || []
      list.push(m)
      replyMap.set(pid, list)
      return
    }
    roots.push(m)
    rootSet.add(Number(m.id))
  })
  return roots.map((root) => ({
    root,
    replies: (replyMap.get(Number(root.id)) || [])
  }))
})

function getNodeNameByCode(nodeCode) {
  if (!nodeCode) return ''
  const hit = (flowNodes.value || []).find(x => x.nodeCode === nodeCode)
  return hit?.nodeName || ''
}

function getResourceName(resourceId) {
  const rid = Number(resourceId || 0)
  if (rid <= 0) return ''
  const row = (projectResources.value || []).find(x => Number(x.id) === rid)
  return row?.resourceName || ''
}

function getResourceVideoUrl(resourceId) {
  const rid = Number(resourceId || 0)
  if (rid <= 0) return ''
  const row = (projectResources.value || []).find(x => Number(x.id) === rid)
  return row?.videoUrl || ''
}

function getNodeDisplayText(nodeCode, nodeName) {
  const name = nodeName || getNodeNameByCode(nodeCode)
  if (name) return name
  return getStageDisplayText(nodeCode)
}

function getStageDisplayText(stageCode) {
  const code = String(stageCode || '').toLowerCase()
  const map = {
    scene: '事故场景',
    report: '事故报告',
    recovery: '灾后恢复',
    end: '演练结束'
  }
  return map[code] || stageCode || '-'
}

function loadState() {
  if (!projectId.value) return
  drillApi.getState(projectId.value, false).then((res) => {
    if (res.status) {
      state.value = res.data
      const elapsed = Number(res?.data?.elapsedSeconds ?? 0)
      elapsedSecondsLocal.value = elapsed > 0 ? elapsed : 0
      if (!selectedNodeCode.value && res?.data?.currentNodeCode) {
        selectedNodeCode.value = res.data.currentNodeCode
      }
      if (!previewNodeCode.value && res?.data?.currentNodeCode) {
        previewNodeCode.value = res.data.currentNodeCode
      }
      const nodeCode = res?.data?.currentNodeCode || ''
      if (nodeCode && nodeCode !== syncedNodeCode.value) {
        const r = resolveSegmentRange(nodeCode, res?.data?.currentNodeVideoStartSeconds, res?.data?.currentNodeVideoEndSeconds)
        applyNodePlayback(nodeCode, r.start, r.end, false)
      }
    }
  })
}

function goProjectSelect() { router.push('/teacher/project-select') }
function logout() {
  store.commit('clearUserInfo')
  router.replace('/login')
}
function switchPanel(panel) {
  activePanel.value = panel
  if (panel === 'scene') refreshDiscussion()
  if (panel === 'tasks' || panel === 'evidence') refreshFlowData()
  if (panel === 'tasks') nextTick(() => bindTaskTableResizeObserver())
}

function onStart() {
  if (!projectId.value) return ElMessage.warning('请先选择项目')
  drillApi.start(projectId.value, true).then((res) => {
    if (res.status) {
      ElMessage.success(res.message || '已开始')
      // 新一轮演练启动后，先清空前一轮本地痕迹，避免短暂显示旧操作
      messages.value = []
      flowActions.value = []
      pendingPlayback.value = null
      syncedNodeCode.value = ''
      previewNodeCode.value = ''
      loadState()
    } else ElMessage.error(res.message || '操作失败')
  })
}

function onPauseResume() {
  if (!projectId.value) return ElMessage.warning('请先选择项目')
  const s = state.value?.status
  const fn = s === 2 ? drillApi.resume : drillApi.pause
  fn(projectId.value, true).then((res) => {
    if (res.status) {
      ElMessage.success(res.message || '操作成功')
      loadState()
    } else ElMessage.error(res.message || '操作失败')
  })
}

function onEnd() {
  if (!projectId.value) return ElMessage.warning('请先选择项目')
  const s = state.value?.status ?? state.value?.Status ?? 0
  if (ending.value || s === 3) return
  ending.value = true
  drillApi.end(projectId.value, true).then((res) => {
    if (res.status) {
      ElMessage.success(res.message || '已结束')
      loadState()
      if (projectId.value) drillApi.setStage(projectId.value, 'review', false)
      router.push('/teacher/review')
    } else ElMessage.error(res.message || '操作失败')
  }).finally(() => {
    ending.value = false
  })
}

function onNextNode() {
  if (!projectId.value) return ElMessage.warning('请先选择项目')
  drillApi.nextNode(projectId.value, true).then((res) => {
    if (res.status) {
      ElMessage.success(res.message || '已进入下一节点')
      const r = resolveSegmentRange(res?.data?.currentNodeCode, res?.data?.currentNodeVideoStartSeconds, res?.data?.currentNodeVideoEndSeconds)
      const nodeCode = res?.data?.currentNodeCode || ''
      if (nodeCode) applyNodePlayback(nodeCode, r.start, r.end, true)
      loadState()
      refreshFlowData()
      if (String(res?.data?.currentStage || '').toLowerCase() === 'report') {
        router.push('/teacher/report')
      }
    } else ElMessage.error(res.message || '操作失败')
  })
}

function goReview() {
  if (!projectId.value) return ElMessage.warning('请先选择项目')
  const s = state.value?.status ?? state.value?.Status ?? 0
  if (s !== 3) {
    if (ending.value) return
    ending.value = true
    drillApi.end(projectId.value, true).then((res) => {
      if (!res.status) return ElMessage.error(res.message || '结束失败')
      ElMessage.success(res.message || '已结束')
      drillApi.setStage(projectId.value, 'review', false)
      router.push('/teacher/review')
    }).finally(() => {
      ending.value = false
    })
    return
  }
  drillApi.setStage(projectId.value, 'review', false)
  router.push('/teacher/review')
}

function refreshDiscussion() {
  if (!projectId.value) return
  const nodeCode = state.value?.currentNodeCode || ''
  Promise.all([
    drillApi.getMessages(projectId.value, 'discussion', false, nodeCode),
    drillApi.getMessages(projectId.value, 'speech', false, nodeCode)
  ]).then(([discussionRes, speechRes]) => {
    if (!discussionRes?.status && !speechRes?.status) return
    const discussionRows = (discussionRes?.data || []).map(x => ({
      id: x.id ?? x.Id,
      userId: x.userId ?? x.UserId ?? null,
      content: x.content ?? x.Content,
      userName: x.userName ?? x.UserName,
      roleName: x.roleName ?? x.RoleName,
      createDate: x.createDate ?? x.CreateDate,
      parentMessageId: x.parentMessageId ?? x.ParentMessageId ?? null,
      nodeCode: x.nodeCode ?? x.NodeCode ?? '',
      channel: 'discussion',
      toRoleName: ''
    }))
    const speechRows = (speechRes?.data || []).map((x) => {
      const rawContent = String(x.content ?? x.Content ?? '')
      let text = rawContent
      let toRoleName = ''
      try {
        const jo = JSON.parse(rawContent)
        text = jo.text || rawContent
        toRoleName = jo.toRoleName || ''
      } catch (e) {}
      return {
        id: x.id ?? x.Id,
        userId: x.userId ?? x.UserId ?? null,
        content: text,
        userName: x.userName ?? x.UserName,
        roleName: x.roleName ?? x.RoleName,
        createDate: x.createDate ?? x.CreateDate,
        parentMessageId: null,
        nodeCode: x.nodeCode ?? x.NodeCode ?? '',
        channel: 'speech',
        toRoleName
      }
    })
    messages.value = [...discussionRows, ...speechRows].sort((a, b) => {
      const ad = String(a.createDate || '')
      const bd = String(b.createDate || '')
      if (ad === bd) return Number(b.id || 0) - Number(a.id || 0)
      return bd.localeCompare(ad)
    })
  })
}

function loadMemberRoles() {
  if (!projectId.value) return
  drillApi.getMembers(projectId.value, null, false).then((res) => {
    if (!res?.status) return
    const map = {}
    const nameMap = {}
    const members = []
    ;(res.data || []).forEach((row) => {
      const role = String(row.roleName ?? row.RoleName ?? '').trim()
      const account = String(row.userName ?? row.UserName ?? '').trim()
      const trueName = String(row.userTrueName ?? row.UserTrueName ?? '').trim()
      const userId = Number(row.userId ?? row.UserId ?? 0)
      members.push({ userId, userName: account, userTrueName: trueName, roleName: role })
      if (userId > 0) {
        if (role) map[`id_${userId}`] = role
        if (trueName) nameMap[`id_${userId}`] = trueName
        else if (account) nameMap[`id_${userId}`] = account
      }
      if (role && account) map[`login_${account}`] = role
      if (role && trueName) map[`name_${trueName}`] = role
      if (account && trueName) nameMap[`login_${account}`] = trueName
      if (trueName) nameMap[`name_${trueName}`] = trueName
      else if (account) nameMap[`name_${account}`] = account
    })
    projectMembers.value = members
    memberRoleMap.value = map
    memberNameMap.value = nameMap
  })
}

function sendDiscussion() {
  const c = text.value.trim()
  if (!c) return
  drillApi.sendMessage(projectId.value, 'discussion', c, true, { nodeCode: state.value?.currentNodeCode || '' }).then((res) => {
    if (res.status) {
      text.value = ''
      refreshDiscussion()
    } else {
      ElMessage.error(res.message || '发送失败')
    }
  })
}

function loadFlowNodes() {
  if (!projectId.value) return
  drillApi.getFlowNodes(projectId.value, false).then((res) => {
    if (!res.status) return
    flowNodes.value = (res.data || []).map(x => ({
      id: x.id ?? x.Id,
      nodeCode: x.nodeCode ?? x.NodeCode,
      nodeName: x.nodeName ?? x.NodeName,
      orderNo: x.orderNo ?? x.OrderNo ?? 0,
      resourceId: x.resourceId ?? x.ResourceId ?? null,
      videoStartSeconds: x.videoStartSeconds ?? x.VideoStartSeconds ?? 0,
      videoEndSeconds: x.videoEndSeconds ?? x.VideoEndSeconds ?? null
    })).sort((a, b) => {
      const ao = Number(a.orderNo ?? 0)
      const bo = Number(b.orderNo ?? 0)
      if (ao !== bo) return ao - bo
      return Number(a.id ?? 0) - Number(b.id ?? 0)
    })
  })
}

function loadProjectResources() {
  if (!projectId.value) return
  drillApi.getProjectResources(projectId.value, false).then((res) => {
    if (!res.status) {
      projectResources.value = []
      return
    }
    projectResources.value = (res.data || []).map((x) => ({
      id: x.id ?? x.Id,
      resourceName: x.resourceName ?? x.ResourceName ?? '',
      videoUrl: x.videoUrl ?? x.VideoUrl ?? '',
      enable: x.enable ?? x.Enable ?? 1
    }))
  })
}

function applyNodePlayback(nodeCode, start, end, autoPlay) {
  const node = (flowNodes.value || []).find(x => x.nodeCode === nodeCode)
  const req = {
    id: ++playbackSeq.value,
    nodeCode,
    start: Math.max(0, Number(start || 0)),
    end: end == null ? null : Math.max(0, Number(end || 0)),
    autoPlay: !!autoPlay,
    resourceId: node?.resourceId ?? null
  }
  pendingPlayback.value = req
  previewNodeCode.value = nodeCode || previewNodeCode.value
  syncedNodeCode.value = nodeCode || syncedNodeCode.value
  selectedNodeCode.value = nodeCode || selectedNodeCode.value
  nextTick(() => tryApplyPendingPlayback(req))
}

function tryApplyPendingPlayback(req) {
  const video = videoRef.value
  if (!video || !req || pendingPlayback.value?.id !== req.id) return
  const expectedUrl = getResourceVideoUrl(req.resourceId)
  if (expectedUrl && currentVideoUrl.value !== expectedUrl) return
  seekVideoByRange(req.start, req.end, req.autoPlay, req.id)
}

function seekVideoByRange(seconds, endSeconds = null, autoPlay = false, playRequestId = 0) {
  const target = Math.max(0, Number(seconds) || 0)
  currentSegmentStart.value = target
  currentSegmentEnd.value = endSeconds == null ? null : Math.max(0, Number(endSeconds || 0))
  const video = videoRef.value
  if (!video) return
  const doSeek = () => {
    if (playRequestId && pendingPlayback.value?.id && pendingPlayback.value.id !== playRequestId) return
    video.currentTime = target
    if (autoPlay) {
      video.play().catch(() => {})
    }
  }
  if (video.readyState >= 1) doSeek()
  else {
    const onLoaded = () => {
      doSeek()
      video.removeEventListener('loadedmetadata', onLoaded)
    }
    video.addEventListener('loadedmetadata', onLoaded)
  }
}

function onVideoTimeUpdate() {
  if (currentSegmentEnd.value == null) return
  const video = videoRef.value
  if (!video) return
  if (video.currentTime >= currentSegmentEnd.value) {
    // 节点时间段轮播：到段末回到段首继续播放
    video.currentTime = currentSegmentStart.value || 0
    video.play().catch(() => {})
  }
}

function resolveSegmentRange(nodeCode, startOverride, endOverride) {
  const list = (nodeTimeline.value || []).slice().sort((a, b) => {
    if (a.orderNo !== b.orderNo) return Number(a.orderNo || 0) - Number(b.orderNo || 0)
    return Number(a.videoStartSeconds || 0) - Number(b.videoStartSeconds || 0)
  })
  const idx = list.findIndex(x => x.nodeCode === nodeCode)
  const current = idx >= 0 ? list[idx] : null
  const start = Math.max(0, Number(startOverride ?? list[idx]?.videoStartSeconds ?? 0))
  let end = endOverride
  if (end == null && idx >= 0 && idx < list.length - 1) {
    const nextNode = list[idx + 1]
    const sameResource = Number(nextNode?.resourceId || 0) === Number(current?.resourceId || 0)
    if (sameResource) {
      const nextStart = Number(nextNode?.videoStartSeconds ?? -1)
      if (nextStart > start) end = nextStart
    }
  }
  if (end != null) {
    const e = Math.max(start, Number(end || 0))
    return { start, end: e }
  }
  return { start, end: null }
}

function selectNodeSegment(node) {
  if (!node?.nodeCode) return
  if (!projectId.value) return
  drillApi.setNode(projectId.value, node.nodeCode, true).then((res) => {
    if (!res.status) return ElMessage.error(res.message || '切换节点失败')
    const nodeCode = res?.data?.currentNodeCode || node.nodeCode
    const r = resolveSegmentRange(nodeCode, res?.data?.currentNodeVideoStartSeconds, res?.data?.currentNodeVideoEndSeconds)
    applyNodePlayback(nodeCode, r.start, r.end, true)
    loadState()
    refreshFlowData()
  })
}

function getNodeTagType(nodeCode) {
  if (nodeCode === state.value?.currentNodeCode) return 'warning'
  if (nodeCode === previewNodeCode.value) return 'success'
  return 'info'
}

function isTeacherMessage(message) {
  if (Number(message?.parentMessageId || 0) > 0) return true
  const role = resolveRoleName(message).toLowerCase()
  const name = String(message?.userName || '').toLowerCase()
  return role.includes('教师') || role.includes('老师') || role.includes('teacher') || name.includes('teacher')
}
function resolveRoleName(message) {
  const direct = String(message?.roleName || '').trim()
  if (direct) return direct
  const userId = Number(message?.userId || 0)
  if (userId > 0 && memberRoleMap.value[`id_${userId}`]) return memberRoleMap.value[`id_${userId}`]
  const userName = String(message?.userName || '').trim()
  if (userName && memberRoleMap.value[`name_${userName}`]) return memberRoleMap.value[`name_${userName}`]
  if (userName && memberRoleMap.value[`login_${userName}`]) return memberRoleMap.value[`login_${userName}`]
  return ''
}
function resolveDisplayName(message) {
  const direct = String(message?.userName || '').trim()
  if (direct) return direct
  const userId = Number(message?.userId || 0)
  if (userId > 0 && memberNameMap.value[`id_${userId}`]) return memberNameMap.value[`id_${userId}`]
  return isTeacherMessage(message) ? '教师' : '匿名'
}
function isSelfInitiatedMessage(message) {
  const txt = String(message?.content || '')
  return txt.startsWith('[自主发言]')
}
function getDisplayMessageContent(content) {
  return String(content || '').replace(/^\[自主发言\]\s*/, '')
}
function resolveDiscussionBubbleClass(message) {
  if (isTeacherMessage(message)) return 'bubble-teacher'
  if (isSelfInitiatedMessage(message)) return 'bubble-self'
  if (message?.channel === 'speech') return 'bubble-self'
  return 'bubble-student'
}
function getRoleMetaText(message) {
  if (isTeacherMessage(message)) return '教师'
  const base = resolveRoleName(message) || '学员'
  if (message?.channel === 'speech' && message?.toRoleName) {
    return `${base} 发送给 ${message.toRoleName}`
  }
  return `${base}${isSelfInitiatedMessage(message) ? '[自主发言]' : ''}`
}
function parseSubmittedItems(textContent) {
  const text = String(textContent || '').trim()
  if (!text) return ['（空）']
  return text.split(/\r?\n/).map(x => x.trim()).filter(Boolean)
}

function normalizeRoleName(roleName) {
  return String(roleName || '')
    .trim()
    .toLowerCase()
    .replace(/[\s\u3000]+/g, '')
}

function parseRoleTaskGroups(taskBookJson) {
  if (!taskBookJson) return []
  try {
    const obj = typeof taskBookJson === 'string' ? JSON.parse(taskBookJson) : taskBookJson
    const tasks = Array.isArray(obj?.tasks) ? obj.tasks : []
    return tasks.map((t, idx) => {
      const id = String(t?.id || `task_${idx + 1}`).trim()
      const title = String(t?.title || `角色任务${idx + 1}`).trim() || `角色任务${idx + 1}`
      const arr = Array.isArray(t?.items) ? t.items : []
      const items = arr.map((it) => String(it?.text || '').trim()).filter(Boolean)
      return { id, title, items }
    })
  } catch (e) {
    return []
  }
}

function loadDrillRoleTaskMap() {
  drillApi.getDrillRoles(false).then((res) => {
    if (!res?.status) return
    const map = {}
    ;(res.data || []).forEach((x) => {
      const roleName = String(x.roleName ?? x.RoleName ?? '').trim()
      if (!roleName) return
      const taskBookJson = x.taskBookJson ?? x.TaskBookJson ?? ''
      map[normalizeRoleName(roleName)] = parseRoleTaskGroups(taskBookJson)
    })
    drillRoleTaskMap.value = map
  })
}

function getNodeRoleTaskSelectionsMap() {
  try {
    const raw = state.value?.settingsJson ?? state.value?.SettingsJson ?? ''
    if (!raw) return {}
    const obj = typeof raw === 'string' ? JSON.parse(raw) : raw
    const map = obj?.nodeRoleTaskSelections
    return map && typeof map === 'object' ? map : {}
  } catch (e) {
    return {}
  }
}

function getSelectedTaskIdsByNodeRole(nodeCode, roleName) {
  const code = String(nodeCode || '').trim()
  const role = String(roleName || '').trim()
  if (!code || !role) return []
  const nodeMap = getNodeRoleTaskSelectionsMap()
  const exactNode = nodeMap?.[code] || {}
  const roleHit = exactNode?.[role]
  if (Array.isArray(roleHit)) return roleHit.map(x => String(x || '').trim()).filter(Boolean)
  const hitNodeKey = Object.keys(nodeMap || {}).find(k => String(k || '').trim().toLowerCase() === code.toLowerCase())
  const nodeVal = hitNodeKey ? (nodeMap[hitNodeKey] || {}) : {}
  const roleKey = Object.keys(nodeVal || {}).find(k => normalizeRoleName(k) === normalizeRoleName(role))
  const val = roleKey ? nodeVal[roleKey] : null
  return Array.isArray(val) ? val.map(x => String(x || '').trim()).filter(Boolean) : []
}

function resolveRoleTaskTexts(nodeCode, roleName, selectedTaskIds = []) {
  const key = normalizeRoleName(roleName)
  const groups = drillRoleTaskMap.value?.[key] || []
  const selectedSet = new Set((selectedTaskIds || []).map(x => String(x || '').trim()).filter(Boolean))
  const selectedGroups = groups.filter(g => selectedSet.has(String(g.id || '').trim()))
  const texts = []
  selectedGroups.forEach((g, idx) => {
    const arr = Array.isArray(g?.items) ? g.items : []
    if (arr.length) {
      arr.forEach((txt) => {
        const t = String(txt || '').trim()
        if (t) texts.push(t)
      })
      return
    }
    const title = String(g?.title || `角色任务${idx + 1}`).trim()
    if (title) texts.push(title)
  })
  return texts
}

function getTaskPanelNodeCode() {
  return String(selectedNodeCode.value || state.value?.currentNodeCode || '').trim()
}

function getActivatedRoleTaskSelectionForNode(nodeCode) {
  const code = String(nodeCode || '').trim()
  if (!code) return {}
  const map = getNodeRoleTaskSelectionsMap()
  const exact = map?.[code]
  if (exact && typeof exact === 'object') return exact
  const hitKey = Object.keys(map || {}).find(k => String(k || '').trim().toLowerCase() === code.toLowerCase())
  const val = hitKey ? map?.[hitKey] : null
  return (val && typeof val === 'object') ? val : {}
}

function hasActivatedRolesForNode(nodeCode) {
  const nodeMap = getActivatedRoleTaskSelectionForNode(nodeCode)
  return Object.keys(nodeMap || {}).length > 0
}

const taskNodeHasActivatedRoles = computed(() => hasActivatedRolesForNode(getTaskPanelNodeCode()))

const taskProgressRows = computed(() => {
  const nodeCode = getTaskPanelNodeCode()
  if (!nodeCode || !hasActivatedRolesForNode(nodeCode)) return []
  const nodeMap = getActivatedRoleTaskSelectionForNode(nodeCode)
  const rows = []
  Object.keys(nodeMap || {}).forEach((role) => {
    const members = (projectMembers.value || []).filter((m) => (
      normalizeRoleName(m.roleName) === normalizeRoleName(role)
    ))
    if (!members.length) {
      rows.push({
        id: `empty_${nodeCode}_${normalizeRoleName(role)}`,
        roleName: role,
        studentName: '-',
        submitted: false,
        noMember: true
      })
      return
    }
    members.forEach((member) => {
      const userId = Number(member.userId ?? 0)
      const studentName = String(member.userTrueName ?? member.userName ?? '').trim() || '-'
      rows.push({
        id: `progress_${nodeCode}_${normalizeRoleName(role)}_${userId || studentName}`,
        roleName: role,
        studentName,
        submitted: hasStudentTaskSubmission(nodeCode, role, member),
        noMember: false
      })
    })
  })
  return rows
})

const taskRoleOptions = computed(() => (
  Array.from(new Set((taskProgressRows.value || []).map(x => x.roleName).filter(Boolean)))
))

const filteredTaskProgressRows = computed(() => {
  const role = String(selectedTaskRole.value || '').trim()
  const rows = taskProgressRows.value || []
  if (!role) return rows
  return rows.filter(x => String(x.roleName || '') === role)
})

const TASK_TABLE_COLUMN_LABELS = {
  roleName: '身份组',
  studentName: '学员',
  status: '状态'
}

function estimateTaskCellWidth(text, extra = 20) {
  const str = String(text || '')
  let width = extra
  for (let i = 0; i < str.length; i += 1) {
    width += str.charCodeAt(i) > 255 ? 13 : 7
  }
  return width
}

function getTaskRowStatusText(row) {
  if (row?.noMember) return '暂无学员'
  return row?.submitted ? '已提交' : '待提交'
}

function fitTaskTableColumnWidths(naturalWidths, maxTotal) {
  const keys = ['roleName', 'studentName', 'status']
  const minWidths = { roleName: 56, studentName: 48, status: 72 }
  const total = keys.reduce((sum, key) => sum + naturalWidths[key], 0)
  if (total <= maxTotal) {
    return { ...naturalWidths, tableWidth: total }
  }
  const scale = maxTotal / total
  const fitted = {}
  keys.forEach((key) => {
    fitted[key] = Math.max(minWidths[key], Math.floor(naturalWidths[key] * scale))
  })
  const fittedTotal = keys.reduce((sum, key) => sum + fitted[key], 0)
  return { ...fitted, tableWidth: Math.min(maxTotal, fittedTotal) }
}

const taskTableLayout = computed(() => {
  const rows = filteredTaskProgressRows.value || []
  const natural = {
    roleName: estimateTaskCellWidth(TASK_TABLE_COLUMN_LABELS.roleName),
    studentName: estimateTaskCellWidth(TASK_TABLE_COLUMN_LABELS.studentName),
    status: estimateTaskCellWidth(TASK_TABLE_COLUMN_LABELS.status, 28)
  }
  rows.forEach((row) => {
    natural.roleName = Math.max(natural.roleName, estimateTaskCellWidth(row.roleName))
    natural.studentName = Math.max(natural.studentName, estimateTaskCellWidth(row.studentName))
    natural.status = Math.max(natural.status, estimateTaskCellWidth(getTaskRowStatusText(row), 28))
  })
  const maxWidth = Math.max(240, taskTableContainerWidth.value || 360)
  return fitTaskTableColumnWidths(natural, maxWidth)
})

function updateTaskTableContainerWidth() {
  const el = taskTableWrapRef.value
  if (!el) return
  taskTableContainerWidth.value = el.clientWidth || 360
}

function bindTaskTableResizeObserver() {
  if (typeof ResizeObserver === 'undefined' || !taskTableWrapRef.value) return
  if (taskTableResizeObserver) taskTableResizeObserver.disconnect()
  taskTableResizeObserver = new ResizeObserver(() => updateTaskTableContainerWidth())
  taskTableResizeObserver.observe(taskTableWrapRef.value)
  updateTaskTableContainerWidth()
}

watch(activePanel, (panel) => {
  if (panel !== 'tasks') return
  nextTick(() => bindTaskTableResizeObserver())
})

watch(selectedNodeCode, () => {
  selectedTaskRole.value = ''
})

function hasStudentTaskSubmission(nodeCode, roleName, member) {
  return !!findStudentTaskSubmission(nodeCode, roleName, member)
}

function findStudentTaskSubmission(nodeCode, roleName, member) {
  const roleKey = normalizeRoleName(roleName)
  const userId = Number(member?.userId ?? 0)
  const names = new Set([
    String(member?.userTrueName ?? '').trim(),
    String(member?.userName ?? '').trim()
  ].filter(Boolean))
  return (flowActions.value || []).find((x) => {
    if (normalizeRoleName(x?.roleName) !== roleKey) return false
    if (String(x?.nodeCode || '') !== String(nodeCode || '')) return false
    if (String(x?.taskTitle || '').trim() !== '任务提交') return false
    if (Number(x?.status ?? 1) < 1) return false
    if (userId > 0 && Number(x?.userId ?? 0) === userId) return true
    const un = String(x?.userName || '').trim()
    return un && names.has(un)
  }) || null
}

function refreshFlowData() {
  if (!projectId.value) return
  const nodeCode = getTaskPanelNodeCode()
  drillApi.getFlowActions(projectId.value, nodeCode, false).then((res) => {
    if (res.status) {
      flowActions.value = (res.data || []).map(x => ({
        id: x.id ?? x.Id,
        nodeCode: x.nodeCode ?? x.NodeCode,
        nodeName: x.nodeName ?? x.NodeName,
        roleName: x.roleName ?? x.RoleName,
        taskTitle: x.taskTitle ?? x.TaskTitle,
        userId: x.userId ?? x.UserId,
        userName: x.userName ?? x.UserName,
        textContent: x.textContent ?? x.TextContent,
        occurAt: x.occurAt ?? x.OccurAt,
        status: x.status ?? x.Status ?? 0
      }))
    }
  })
}

const settingsVisible = ref(false)
const settings = reactive({ allowSpeak: true, note: '' })
function openSettings() { settingsVisible.value = true }
function saveSettings() {
  if (!projectId.value) return ElMessage.warning('请先选择项目')
  drillApi.saveSettings(projectId.value, { ...settings }, true).then((res) => {
    if (res.status) {
      ElMessage.success(res.message || '已保存')
      settingsVisible.value = false
    } else ElMessage.error(res.message || '保存失败')
  })
}

onMounted(() => {
  if (projectId.value) {
    loadState()
    loadFlowNodes()
    loadProjectResources()
    loadDrillRoleTaskMap()
    loadMemberRoles()
  }
  // 展示层每秒自增；真实值以服务端 State 返回的 elapsedSeconds 为准
  timer.value = setInterval(() => {
    const s = state.value?.status ?? state.value?.Status ?? 0
    if (s === 1) elapsedSecondsLocal.value += 1
  }, 1000)
  pollingTimer.value = setInterval(() => {
    if (projectId.value) {
      loadState()
      loadFlowNodes()
      loadProjectResources()
      if (!Object.keys(drillRoleTaskMap.value || {}).length) loadDrillRoleTaskMap()
      if (!Object.keys(memberRoleMap.value).length) loadMemberRoles()
      if (activePanel.value === 'scene') refreshDiscussion()
      if (activePanel.value === 'tasks' || activePanel.value === 'evidence') refreshFlowData()
    }
  }, 3000)
  pokeToolbar()
  nextTick(() => {
    if (activePanel.value === 'tasks') bindTaskTableResizeObserver()
  })
})
watch(currentVideoUrl, () => {
  const req = pendingPlayback.value
  if (!req) return
  nextTick(() => tryApplyPendingPlayback(req))
})
onUnmounted(() => {
  if (timer.value) clearInterval(timer.value)
  if (pollingTimer.value) clearInterval(pollingTimer.value)
  if (toolbarTimer) clearTimeout(toolbarTimer)
  if (taskTableResizeObserver) {
    taskTableResizeObserver.disconnect()
    taskTableResizeObserver = null
  }
})
</script>

<style scoped>
.scene-page {
  height: 100vh;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 10px;
  background: #000;
}
.scene-toolbar {
  border-radius: 8px;
  padding: 8px 10px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  background: rgba(255, 255, 255, 0.96);
}
.toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.toolbar-right.hidden {
  opacity: 0.2;
  transition: opacity 0.15s ease;
}
.project-name { color:#333; font-size: 13px; font-weight: 600; }
.time { color: #666; font-size: 12px; }
.scene-content {
  flex: 1;
  display: flex;
  gap: 12px;
  min-height: 0;
}
.video-panel {
  position: relative;
  flex: 1;
  min-width: 0;
  min-height: 0;
  background: #000;
  border-radius: 10px;
  overflow: hidden;
  transition: all 0.2s ease;
}
.panel-shell {
  width: 430px;
  max-width: 50vw;
  display: flex;
  gap: 6px;
  min-height: 0;
  transition: width 0.2s ease;
}
.scene-content.collapsed .panel-shell {
  width: 24px;
}
.panel-toggle {
  width: 24px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.18);
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  user-select: none;
}
.scene-video {
  width: 100%;
  height: 100%;
  min-height: 400px;
  object-fit: contain;
  background: #000;
}
.video-empty-tip {
  position: absolute;
  top: 24px;
  left: 24px;
  background: rgba(0, 0, 0, 0.62);
  color: #fff;
  font-size: 12px;
  padding: 6px 10px;
  border-radius: 6px;
}
.right-panel {
  flex: 1;
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 10px;
  padding: 10px 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  min-height: 0;
  min-width: 0;
  overflow: hidden;
}
.panel-cards {
  display: flex;
  align-items: center;
  gap: 8px;
  border-bottom: 1px solid #ebeef5;
  padding-bottom: 8px;
  overflow: auto;
  flex-wrap: nowrap;
}
.panel-card {
  border: 1px solid #dcdfe6;
  color: #606266;
  border-radius: 6px;
  padding: 6px 10px;
  white-space: nowrap;
  cursor: pointer;
  font-size: 12px;
}
.panel-card.active {
  border-color: #409eff;
  color: #409eff;
  background: #ecf5ff;
}
.panel-body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  display: flex;
  flex-direction: column;
}
.timeline-items {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}
.scene-card-header {
  position: sticky;
  top: 0;
  z-index: 2;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  background: #fff;
}
.scene-card-title {
  font-size: 13px;
  font-weight: 600;
  color: #303133;
}
.scene-node-toggle {
  width: 22px;
  height: 22px;
  border-radius: 6px;
  border: 1px solid #dcdfe6;
  color: #606266;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  user-select: none;
}
.timeline-items.clickable .node-tag {
  cursor: pointer;
}
.panel-discussion { display: flex; flex-direction: column; gap: 10px; }
.messages { max-height: 360px; overflow: auto; padding: 8px; background: #fafafa; border: 1px solid #eee; border-radius: 6px; }
.thread { margin-bottom: 12px; }
.msg { margin-bottom: 8px; max-width: 90%; }
.msg-student { margin-right: auto; }
.msg-teacher { margin-left: auto; }
.meta { font-size: 12px; color:#666; display:flex; gap:8px; margin-bottom: 4px; }
.name { font-weight:600; color:#333; }
.role { color:#1890ff; }
.time { margin-left:auto; color:#999; }
.bubble { border-radius:8px; padding:8px 10px; white-space: pre-wrap; }
.bubble-student { background:#fff; border:1px solid #e8e8e8; color: #303133; }
.bubble-self { background:#409eff; border:1px solid #2f8cff; color:#111; }
.meta-teacher { justify-content: flex-end; }
.meta-teacher .role { color: #67c23a; }
.bubble-teacher { background:#67c23a; color:#111; border:1px solid #5daf34; }
.actions { display:flex; justify-content: flex-end; gap: 8px; }
.task-toolbar { margin-bottom: 8px; display:flex; gap:8px; align-items:center; flex-wrap: wrap; }
.task-panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  overflow: hidden;
}
.task-table-wrap {
  flex: 1;
  min-height: 0;
  max-width: 100%;
  overflow: auto;
}
.task-progress-table {
  max-width: 100%;
}
:deep(.task-progress-table .el-table__header-wrapper th),
:deep(.task-progress-table .el-table__body-wrapper td) {
  user-select: none;
}
:deep(.task-progress-table .el-table__header-wrapper .el-table__column-resize-proxy) {
  display: none;
}
:deep(.task-title-cell .cell) {
  white-space: normal;
  word-break: break-word;
  line-height: 1.5;
}
.evidence-toolbar { margin-bottom: 8px; display:flex; justify-content: flex-end; }
.evidence-list { display: flex; flex-direction: column; gap: 8px; }
.evidence-card {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  background: #fafafa;
  padding: 10px;
}
.evidence-header { font-weight: 600; color: #303133; margin-bottom: 8px; }
.evidence-items { display: flex; flex-direction: column; gap: 6px; }
.evidence-item {
  border-radius: 6px;
  border: 1px solid #e4e7ed;
  background: #fff;
  padding: 6px 8px;
  color: #606266;
}

@media (max-width: 1200px) {
  .scene-content {
    flex-direction: column;
  }
  .panel-shell,
  .scene-content.collapsed .panel-shell {
    width: 100%;
    max-width: 100%;
  }
  .panel-toggle {
    width: 100%;
    height: 24px;
  }
}
</style>

