<template>
  <div class="page">
    <TeacherPageHeader />

    <div class="content">
      <div class="messages" ref="listRef">
        <div v-for="item in discussionThreads" :key="item.root.id" class="thread">
          <div
            class="msg"
            :class="isTeacherMessage(item.root) ? 'msg-teacher' : 'msg-student'"
            @click="onClickRoot(item.root)"
          >
            <div class="meta" :class="{ 'meta-teacher': isTeacherMessage(item.root) }">
              <span class="name">{{ resolveDisplayName(item.root) }}</span>
              <span class="role">{{ getRoleText(item.root) }}</span>
              <span class="time">{{ item.root.createDate }}</span>
            </div>
            <div class="bubble" :class="isTeacherMessage(item.root) ? 'bubble-teacher' : 'bubble-student'">{{ item.root.content }}</div>
          </div>
          <div v-for="reply in item.replies" :key="reply.id" class="msg msg-teacher">
            <div class="review-reply-title">
              {{ resolveDisplayName(reply) }}【教师点评】
            </div>
            <div class="review-reply-card">
              <div class="review-origin-mini">{{ truncateDiscussionContent(item.root.content, 8) }}</div>
              <div class="review-content">{{ reply.content }}</div>
            </div>
          </div>
        </div>
      </div>
      <div class="composer">
        <el-input v-model="text" type="textarea" :rows="3" placeholder="输入讨论内容…" />
        <div class="actions">
          <el-button @click="refresh">刷新</el-button>
          <el-button type="primary" :disabled="!canSend" @click="send">发送</el-button>
        </div>
      </div>
    </div>

    <el-dialog v-model="reviewVisible" title="点评学员发言" width="760px">
      <div class="review-target">
        {{ resolveDisplayName(reviewTarget) }}（{{ getRoleText(reviewTarget) }}）
      </div>
      <div class="review-origin">{{ reviewTarget?.content || '-' }}</div>
      <el-input v-model="reviewText" type="textarea" :rows="6" placeholder="请输入点评内容" />
      <template #footer>
        <el-button @click="closeReviewDialog">取消</el-button>
        <el-button type="primary" :disabled="!reviewText.trim()" @click="submitReview">提交点评</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'
import TeacherPageHeader from './TeacherPageHeader.vue'

const router = useRouter()
const { projectId } = useTeacherProject()

const messages = ref([])
const memberRoleMap = ref({})
const memberNameMap = ref({})
const text = ref('')
const listRef = ref(null)
const reviewVisible = ref(false)
const reviewTarget = ref(null)
const reviewText = ref('')
let timer = null

const canSend = computed(() => projectId.value > 0 && text.value.trim().length > 0)
const discussionThreads = computed(() => {
  const rows = messages.value || []
  const roots = []
  const replyMap = new Map()
  const idSet = new Set(rows.map(x => Number(x.id || 0)))
  rows.forEach((m) => {
    const pid = Number(m.parentMessageId || 0)
    if (pid > 0 && idSet.has(pid)) {
      const list = replyMap.get(pid) || []
      list.push(m)
      replyMap.set(pid, list)
      return
    }
    roots.push(m)
  })
  return roots.map((root) => ({
    root,
    replies: (replyMap.get(Number(root.id)) || []).filter(x => isTeacherMessage(x))
  }))
})

function refresh() {
  if (!projectId.value) return
  drillApi.getMessages(projectId.value, 'discussion', false).then((res) => {
    if (res.status) {
      messages.value = (res.data || []).map(x => ({
        id: x.id ?? x.Id,
        userId: x.userId ?? x.UserId,
        content: x.content ?? x.Content,
        userName: x.userName ?? x.UserName,
        roleName: x.roleName ?? x.RoleName,
        parentMessageId: x.parentMessageId ?? x.ParentMessageId ?? null,
        createDate: x.createDate ?? x.CreateDate
      }))
      nextTick(() => {
        const el = listRef.value
        if (el) el.scrollTop = el.scrollHeight
      })
    }
  })
}

function loadMemberRoleMap() {
  if (!projectId.value) return
  drillApi.getMembers(projectId.value, null, false).then((res) => {
    if (!res.status) return
    const map = {}
    const nameMap = {}
    ;(res.data || []).forEach((x) => {
      const roleName = x.roleName ?? x.RoleName
      const userId = x.userId ?? x.UserId
      const userTrueName = x.userTrueName ?? x.UserTrueName
      const userName = x.userName ?? x.UserName
      if (roleName && userId != null) map[`id_${userId}`] = roleName
      if (roleName && userTrueName) map[`name_${userTrueName}`] = roleName
      if (roleName && userName) map[`login_${userName}`] = roleName
      if (userId != null && userTrueName) nameMap[`id_${userId}`] = userTrueName
      if (userName && userTrueName) nameMap[`login_${userName}`] = userTrueName
    })
    memberRoleMap.value = map
    memberNameMap.value = nameMap
  })
}

function resolveRoleName(message) {
  const m = memberRoleMap.value || {}
  const direct = message?.roleName || ''
  if (direct) return direct
  const byId = message?.userId != null ? m[`id_${message.userId}`] : ''
  if (byId) return byId
  const byName = message?.userName ? m[`name_${message.userName}`] : ''
  if (byName) return byName
  const byLogin = message?.userName ? m[`login_${message.userName}`] : ''
  if (byLogin) return byLogin
  return ''
}
function isTeacherMessage(message) {
  if (Number(message?.parentMessageId || 0) > 0) return true
  const role = String(resolveRoleName(message) || '').toLowerCase()
  const name = String(message?.userName || '').toLowerCase()
  return role.includes('教师') || role.includes('老师') || role.includes('teacher') || name.includes('teacher')
}
function resolveDisplayName(message) {
  const name = String(message?.userName || '').trim()
  if (name) return name
  const key = message?.userId != null ? `id_${message.userId}` : ''
  if (key && memberNameMap.value[key]) return memberNameMap.value[key]
  return isTeacherMessage(message) ? '教师' : '匿名'
}

function getRoleText(message) {
  const role = resolveRoleName(message)
  if (role) return role
  return isTeacherMessage(message) ? '教师' : '学员'
}
function truncateDiscussionContent(content, max = 8) {
  const text = String(content || '').trim()
  if (!text) return '-'
  if (text.length <= max) return `${text}`
  return `${text.slice(0, max)}...`
}

function send() {
  const c = text.value.trim()
  if (!c) return
  drillApi.sendMessage(projectId.value, 'discussion', c, true).then((res) => {
    if (res.status) {
      text.value = ''
      refresh()
    } else {
      ElMessage.error(res.message || '发送失败')
    }
  })
}
function onClickRoot(message) {
  if (!message || isTeacherMessage(message)) return
  reviewTarget.value = message
  reviewText.value = ''
  reviewVisible.value = true
}
function closeReviewDialog() {
  reviewVisible.value = false
  reviewTarget.value = null
  reviewText.value = ''
}
function submitReview() {
  const targetId = Number(reviewTarget.value?.id || 0)
  const content = String(reviewText.value || '').trim()
  if (!targetId) return ElMessage.warning('请选择要点评的发言')
  if (!content) return ElMessage.warning('请输入点评内容')
  drillApi.sendMessage(projectId.value, 'discussion', content, true, {
    parentMessageId: targetId
  }).then((res) => {
    if (!res.status) return ElMessage.error(res.message || '点评失败')
    ElMessage.success('点评已提交')
    closeReviewDialog()
    refresh()
  })
}

onMounted(() => {
  if (!projectId.value) {
    ElMessage.warning('请先选择项目')
    router.replace('/teacher/project-select')
    return
  }
  loadMemberRoleMap()
  refresh()
  timer = setInterval(() => {
    if (projectId.value > 0) refresh()
  }, 2000)
})

watch(() => projectId.value, (v) => {
  messages.value = []
  memberRoleMap.value = {}
  memberNameMap.value = {}
  if (!v) return
  loadMemberRoleMap()
  refresh()
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<style scoped>
.page { height: 100%; display: flex; flex-direction: column; gap: 12px; }
.content { flex:1; display:flex; flex-direction: column; background:#fff; border:1px solid #e8e8e8; border-radius:8px; overflow:hidden; }
.messages { flex:1; overflow:auto; padding: 12px; background: #fafafa; }
.thread { margin-bottom: 12px; }
.msg { margin-bottom: 8px; max-width: 86%; }
.msg-student { margin-right: auto; }
.msg-teacher { margin-left: auto; }
.meta { font-size: 12px; color:#666; display:flex; gap:8px; margin-bottom: 6px; }
.meta-teacher { justify-content: flex-end; }
.name { font-weight:600; color:#333; }
.role { color:#1890ff; }
.meta-teacher .role { color:#67c23a; }
.time { margin-left:auto; color:#999; }
.bubble { border-radius:8px; padding:10px 12px; white-space: pre-wrap; }
.bubble-student { background:#fff; border:1px solid #e8e8e8; color:#303133; }
.bubble-teacher { background:#67c23a; border:1px solid #5daf34; color:#111; }
.composer { border-top:1px solid #eee; padding: 12px; }
.actions { margin-top: 8px; display:flex; justify-content: flex-end; gap: 8px; }
.review-reply-title {
  color: #303133;
  font-size: 14px;
  font-weight: 600;
  margin-bottom: 6px;
  text-align: right;
}
.review-reply-card {
  width: 100%;
  background: #67c23a;
  border: 1px solid #5daf34;
  border-radius: 8px;
  color: #111;
  overflow: hidden;
}
.review-origin-mini {
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
.review-target { font-weight: 600; color: #303133; margin-bottom: 8px; }
.review-origin {
  margin-bottom: 12px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 8px 10px;
  background: #f8f9fb;
  color: #606266;
  white-space: pre-wrap;
}
</style>

