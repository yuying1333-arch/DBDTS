<template>
  <div class="page">
    <TeacherPageHeader>
      <template #left>
        <el-tag size="small" type="info">总时长：{{ formatSec(totalDurationSec) }}</el-tag>
        <el-tag size="small" type="warning">当前：{{ formatSec(replayCursorSec) }}</el-tag>
      </template>
    </TeacherPageHeader>

    <div class="grid">
      <div class="panel scene">
        <div class="panel-hd">演练节点</div>
        <div class="panel-bd node-view">
          <template v-if="orderedNodes.length">
            <div
              class="node-card all-node"
              :class="{ active: selectedDiscussionNodeCode === ALL_DISCUSSION_NODE }"
              @click="selectAllDiscussions"
            >
              <div class="node-name">全部</div>
              <div class="node-desc">截止当前时间点的所有讨论内容</div>
            </div>
            <div
              v-for="n in orderedNodes"
              :key="n.nodeCode"
              :ref="(el) => setNodeRef(n.nodeCode, el)"
              class="node-card"
              :class="{ active: selectedDiscussionNodeCode === n.nodeCode, current: currentNodeCode === n.nodeCode }"
              @click="jumpToNode(n)"
            >
              <div class="node-name">{{ n.nodeName || n.nodeCode }}</div>
              <div class="node-desc">{{ n.description || '该节点未配置描述' }}</div>
            </div>
          </template>
          <div v-else class="empty-tip">暂无演练节点数据</div>
        </div>
      </div>

      <div class="panel discussion">
        <div class="panel-hd">现场讨论</div>
        <div class="panel-bd">
          <template v-if="discussionThreadsAtCursor.length">
            <div v-for="item in discussionThreadsAtCursor" :key="item.root.id" class="discussion-thread">
              <div class="discussion-item">
                <div class="discussion-meta">
                  <span class="speaker">{{ resolveDiscussionName(item.root) }}</span>
                  <span>（{{ resolveDiscussionRole(item.root) }}）</span>
                </div>
                <div class="discussion-content">{{ item.root.content || '-' }}</div>
              </div>

              <div v-for="reply in item.replies" :key="reply.id" class="review-reply-wrap">
                <div class="review-reply-title">
                  {{ resolveDiscussionName(reply) }}【教师点评】
                </div>
                <div class="review-reply-card">
                  <div class="review-origin">{{ truncateDiscussionContent(item.root.content, 8) }}</div>
                  <div class="review-content">{{ reply.content || '-' }}</div>
                </div>
              </div>
            </div>
          </template>
          <div v-else class="empty-tip">当前时间点无讨论内容</div>
        </div>
      </div>

      <div class="panel timeline">
        <div class="panel-hd">演练复盘时间线</div>
        <div class="panel-bd">
          <div class="player">
            <el-button size="small" @click="togglePlay" :disabled="!canPlay">{{ isPlaying ? '暂停' : '播放' }}</el-button>
            <el-button size="small" @click="stopPlay" :disabled="!canPlay">停止</el-button>
            <el-button size="small" @click="jumpPrevEventPoint" :disabled="!canJumpPoint">上一时间点</el-button>
            <el-button size="small" @click="jumpNextEventPoint" :disabled="!canJumpPoint">下一时间点</el-button>
            <span class="time-label">{{ formatSec(replayCursorSec) }} / {{ formatSec(totalDurationSec) }}</span>
          </div>
          <div class="slider-wrap">
            <el-slider
              v-model="replayCursorSec"
              :min="0"
              :max="Math.max(1, totalDurationSec)"
              :step="1"
              :disabled="!canPlay"
              @change="onCursorChanged"
            />
            <div class="event-dots" v-if="canPlay">
              <span
                v-for="dot in eventDots"
                :key="dot.key"
                class="event-dot"
                :style="{ left: `${dot.left}%` }"
                :title="dot.title"
              />
            </div>
          </div>
          <div class="event-list">
            <div v-for="e in visibleEvents" :key="e.id" class="event-item">
              <span class="event-time">{{ formatSec(e.relativeSec) }}</span>
              <span class="event-text">{{ e.title || '-' }}：{{ e.content || '-' }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'
import TeacherPageHeader from './TeacherPageHeader.vue'

const { projectId } = useTeacherProject()

const events = ref([])
const discussions = ref([])
const memberRoleMap = ref({})
const memberNameMap = ref({})
const flowNodes = ref([])
const stateData = ref(null)
const replayCursorSec = ref(0)
const totalDurationSec = ref(0)
const startedAtMs = ref(0)
const ALL_DISCUSSION_NODE = '__ALL__'
const selectedDiscussionNodeCode = ref(ALL_DISCUSSION_NODE)
const isPlaying = ref(false)
const nodeRefMap = ref({})
let playTimer = null
let syncTimer = null

function toMs(v) {
  if (!v) return 0
  const t = new Date(v).getTime()
  return Number.isFinite(t) ? t : 0
}
function formatSec(sec) {
  const n = Math.max(0, Number(sec || 0))
  const h = String(Math.floor(n / 3600)).padStart(2, '0')
  const m = String(Math.floor((n % 3600) / 60)).padStart(2, '0')
  const s = String(Math.floor(n % 60)).padStart(2, '0')
  return `${h}:${m}:${s}`
}
function formatRange(start, end) {
  const s = Math.max(0, Number(start || 0))
  if (end == null) return `${formatSec(s)} - 结束`
  return `${formatSec(s)} - ${formatSec(Math.max(s, Number(end || 0)))}`
}

const orderedNodes = computed(() => {
  return (flowNodes.value || [])
    .map(x => ({
      nodeCode: x.nodeCode ?? x.NodeCode,
      nodeName: x.nodeName ?? x.NodeName,
      description: x.description ?? x.Description ?? '',
      orderNo: Number(x.orderNo ?? x.OrderNo ?? 0),
      videoStartSeconds: Number(x.videoStartSeconds ?? x.VideoStartSeconds ?? 0),
      videoEndSeconds: (x.videoEndSeconds ?? x.VideoEndSeconds) == null ? null : Number(x.videoEndSeconds ?? x.VideoEndSeconds)
    }))
    .sort((a, b) => {
      if (a.orderNo !== b.orderNo) return a.orderNo - b.orderNo
      return a.videoStartSeconds - b.videoStartSeconds
    })
})

const normalizedEvents = computed(() => {
  const base = startedAtMs.value
  return (events.value || []).map((e, idx) => {
    const occurAt = e.occurAt || e.OccurAt || e.createDate || e.CreateDate
    const atMs = toMs(occurAt)
    let relativeSec = idx
    if (base > 0 && atMs > 0) {
      relativeSec = Math.max(0, Math.floor((atMs - base) / 1000))
    }
    return {
      id: e.id ?? e.Id ?? `${idx}_${relativeSec}`,
      title: e.title ?? e.Title,
      content: e.content ?? e.Content,
      occurAt: e.occurAt ?? e.OccurAt,
      createDate: e.createDate ?? e.CreateDate,
      userName: e.userName ?? e.UserName,
      roleName: e.roleName ?? e.RoleName,
      relativeSec
    }
  }).sort((a, b) => a.relativeSec - b.relativeSec)
})

const visibleEvents = computed(() => {
  return normalizedEvents.value.filter(e => e.relativeSec <= replayCursorSec.value)
})
const normalizedDiscussions = computed(() => {
  const base = startedAtMs.value
  return (discussions.value || []).map((m, idx) => {
    const at = m.createDate ?? m.CreateDate
    const atMs = toMs(at)
    let relativeSec = idx
    if (base > 0 && atMs > 0) {
      relativeSec = Math.max(0, Math.floor((atMs - base) / 1000))
    }
    return {
      id: m.id ?? m.Id ?? `${idx}_${relativeSec}`,
      userId: m.userId ?? m.UserId ?? null,
      userName: m.userName ?? m.UserName ?? '-',
      roleName: m.roleName ?? m.RoleName ?? '',
      nodeCode: m.nodeCode ?? m.NodeCode ?? '',
      parentMessageId: m.parentMessageId ?? m.ParentMessageId ?? null,
      content: m.content ?? m.Content ?? '',
      createDate: m.createDate ?? m.CreateDate ?? '',
      relativeSec
    }
  }).sort((a, b) => a.relativeSec - b.relativeSec)
})
const discussionsAtCursor = computed(() => {
  const sec = Math.floor(Math.max(0, Number(replayCursorSec.value || 0)))
  return normalizedDiscussions.value.filter(x => x.relativeSec <= sec)
})
const discussionsBySelectedNode = computed(() => {
  if (selectedDiscussionNodeCode.value === ALL_DISCUSSION_NODE) return discussionsAtCursor.value
  return normalizedDiscussions.value.filter(x => String(x.nodeCode || '') === String(selectedDiscussionNodeCode.value || ''))
})
const discussionThreadsAtCursor = computed(() => {
  const roots = discussionsBySelectedNode.value.filter(x => Number(x.parentMessageId || 0) <= 0)
  const allRows = normalizedDiscussions.value || []
  const replyMap = new Map()
  allRows.forEach((m) => {
    const pid = Number(m.parentMessageId || 0)
    if (pid <= 0) return
    const list = replyMap.get(pid) || []
    list.push(m)
    replyMap.set(pid, list)
  })
  return roots.map((root) => ({
    root,
    replies: (replyMap.get(Number(root.id)) || []).filter(x => isTeacherDiscussion(x))
  }))
})
function resolveDiscussionRole(message) {
  const direct = String(message?.roleName || '').trim()
  if (direct) return direct
  const uid = Number(message?.userId || 0)
  if (uid > 0 && memberRoleMap.value[`id_${uid}`]) return memberRoleMap.value[`id_${uid}`]
  const uname = String(message?.userName || '').trim()
  if (uname && memberRoleMap.value[`name_${uname}`]) return memberRoleMap.value[`name_${uname}`]
  if (uname && memberRoleMap.value[`login_${uname}`]) return memberRoleMap.value[`login_${uname}`]
  return isTeacherDiscussion(message) ? '教师' : '学员'
}
function truncateDiscussionContent(content, max = 8) {
  const text = String(content || '').trim()
  if (!text) return '-'
  if (text.length <= max) return `${text}`
  return `${text.slice(0, max)}...`
}
function resolveDiscussionName(message) {
  const direct = String(message?.userName || '').trim()
  if (direct && direct !== '-') return direct
  const uid = Number(message?.userId || 0)
  if (uid > 0 && memberNameMap.value[`id_${uid}`]) return memberNameMap.value[`id_${uid}`]
  return isTeacherDiscussion(message) ? '教师' : '匿名'
}
function isTeacherDiscussion(message) {
  if (Number(message?.parentMessageId || 0) > 0) return true
  const role = String(resolveDiscussionRoleRaw(message)).toLowerCase()
  const name = String(message?.userName || '').toLowerCase()
  return role.includes('教师') || role.includes('老师') || role.includes('teacher') || name.includes('teacher')
}
function resolveDiscussionRoleRaw(message) {
  const direct = String(message?.roleName || '').trim()
  if (direct) return direct
  const uid = Number(message?.userId || 0)
  if (uid > 0 && memberRoleMap.value[`id_${uid}`]) return memberRoleMap.value[`id_${uid}`]
  const uname = String(message?.userName || '').trim()
  if (uname && memberRoleMap.value[`name_${uname}`]) return memberRoleMap.value[`name_${uname}`]
  if (uname && memberRoleMap.value[`login_${uname}`]) return memberRoleMap.value[`login_${uname}`]
  return ''
}

const currentNode = computed(() => {
  const cursor = replayCursorSec.value
  const nodes = orderedNodes.value
  if (!nodes.length) return null
  const hit = nodes.find(n => {
    const start = n.videoStartSeconds
    const end = n.videoEndSeconds == null ? Number.MAX_SAFE_INTEGER : n.videoEndSeconds
    return cursor >= start && cursor < end
  })
  if (hit) return hit
  const candidates = nodes.filter(n => n.videoStartSeconds <= cursor)
  return candidates.length ? candidates[candidates.length - 1] : nodes[0]
})
const currentNodeCode = computed(() => currentNode.value?.nodeCode || '')

const canPlay = computed(() => totalDurationSec.value > 0)
const eventPointSeconds = computed(() => {
  const secSet = new Set()
  normalizedEvents.value.forEach((e) => {
    secSet.add(Math.max(0, Number(e.relativeSec || 0)))
  })
  return Array.from(secSet).sort((a, b) => a - b)
})
const canJumpPoint = computed(() => canPlay.value && eventPointSeconds.value.length > 0)
const eventDots = computed(() => {
  if (!totalDurationSec.value) return []
  const used = new Set()
  return normalizedEvents.value
    .map((e, idx) => {
      const sec = Math.max(0, Math.min(totalDurationSec.value, Number(e.relativeSec || 0)))
      const key = `${sec}_${idx}`
      if (used.has(sec)) return null
      used.add(sec)
      const left = Number(((sec / Math.max(1, totalDurationSec.value)) * 100).toFixed(2))
      return {
        key,
        left,
        title: `${formatSec(sec)} ${e.title || ''}`.trim()
      }
    })
    .filter(Boolean)
})

function setNodeRef(code, el) {
  if (!code) return
  if (el) nodeRefMap.value[code] = el
  else delete nodeRefMap.value[code]
}
function scrollToActiveNode() {
  const code = currentNodeCode.value
  if (!code) return
  const el = nodeRefMap.value[code]
  if (!el || typeof el.scrollIntoView !== 'function') return
  el.scrollIntoView({ block: 'nearest', behavior: 'smooth' })
}

function stopPlaybackTimer() {
  if (playTimer) {
    clearInterval(playTimer)
    playTimer = null
  }
  isPlaying.value = false
}
function togglePlay() {
  if (!canPlay.value) return
  if (isPlaying.value) {
    stopPlaybackTimer()
    return
  }
  isPlaying.value = true
  playTimer = setInterval(() => {
    if (replayCursorSec.value >= totalDurationSec.value) {
      stopPlaybackTimer()
      return
    }
    replayCursorSec.value += 1
  }, 1000)
}
function stopPlay() {
  stopPlaybackTimer()
  replayCursorSec.value = 0
}
function onCursorChanged(v) {
  replayCursorSec.value = Math.max(0, Math.min(totalDurationSec.value, Number(v || 0)))
}
function jumpToNode(node) {
  if (node?.nodeCode) selectedDiscussionNodeCode.value = node.nodeCode
  replayCursorSec.value = Math.max(0, Number(node?.videoStartSeconds || 0))
}
function selectAllDiscussions() {
  selectedDiscussionNodeCode.value = ALL_DISCUSSION_NODE
}
function jumpPrevEventPoint() {
  if (!canJumpPoint.value) return
  const current = Math.floor(Number(replayCursorSec.value || 0))
  const list = eventPointSeconds.value
  const prev = list.filter(x => x < current)
  replayCursorSec.value = prev.length ? prev[prev.length - 1] : list[0]
}
function jumpNextEventPoint() {
  if (!canJumpPoint.value) return
  const current = Math.floor(Number(replayCursorSec.value || 0))
  const list = eventPointSeconds.value
  const next = list.find(x => x > current)
  replayCursorSec.value = next == null ? list[list.length - 1] : next
}

function resetProjectData() {
  stopPlaybackTimer()
  events.value = []
  discussions.value = []
  flowNodes.value = []
  stateData.value = null
  replayCursorSec.value = 0
  totalDurationSec.value = 0
  startedAtMs.value = 0
  selectedDiscussionNodeCode.value = ALL_DISCUSSION_NODE
  memberRoleMap.value = {}
  memberNameMap.value = {}
}

function calcDurationSeconds(state, list, nodes) {
  const started = toMs(state?.startedAt ?? state?.StartedAt)
  const ended = toMs(state?.endedAt ?? state?.EndedAt)
  const elapsed = Number(state?.elapsedSeconds ?? 0)
  const byState = started > 0 && ended >= started ? Math.floor((ended - started) / 1000) : elapsed
  const byEvent = list.length ? Math.max(...list.map(x => x.relativeSec || 0)) : 0
  const byNode = nodes.length ? Math.max(...nodes.map(x => Number(x.videoEndSeconds ?? x.videoStartSeconds ?? 0))) : 0
  return Math.max(0, byState, byEvent, byNode)
}

function loadReviewData(pid) {
  if (!pid) return
  resetProjectData()
  drillApi.setStage(pid, 'review', false)
  Promise.all([
    drillApi.getState(pid, false),
    drillApi.getFlowNodes(pid, false),
    drillApi.getEvents(pid, null, false),
    drillApi.getMessages(pid, 'discussion', false),
    drillApi.getMembers(pid, null, false)
  ]).then(([stateRes, nodeRes, eventRes, msgRes, memberRes]) => {
    stateData.value = stateRes?.status ? (stateRes.data || {}) : {}
    flowNodes.value = nodeRes?.status ? (nodeRes.data || []) : []
    events.value = eventRes?.status ? (eventRes.data || []) : []
    discussions.value = msgRes?.status ? (msgRes.data || []) : []
    if (memberRes?.status) {
      const roleMap = {}
      const nameMap = {}
      ;(memberRes.data || []).forEach((row) => {
        const role = String(row.roleName ?? row.RoleName ?? '').trim()
        const account = String(row.userName ?? row.UserName ?? '').trim()
        const trueName = String(row.userTrueName ?? row.UserTrueName ?? '').trim()
        const userId = Number(row.userId ?? row.UserId ?? 0)
        if (userId > 0) {
          if (role) roleMap[`id_${userId}`] = role
          if (trueName) nameMap[`id_${userId}`] = trueName
          else if (account) nameMap[`id_${userId}`] = account
        }
        if (role && account) roleMap[`login_${account}`] = role
        if (role && trueName) roleMap[`name_${trueName}`] = role
      })
      memberRoleMap.value = roleMap
      memberNameMap.value = nameMap
    }
    startedAtMs.value = toMs(stateData.value?.startedAt ?? stateData.value?.StartedAt)
    const duration = calcDurationSeconds(stateData.value, normalizedEvents.value, orderedNodes.value)
    totalDurationSec.value = duration
    replayCursorSec.value = duration
  })
}
function refreshDiscussionOnly(pid) {
  if (!pid) return
  drillApi.getMessages(pid, 'discussion', false).then((msgRes) => {
    if (!msgRes?.status) return
    discussions.value = msgRes.data || []
  })
}

watch(projectId, (pid) => {
  if (!pid) {
    resetProjectData()
    ElMessage.warning('请先选择项目')
    return
  }
  loadReviewData(pid)
}, { immediate: true })
watch(currentNodeCode, () => {
  nextTick(() => scrollToActiveNode())
})

watch(projectId, (pid) => {
  if (syncTimer) {
    clearInterval(syncTimer)
    syncTimer = null
  }
  if (!pid) return
  syncTimer = setInterval(() => {
    refreshDiscussionOnly(pid)
  }, 3000)
}, { immediate: true })

onBeforeUnmount(() => {
  stopPlaybackTimer()
  if (syncTimer) clearInterval(syncTimer)
})
</script>

<style scoped>
.page {
  height: calc(100vh - 40px);
  max-height: calc(100vh - 40px);
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 0;
  overflow: hidden;
}
.grid {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 1fr 2fr;
  grid-template-rows: 1fr 1fr;
  gap: 12px;
  overflow: hidden;
}
.panel {
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.scene {
  grid-row: 1 / span 2;
}
.panel-hd {
  padding: 10px 12px;
  font-weight: 600;
  border-bottom: 1px solid #eee;
}
.panel-bd {
  padding: 12px;
  overflow-y: auto;
  overflow-x: hidden;
  flex: 1;
  min-height: 0;
  scrollbar-width: none;
  -ms-overflow-style: none;
}
.panel-bd::-webkit-scrollbar {
  width: 0;
  height: 0;
}
.node-view {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.node-card {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 10px;
  cursor: pointer;
  background: #fff;
  transition: all 0.2s ease;
}
.node-card.active {
  border-color: #409eff;
  background: #ecf5ff;
  box-shadow: 0 0 0 1px rgba(64, 158, 255, 0.25) inset;
}
.node-card.current {
  border-style: dashed;
}
.all-node {
  background: #f7fbff;
}
.node-name {
  font-size: 15px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 6px;
}
.node-desc {
  color: #606266;
  white-space: pre-wrap;
  line-height: 1.6;
}
.empty-tip {
  color: #909399;
  text-align: center;
  margin-top: 20px;
}
.discussion-item {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 10px;
  margin-bottom: 8px;
  background: #fafafa;
}
.discussion-thread {
  margin-bottom: 14px;
}
.discussion-meta {
  font-size: 13px;
  color: #606266;
  margin-bottom: 6px;
}
.discussion-meta .speaker {
  font-weight: 600;
  color: #303133;
}
.discussion-content {
  color: #303133;
  white-space: pre-wrap;
  line-height: 1.6;
}
.review-reply-wrap {
  margin-top: 6px;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  width: 100%;
  min-width: 0;
}
.review-reply-title {
  color: #303133;
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 6px;
}
.review-reply-card {
  width: 92%;
  max-width: 100%;
  min-width: 0;
  background: #67c23a;
  border: 1px solid #5daf34;
  border-radius: 8px;
  color: #111;
  overflow: hidden;
}
.review-origin {
  margin: 10px 12px 8px;
  padding: 8px 10px;
  background: #fff;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
  color: #303133;
  font-size: 14px;
}
.review-content {
  padding: 8px 12px 12px;
  white-space: pre-wrap;
  line-height: 1.6;
}
.player {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 10px;
}
.time-label {
  margin-left: auto;
  color: #606266;
  font-size: 13px;
}
.slider-wrap {
  position: relative;
  width: 100%;
  margin-bottom: 10px;
}
.event-dots {
  pointer-events: none;
  position: absolute;
  left: 10px;
  right: 10px;
  top: 50%;
  transform: translateY(-50%);
  height: 10px;
}
.event-dot {
  position: absolute;
  width: 8px;
  height: 8px;
  margin-left: -4px;
  border-radius: 50%;
  background: #f56c6c;
  opacity: .9;
}
.event-list {
  display: grid;
  gap: 6px;
}
.event-item {
  display: flex;
  gap: 8px;
  padding: 7px 8px;
  border-radius: 6px;
  background: #f8f9fb;
}
.event-time {
  font-weight: 600;
  min-width: 64px;
  color: #409eff;
}
.event-text {
  white-space: pre-wrap;
  color: #303133;
}
.timeline :deep(.el-slider__runway) {
  background: #e4e7ed;
}
.timeline :deep(.el-slider__bar) {
  background: #409eff;
}
.timeline :deep(.el-slider__button) {
  border-color: #409eff;
}
@media (max-width: 1360px) {
  .grid {
    grid-template-columns: 1fr;
    grid-template-rows: auto;
  }
  .scene {
    grid-row: auto;
  }
}
</style>

