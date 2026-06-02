<template>
  <div class="project-edit">
    <div class="block title">项目信息</div>
    <div class="sub-menu">
      <div
        v-for="item in sectionMenus"
        :key="item.key"
        class="sub-menu-item"
        :class="{ active: activeSection === item.key }"
        @click="activeSection = item.key"
      >
        {{ item.label }}
      </div>
    </div>
    <el-form ref="formRef" :model="form" :rules="rules" label-width="100px" class="edit-form">
      <el-card class="edit-card" shadow="never" v-show="activeSection === 'basic'">
        <template #header>基础信息</template>
      <el-form-item label="项目名称" prop="name">
        <el-input v-model="form.name" placeholder="请输入项目名称" clearable />
      </el-form-item>
      <el-form-item label="项目编号" prop="code">
        <el-input v-model="form.code" placeholder="请输入项目编号" clearable />
      </el-form-item>
      <el-form-item label="状态" prop="status">
        <el-select v-model="form.status" placeholder="请选择" style="width:100%">
          <el-option label="未开始" :value="0" />
          <el-option label="进行中" :value="1" />
          <el-option label="已结束" :value="2" />
        </el-select>
      </el-form-item>
      <el-form-item label="备注" prop="remark">
        <el-input v-model="form.remark" type="textarea" :rows="3" placeholder="请输入备注" />
      </el-form-item>
      </el-card>

      <el-card class="edit-card" shadow="never" v-show="activeSection === 'resource'">
        <template #header>项目资源</template>
      <el-form-item label="视频资源">
        <div class="resource-wrap">
          <div class="resource-toolbar">
            <el-button size="small" type="primary" plain @click="addResource">新增视频</el-button>
          </div>
          <div v-for="(res, ridx) in resourceList" :key="res.__key" class="resource-item">
            <div class="resource-item-head">
              <span>视频{{ ridx + 1 }}</span>
              <el-button link type="danger" @click="removeResource(ridx)" :disabled="resourceList.length<=1">删除</el-button>
            </div>
            <el-input v-model="res.resourceName" placeholder="例如：事故场景主视频" clearable />
            <div class="video-upload-wrap">
              <el-upload
                :show-file-list="false"
                accept="video/mp4,video/webm,video/ogg,video/quicktime"
                :http-request="(opt) => uploadResourceVideo(opt, res)"
              >
                <el-button type="primary" plain>上传视频</el-button>
              </el-upload>
              <el-input v-model="res.videoUrl" placeholder="或直接粘贴视频URL" clearable />
            </div>
          </div>
        </div>
      </el-form-item>
      </el-card>

      <el-card class="edit-card" shadow="never" v-show="activeSection === 'node'">
        <template #header>节点配置</template>
        <el-form-item label="节点配置">
        <div class="node-config-wrap">
          <div v-if="nodeConfigs.length && currentEditorVideoUrl" class="node-quick-editor">
            <div class="quick-editor-line">
              <el-select v-model="selectedNodeKey" class="quick-node-select" placeholder="选择节点">
                <el-option v-for="n in nodeConfigs" :key="n.__key" :label="n.nodeName || n.nodeCode || '未命名节点'" :value="n.__key" />
              </el-select>
              <el-switch
                v-model="endToVideoEnd"
                inline-prompt
                active-text="到视频末尾"
                inactive-text="自定义结束"
                @change="onEndToVideoEndChange"
              />
              <el-button size="small" @click="setStartByCurrent">当前时间设开始</el-button>
              <el-button size="small" @click="setEndByCurrent" :disabled="endToVideoEnd">当前时间设结束</el-button>
              <el-button size="small" @click="jumpVideoTo(selectedRange[0])">预览起点</el-button>
            </div>
            <div class="quick-editor-slider">
              <el-slider
                v-model="selectedRange"
                range
                :min="0"
                :max="Math.max(1, videoDuration)"
                :step="1"
                @change="onRangeChange"
              />
            </div>
          </div>
          <div v-if="currentEditorVideoUrl" class="video-editor-wrap">
            <video
              ref="editorVideoRef"
              class="editor-video"
              :src="currentEditorVideoUrl"
              controls
              preload="metadata"
              @loadedmetadata="onEditorVideoLoaded"
              @timeupdate="onEditorTimeUpdate"
            />
            <div class="video-meta">
              <span>当前播放：{{ editorCurrentSeconds }}s</span>
              <span>总时长：{{ videoDuration }}s</span>
              <span>编辑视频：{{ currentEditorResource?.resourceName || '未命名视频' }}</span>
            </div>
          </div>
          <div class="node-toolbar">
            <el-button size="small" type="primary" plain @click="addNode">新增节点</el-button>
          </div>
          <el-table :data="nodeConfigs" border size="small" style="width:100%">
            <el-table-column label="节点名称" min-width="220">
              <template #default="{ row }">
                <el-input v-model="row.nodeName" placeholder="请输入节点名称" />
              </template>
            </el-table-column>
            <el-table-column label="节点描述" min-width="260">
              <template #default="{ row }">
                <el-input v-model="row.description" type="textarea" :rows="2" placeholder="请输入节点描述（用于复盘展示）" />
              </template>
            </el-table-column>
            <el-table-column label="节点编码" width="180">
              <template #default="{ row }">
                <el-input v-model="row.nodeCode" placeholder="node_xxx" />
              </template>
            </el-table-column>
            <el-table-column label="所属视频" width="220">
              <template #default="{ row }">
                <el-select v-model="row.resourceKey" placeholder="请选择视频" style="width:100%" @change="onNodeResourceChange(row)">
                  <el-option
                    v-for="res in resourceList"
                    :key="res.__key"
                    :label="res.resourceName || '未命名视频'"
                    :value="res.__key"
                  />
                </el-select>
              </template>
            </el-table-column>
            <el-table-column label="激活身份组" min-width="220">
              <template #default="{ row }">
                <div class="role-task-cell" @click="openRoleTaskDialog(row)">
                  <template v-if="row.allowedRoles?.length">
                    <el-tag
                      v-for="r in row.allowedRoles.slice(0, 2)"
                      :key="`${row.__key}_${r}`"
                      size="small"
                      class="role-chip"
                    >
                      {{ r }}
                    </el-tag>
                    <el-tag v-if="row.allowedRoles.length > 2" size="small" type="info">
                      +{{ row.allowedRoles.length - 2 }}
                    </el-tag>
                  </template>
                  <span v-else class="role-task-cell-empty">点击配置身份组与任务</span>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="开始秒" width="160">
              <template #default="{ row }">
                <el-input-number v-model="row.videoStartSeconds" :min="0" :step="1" controls-position="right" />
              </template>
            </el-table-column>
            <el-table-column label="结束秒" width="160">
              <template #default="{ row }">
                <el-input-number v-model="row.videoEndSeconds" :min="0" :step="1" controls-position="right" />
              </template>
            </el-table-column>
            <el-table-column label="操作" width="210" fixed="right">
              <template #default="{ row, $index }">
                <el-button link type="primary" @click="moveNodeUp($index)" :disabled="$index===0">上移</el-button>
                <el-button link type="primary" @click="moveNodeDown($index)" :disabled="$index===nodeConfigs.length-1">下移</el-button>
                <el-button link type="danger" @click="removeNode($index)" :disabled="nodeConfigs.length<=1">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </div>
      </el-form-item>
      </el-card>

      <el-card class="edit-card" shadow="never" v-show="activeSection === 'member'">
        <template #header>项目成员管理</template>
        <el-form-item label="项目成员">
        <div class="member-wrap">
          <div class="member-toolbar">
            <el-button size="small" type="primary" plain @click="openMemberDialog">新增成员</el-button>
            <el-button size="small" @click="triggerImportMembers">导入账号姓名</el-button>
            <el-button size="small" @click="exportMembersSimple">导出账号姓名</el-button>
          </div>
          <el-table :data="memberRows" border size="small" style="width:100%">
            <el-table-column type="index" label="#" width="60" />
            <el-table-column prop="userName" label="账号" min-width="180" />
            <el-table-column prop="userTrueName" label="姓名" min-width="180" />
          </el-table>
        </div>
      </el-form-item>
      </el-card>
      <el-form-item>
        <el-button type="primary" @click="onSave">保存</el-button>
        <el-button @click="onCancel">取消</el-button>
      </el-form-item>
    </el-form>
    <input ref="memberImportInputRef" type="file" accept=".txt,.csv,text/plain,text/csv" style="display:none" @change="onMemberImportFileChange" />
    <el-dialog v-model="memberDialogVisible" title="新增成员" width="420px" @close="resetMemberForm">
      <el-form :model="memberForm" :rules="memberRules" ref="memberFormRef" label-width="70px">
        <el-form-item label="账号" prop="userName">
          <el-input v-model="memberForm.userName" placeholder="请输入账号" />
        </el-form-item>
        <el-form-item label="姓名" prop="userTrueName">
          <el-input v-model="memberForm.userTrueName" placeholder="请输入姓名" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="memberDialogVisible=false">取消</el-button>
        <el-button type="primary" @click="confirmAddMember">确定</el-button>
      </template>
    </el-dialog>
    <el-dialog
      v-model="roleTaskDialogVisible"
      title="配置节点激活身份组与任务"
      width="860px"
      @close="resetRoleTaskDialog"
    >
      <div class="role-task-dialog">
        <div class="role-task-dialog-left">
          <div class="dialog-panel-title">激活身份组</div>
          <div class="task-dialog-actions">
            <el-button size="small" @click="selectAllDialogRoles">全选</el-button>
            <el-button size="small" @click="clearDialogRoles">清空</el-button>
          </div>
          <el-checkbox-group v-model="dialogAllowedRoles" class="role-check-list">
            <div
              v-for="roleName in drillRoleOptions"
              :key="roleName"
              class="role-check-item"
              :class="{ active: dialogActiveRole === roleName }"
              @click="onDialogRoleClick(roleName)"
            >
              <el-checkbox :label="roleName" @change="() => onDialogRoleChecked(roleName)">
                {{ roleName }}
              </el-checkbox>
            </div>
          </el-checkbox-group>
        </div>
        <div class="role-task-dialog-right">
          <div class="dialog-panel-title">
            {{ dialogActiveRole ? `${dialogActiveRole} - 激活任务` : '请选择左侧身份组' }}
          </div>
          <div v-if="dialogActiveRole" class="task-dialog-actions">
            <el-button size="small" @click="selectAllDialogTasks">全选</el-button>
            <el-button size="small" @click="clearDialogTasks">清空</el-button>
          </div>
          <el-checkbox-group
            v-if="dialogActiveRole"
            v-model="dialogRoleTaskSelections[dialogActiveRole]"
            class="task-check-list"
          >
            <div
              v-for="opt in getRoleTaskOptions(dialogActiveRole)"
              :key="`${dialogActiveRole}_${opt.id}`"
              class="task-check-item"
            >
              <el-checkbox :label="opt.id">
                <span class="task-check-title">{{ opt.label }}</span>
              </el-checkbox>
              <div v-if="opt.items?.length" class="task-check-detail">
                <div v-for="(txt, idx) in opt.items" :key="`${opt.id}_${idx}`" class="task-check-detail-item">
                  {{ idx + 1 }}. {{ txt }}
                </div>
              </div>
            </div>
          </el-checkbox-group>
          <el-empty v-else description="先勾选并选中左侧身份组" />
          <el-empty
            v-if="dialogActiveRole && !getRoleTaskOptions(dialogActiveRole).length"
            description="该身份组未配置任务"
          />
        </div>
      </div>
      <template #footer>
        <el-button @click="roleTaskDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmRoleTaskDialog">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as api from '@/api/project'
import * as drillApi from '@/api/drill'
import axios from 'axios'
import store from '@/store/index'

const router = useRouter()
const route = useRoute()
const formRef = ref(null)
const id = computed(() => route.params.id)
const sectionMenus = [
  { key: 'basic', label: '基础信息' },
  { key: 'resource', label: '项目资源' },
  { key: 'node', label: '节点配置' },
  { key: 'member', label: '项目成员管理' }
]
const activeSection = ref('basic')

const form = reactive({
  name: '',
  code: '',
  status: 0,
  remark: ''
})
const resourceList = ref([])
const nodeConfigs = ref([])
const editorVideoRef = ref(null)
const videoDuration = ref(0)
const editorCurrentSeconds = ref(0)
const selectedNodeKey = ref('')
const selectedRange = ref([0, 0])
const endToVideoEnd = ref(false)
const syncingRange = ref(false)
const drillRoleOptions = ref([])
const drillRoleTaskGroupsMap = ref({})
const projectSettings = ref({})
const memberRows = ref([])
const memberImportInputRef = ref(null)
const memberDialogVisible = ref(false)
const memberFormRef = ref(null)
const memberForm = ref({ userName: '', userTrueName: '' })
const roleTaskDialogVisible = ref(false)
const roleTaskDialogNodeKey = ref('')
const dialogAllowedRoles = ref([])
const dialogRoleTaskSelections = ref({})
const dialogActiveRole = ref('')
const memberRules = {
  userName: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  userTrueName: [{ required: true, message: '请输入姓名', trigger: 'blur' }]
}

const rules = {
  name: [{ required: true, message: '请输入项目名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入项目编号', trigger: 'blur' }]
}

function load() {
  if (!id.value) {
    initNewProject()
    loadDrillRoles()
    return
  }
  api.getProjectById(id.value).then((res) => {
    if (res.status && res.data && res.data.id) {
      form.name = res.data.name || ''
      form.code = res.data.code || ''
      form.status = res.data.status ?? 0
      form.remark = res.data.remark || ''
    }
  })
  loadDrillRoles()
  loadProjectSettings(Number(id.value)).then(() => loadResourceAndNodes(Number(id.value)))
  loadMembers(Number(id.value))
}

function makeNode(node = {}, idx = 1) {
  return {
    __key: `${Date.now()}_${Math.random().toString(16).slice(2)}`,
    resourceId: node.resourceId ?? node.ResourceId ?? null,
    resourceKey: '',
    nodeCode: node.nodeCode || node.NodeCode || `node_${String(idx).padStart(2, '0')}`,
    nodeName: node.nodeName || node.NodeName || `节点${idx}`,
    description: node.description || node.Description || '',
    allowedRoles: Array.isArray(node.allowedRoles) ? node.allowedRoles : [],
    roleTaskSelections: node.roleTaskSelections && typeof node.roleTaskSelections === 'object' ? { ...node.roleTaskSelections } : {},
    videoStartSeconds: node.videoStartSeconds ?? node.VideoStartSeconds ?? 0,
    videoEndSeconds: node.videoEndSeconds ?? node.VideoEndSeconds ?? null
  }
}

function makeResource(row = {}, idx = 1) {
  return {
    __key: `${Date.now()}_${Math.random().toString(16).slice(2)}`,
    id: row.id ?? row.Id ?? null,
    resourceName: row.resourceName ?? row.ResourceName ?? `项目视频${idx}`,
    videoUrl: row.videoUrl ?? row.VideoUrl ?? '',
    enable: row.enable ?? row.Enable ?? 1
  }
}

function initNewProject() {
  resourceList.value = [makeResource({}, 1)]
  const node = makeNode({}, 1)
  node.resourceKey = resourceList.value[0].__key
  nodeConfigs.value = [node]
  selectedNodeKey.value = nodeConfigs.value[0].__key
  memberRows.value = []
}

function loadResourceAndNodes(projectId) {
  if (!projectId) return
  Promise.all([
    drillApi.getProjectResources(projectId, false),
    drillApi.getFlowNodes(projectId, false)
  ]).then(([resourceRes, nodeRes]) => {
    const resources = (resourceRes?.status ? (resourceRes.data || []) : []).map((x, idx) => makeResource(x, idx + 1))
    resourceList.value = resources.length ? resources : [makeResource({}, 1)]
    const firstResourceKey = resourceList.value[0]?.__key || ''
    const resourceMapById = new Map(resourceList.value.map(x => [Number(x.id || 0), x.__key]))

    const list = (nodeRes?.status ? (nodeRes.data || []) : []).map((x, idx) => {
      const row = makeNode(x, idx + 1)
      const rid = Number(row.resourceId || 0)
      row.resourceKey = (rid > 0 && resourceMapById.has(rid)) ? resourceMapById.get(rid) : firstResourceKey
      const map = projectSettings.value?.nodeRolePermissions || {}
      const taskMap = projectSettings.value?.nodeRoleTaskSelections || {}
      row.allowedRoles = Array.isArray(map[row.nodeCode]) ? [...map[row.nodeCode]] : []
      row.roleTaskSelections = taskMap[row.nodeCode] && typeof taskMap[row.nodeCode] === 'object'
        ? JSON.parse(JSON.stringify(taskMap[row.nodeCode]))
        : {}
      return row
    })
    nodeConfigs.value = list.length ? list : [makeNode({}, 1)]
    if (!list.length && firstResourceKey) nodeConfigs.value[0].resourceKey = firstResourceKey
    const selectedExists = nodeConfigs.value.some(x => x.__key === selectedNodeKey.value)
    if ((!selectedNodeKey.value || !selectedExists) && nodeConfigs.value.length) {
      selectedNodeKey.value = nodeConfigs.value[0].__key
    }
    syncSelectedRange()
  })
}

function loadDrillRoles() {
  drillApi.getDrillRoles(false).then((res) => {
    if (!res.status) return
    const rows = (res.data || []).filter((x) => {
      const enable = x.enable != null ? x.enable : (x.Enable != null ? x.Enable : 1)
      return enable === 1
    })
    drillRoleOptions.value = rows
      .map((x) => {
        const roleName = x.roleName != null ? x.roleName : (x.RoleName != null ? x.RoleName : '')
        return String(roleName).trim()
      })
      .filter(Boolean)
    const map = {}
    rows.forEach((x) => {
      const roleName = String(x.roleName ?? x.RoleName ?? '').trim()
      if (!roleName) return
      map[roleName] = parseRoleTaskGroups(x.taskBookJson ?? x.TaskBookJson ?? '')
    })
    drillRoleTaskGroupsMap.value = map
  })
}

function parseRoleTaskGroups(taskBookJson) {
  if (!taskBookJson) return []
  try {
    const obj = typeof taskBookJson === 'string' ? JSON.parse(taskBookJson) : taskBookJson
    const tasks = Array.isArray(obj?.tasks) ? obj.tasks : []
    return tasks.map((t, idx) => {
      const id = String(t?.id || `task_${idx + 1}`)
      const title = String(t?.title || `角色任务${idx + 1}`).trim() || `角色任务${idx + 1}`
      const items = Array.isArray(t?.items) ? t.items : []
      return {
        id,
        title,
        itemCount: items.filter(it => String(it?.text || '').trim()).length,
        items: items.map(it => String(it?.text || '').trim()).filter(Boolean)
      }
    })
  } catch (e) {
    return []
  }
}

function getRoleTaskOptions(roleName) {
  const list = drillRoleTaskGroupsMap.value?.[String(roleName || '').trim()] || []
  return list.map((x, idx) => ({
    id: x.id,
    label: `${x.title || `角色任务${idx + 1}`}${x.itemCount > 0 ? `（${x.itemCount}条）` : ''}`,
    items: Array.isArray(x.items) ? x.items : []
  }))
}

function openRoleTaskDialog(row) {
  if (!row?.__key) return
  roleTaskDialogNodeKey.value = row.__key
  dialogAllowedRoles.value = Array.isArray(row.allowedRoles) ? [...row.allowedRoles] : []
  dialogRoleTaskSelections.value = row.roleTaskSelections && typeof row.roleTaskSelections === 'object'
    ? JSON.parse(JSON.stringify(row.roleTaskSelections))
    : {}
  dialogActiveRole.value = dialogAllowedRoles.value[0] || ''
  roleTaskDialogVisible.value = true
}

function onDialogRoleClick(roleName) {
  dialogActiveRole.value = roleName
  if (!Array.isArray(dialogRoleTaskSelections.value?.[roleName])) {
    dialogRoleTaskSelections.value[roleName] = []
  }
}

function onDialogRoleChecked(roleName) {
  if (dialogAllowedRoles.value.includes(roleName)) {
    if (!dialogActiveRole.value) dialogActiveRole.value = roleName
    if (!Array.isArray(dialogRoleTaskSelections.value?.[roleName])) {
      dialogRoleTaskSelections.value[roleName] = []
    }
    return
  }
  const next = { ...(dialogRoleTaskSelections.value || {}) }
  delete next[roleName]
  dialogRoleTaskSelections.value = next
  if (dialogActiveRole.value === roleName) {
    dialogActiveRole.value = dialogAllowedRoles.value[0] || ''
  }
}

function selectAllDialogRoles() {
  dialogAllowedRoles.value = [...drillRoleOptions.value]
  const next = { ...(dialogRoleTaskSelections.value || {}) }
  dialogAllowedRoles.value.forEach((roleName) => {
    if (!Array.isArray(next[roleName])) next[roleName] = []
  })
  dialogRoleTaskSelections.value = next
  if (!dialogActiveRole.value) dialogActiveRole.value = dialogAllowedRoles.value[0] || ''
}

function clearDialogRoles() {
  dialogAllowedRoles.value = []
  dialogRoleTaskSelections.value = {}
  dialogActiveRole.value = ''
}

function selectAllDialogTasks() {
  const roleName = String(dialogActiveRole.value || '').trim()
  if (!roleName) return
  dialogRoleTaskSelections.value[roleName] = getRoleTaskOptions(roleName).map(x => x.id)
}

function clearDialogTasks() {
  const roleName = String(dialogActiveRole.value || '').trim()
  if (!roleName) return
  dialogRoleTaskSelections.value[roleName] = []
}

function confirmRoleTaskDialog() {
  const row = (nodeConfigs.value || []).find(x => x.__key === roleTaskDialogNodeKey.value)
  if (!row) {
    roleTaskDialogVisible.value = false
    return
  }
  row.allowedRoles = [...dialogAllowedRoles.value]
  const next = {}
  row.allowedRoles.forEach((roleName) => {
    const selected = Array.isArray(dialogRoleTaskSelections.value?.[roleName]) ? dialogRoleTaskSelections.value[roleName] : []
    next[roleName] = selected.map(x => String(x || '').trim()).filter(Boolean)
  })
  row.roleTaskSelections = next
  roleTaskDialogVisible.value = false
}

function resetRoleTaskDialog() {
  roleTaskDialogNodeKey.value = ''
  dialogAllowedRoles.value = []
  dialogRoleTaskSelections.value = {}
  dialogActiveRole.value = ''
}

function loadProjectSettings(projectId) {
  if (!projectId) {
    projectSettings.value = {}
    return Promise.resolve()
  }
  return drillApi.getState(projectId, false).then((res) => {
    const data = res?.status ? (res.data || {}) : {}
    let settings = {}
    try {
      settings = data.settingsJson ? JSON.parse(data.settingsJson) : (data.SettingsJson ? JSON.parse(data.SettingsJson) : {})
    } catch (e) {
      settings = {}
    }
    projectSettings.value = settings || {}
  }).catch(() => {
    projectSettings.value = {}
  })
}

function loadMembers(projectId) {
  if (!projectId) {
    memberRows.value = []
    return
  }
  drillApi.exportSimpleMembers(projectId, false).then((res) => {
    if (!res.status) {
      memberRows.value = []
      return
    }
    memberRows.value = (res.data || []).map((x) => ({
      userName: (x.userName ?? x.UserName ?? '').trim(),
      userTrueName: (x.userTrueName ?? x.UserTrueName ?? '').trim()
    })).filter(x => x.userName && x.userTrueName)
      .sort((a, b) => String(a.userName).localeCompare(String(b.userName), 'zh-Hans-CN'))
  })
}

function uploadResourceVideo(options, targetResource) {
  const token = store.getters.getToken()
  if (!token) {
    ElMessage.warning('请先登录')
    options.onError(new Error('not login'))
    return
  }
  const formData = new FormData()
  formData.append('file', options.file)
  axios.post('/api/Drill/ProjectResources/UploadVideo', formData, {
    headers: { Authorization: token }
  }).then((res) => {
    const data = res.data || {}
    if (data.status) {
      targetResource.videoUrl = data.data || ''
      if (!targetResource.resourceName) targetResource.resourceName = '项目视频资源'
      ElMessage.success(data.message || '视频上传成功')
      options.onSuccess(data)
    } else {
      ElMessage.error(data.message || '视频上传失败')
      options.onError(new Error(data.message || 'upload fail'))
    }
  }).catch((e) => {
    ElMessage.error(e?.message || '视频上传失败')
    options.onError(e)
  })
}

function parseSavedProjectId(raw, fallbackId) {
  if (fallbackId) return Number(fallbackId)
  const d = raw?.data ?? raw?.Data ?? raw
  if (typeof d === 'number') return Number(d)
  if (typeof d === 'string' && /^\d+$/.test(d)) return Number(d)
  const entity = d?.data ?? d
  const rid = entity?.Id ?? entity?.id
  return rid ? Number(rid) : 0
}

function getSelectedNode() {
  const key = selectedNodeKey.value
  if (!key) return null
  return (nodeConfigs.value || []).find(x => x.__key === key) || null
}

const currentEditorResource = computed(() => {
  const node = getSelectedNode()
  if (node?.resourceKey) {
    const row = (resourceList.value || []).find(x => x.__key === node.resourceKey)
    if (row && row.videoUrl) return row
  }
  return (resourceList.value || []).find(x => x.videoUrl) || resourceList.value[0] || null
})

const currentEditorVideoUrl = computed(() => currentEditorResource.value?.videoUrl || '')

function syncSelectedRange() {
  const row = getSelectedNode()
  if (!row) return
  const max = Math.max(0, Math.floor(videoDuration.value || 0))
  let start = Math.max(0, Number(row.videoStartSeconds || 0))
  let end = row.videoEndSeconds == null ? max : Math.max(0, Number(row.videoEndSeconds || 0))
  if (end < start) end = start
  syncingRange.value = true
  selectedRange.value = [start, end]
  endToVideoEnd.value = row.videoEndSeconds == null
  syncingRange.value = false
}

function onRangeChange(val) {
  if (syncingRange.value) return
  const row = getSelectedNode()
  if (!row) return
  const start = Math.max(0, Number(val?.[0] || 0))
  const end = Math.max(start, Number(val?.[1] || 0))
  row.videoStartSeconds = start
  row.videoEndSeconds = endToVideoEnd.value ? null : end
}

function onEndToVideoEndChange(v) {
  const row = getSelectedNode()
  if (!row) return
  if (v) row.videoEndSeconds = null
  else row.videoEndSeconds = Math.max(Number(selectedRange.value?.[1] || 0), Number(row.videoStartSeconds || 0))
  syncSelectedRange()
}

function onEditorVideoLoaded(e) {
  videoDuration.value = Math.floor(e?.target?.duration || 0)
  syncSelectedRange()
}

function onEditorTimeUpdate(e) {
  editorCurrentSeconds.value = Math.floor(e?.target?.currentTime || 0)
}

function setStartByCurrent() {
  const row = getSelectedNode()
  if (!row) return
  row.videoStartSeconds = editorCurrentSeconds.value
  if (row.videoEndSeconds != null && row.videoEndSeconds < row.videoStartSeconds) {
    row.videoEndSeconds = row.videoStartSeconds
  }
  syncSelectedRange()
}

function setEndByCurrent() {
  const row = getSelectedNode()
  if (!row) return
  endToVideoEnd.value = false
  row.videoEndSeconds = Math.max(editorCurrentSeconds.value, Number(row.videoStartSeconds || 0))
  syncSelectedRange()
}

function jumpVideoTo(sec) {
  const video = editorVideoRef.value
  if (!video) return
  video.currentTime = Math.max(0, Number(sec || 0))
}

function addNode() {
  const idx = (nodeConfigs.value || []).length + 1
  const row = makeNode({}, idx)
  row.resourceKey = resourceList.value[0]?.__key || ''
  nodeConfigs.value.push(row)
  selectedNodeKey.value = row.__key
}

function removeNode(index) {
  if (nodeConfigs.value.length <= 1) return
  const removed = nodeConfigs.value.splice(index, 1)
  if (removed[0]?.__key === selectedNodeKey.value) {
    selectedNodeKey.value = nodeConfigs.value[Math.max(0, index - 1)]?.__key || nodeConfigs.value[0]?.__key || ''
  }
}

function moveNodeUp(index) {
  if (index <= 0) return
  const arr = nodeConfigs.value
  const tmp = arr[index - 1]
  arr[index - 1] = arr[index]
  arr[index] = tmp
}

function moveNodeDown(index) {
  const arr = nodeConfigs.value
  if (index >= arr.length - 1) return
  const tmp = arr[index + 1]
  arr[index + 1] = arr[index]
  arr[index] = tmp
}

function addResource() {
  resourceList.value.push(makeResource({}, resourceList.value.length + 1))
}

function removeResource(index) {
  if (resourceList.value.length <= 1) return
  const removed = resourceList.value.splice(index, 1)[0]
  const fallbackKey = resourceList.value[0]?.__key || ''
  nodeConfigs.value.forEach((node) => {
    if (node.resourceKey === removed?.__key) node.resourceKey = fallbackKey
  })
  syncSelectedRange()
}

function onNodeResourceChange(row) {
  const selected = getSelectedNode()
  if (selected?.__key === row?.__key) {
    videoDuration.value = 0
    editorCurrentSeconds.value = 0
    syncSelectedRange()
  }
}

function buildResourceSaveItems() {
  return (resourceList.value || [])
    .map((x, idx) => ({
      clientKey: x.__key,
      resourceName: String(x.resourceName || '').trim() || `项目视频${idx + 1}`,
      videoUrl: String(x.videoUrl || '').trim(),
      enable: x.enable === 0 ? 0 : 1,
      orderNo: idx + 1
    }))
    .filter(x => x.videoUrl)
}

function buildNodeSaveItems() {
  const raw = (nodeConfigs.value || [])
    .map((x, idx) => {
      const name = String(x.nodeName || '').trim()
      const code = String(x.nodeCode || '').trim()
      const start = Math.max(0, Number(x.videoStartSeconds || 0))
      let end = x.videoEndSeconds == null ? null : Math.max(0, Number(x.videoEndSeconds || 0))
      if (end != null && end < start) end = start
      return {
        nodeCode: code || `node_${String(idx + 1).padStart(2, '0')}`,
        nodeName: name || `节点${idx + 1}`,
        description: String(x.description || '').trim(),
        stage: 'scene',
        resourceKey: x.resourceKey || '',
        allowedRoles: Array.isArray(x.allowedRoles) ? [...x.allowedRoles] : [],
        roleTaskSelections: x.roleTaskSelections && typeof x.roleTaskSelections === 'object'
          ? JSON.parse(JSON.stringify(x.roleTaskSelections))
          : {},
        videoStartSeconds: start,
        videoEndSeconds: end
      }
    })
  const used = new Set()
  return raw.map((x, idx) => {
    let code = x.nodeCode
    while (used.has(code)) code = `${x.nodeCode}_${idx + 1}`
    used.add(code)
    return { ...x, nodeCode: code }
  })
}

function resetMemberForm() {
  memberForm.value = { userName: '', userTrueName: '' }
  memberFormRef.value?.resetFields()
}

function openMemberDialog() {
  memberDialogVisible.value = true
}

function confirmAddMember() {
  memberFormRef.value?.validate((valid) => {
    if (!valid) return
    const userName = String(memberForm.value.userName || '').trim()
    const userTrueName = String(memberForm.value.userTrueName || '').trim()
    if (!userName || !userTrueName) return
    const exists = memberRows.value.some(x => String(x.userName).toLowerCase() === userName.toLowerCase())
    if (exists) return ElMessage.warning('该账号已存在')
    memberRows.value.push({ userName, userTrueName })
    memberRows.value.sort((a, b) => String(a.userName).localeCompare(String(b.userName), 'zh-Hans-CN'))
    memberDialogVisible.value = false
  })
}

function triggerImportMembers() {
  memberImportInputRef.value && (memberImportInputRef.value.value = '')
  memberImportInputRef.value?.click()
}

function parseMemberText(text) {
  const lines = String(text || '').split(/\r?\n/).map(x => x.trim()).filter(Boolean)
  const list = []
  lines.forEach((line) => {
    if (line.startsWith('#')) return
    const sep = line.includes(',') ? ',' : (line.includes('，') ? '，' : (line.includes('\t') ? '\t' : ' '))
    const arr = line.split(sep).map(x => x.trim()).filter(Boolean)
    if (arr.length < 2) return
    list.push({ userName: arr[0], userTrueName: arr[1] })
  })
  const map = new Map()
  list.forEach((x) => {
    if (!x.userName || !x.userTrueName) return
    map.set(String(x.userName).toLowerCase(), { userName: x.userName, userTrueName: x.userTrueName })
  })
  return Array.from(map.values())
}

async function onMemberImportFileChange(e) {
  const file = e?.target?.files?.[0]
  if (!file) return
  const text = await file.text()
  const imported = parseMemberText(text)
  if (!imported.length) return ElMessage.warning('未识别到有效数据，请按“账号,姓名”格式导入')
  const map = new Map(memberRows.value.map(x => [String(x.userName).toLowerCase(), { ...x }]))
  imported.forEach((x) => {
    map.set(String(x.userName).toLowerCase(), { userName: x.userName, userTrueName: x.userTrueName })
  })
  memberRows.value = Array.from(map.values()).sort((a, b) => String(a.userName).localeCompare(String(b.userName), 'zh-Hans-CN'))
  ElMessage.success(`已导入 ${imported.length} 条`)
}

function exportMembersSimple() {
  if (!memberRows.value.length) return ElMessage.warning('暂无成员可导出')
  const lines = ['账号,姓名', ...memberRows.value.map(x => `${x.userName},${x.userTrueName}`)]
  const blob = new Blob([lines.join('\n')], { type: 'text/csv;charset=utf-8;' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `${form.name || '项目'}_成员账号姓名.csv`
  document.body.appendChild(a)
  a.click()
  URL.revokeObjectURL(a.href)
  document.body.removeChild(a)
}

function syncMembers(projectId) {
  const items = (memberRows.value || [])
    .map(x => ({
      userName: String(x.userName || '').trim(),
      userTrueName: String(x.userTrueName || '').trim()
    }))
    .filter(x => x.userName && x.userTrueName)
  if (!items.length) return Promise.resolve()
  return drillApi.importSimpleMembers(projectId, items, true).then((res) => {
    if (!res.status) throw new Error(res.message || '成员同步失败')
  })
}

function syncNodeRolePermissions(projectId) {
  const nodeRolePermissions = {}
  const nodeRoleTaskSelections = {}
  ;(nodeConfigs.value || []).forEach((n) => {
    const code = String(n.nodeCode || '').trim()
    if (!code) return
    const roles = Array.isArray(n.allowedRoles) ? n.allowedRoles.map(x => String(x || '').trim()).filter(Boolean) : []
    nodeRolePermissions[code] = roles
    const roleTaskSelections = {}
    roles.forEach((roleName) => {
      const selected = Array.isArray(n.roleTaskSelections?.[roleName]) ? n.roleTaskSelections[roleName] : []
      roleTaskSelections[roleName] = selected.map(x => String(x || '').trim()).filter(Boolean)
    })
    nodeRoleTaskSelections[code] = roleTaskSelections
  })
  const nextSettings = { ...(projectSettings.value || {}), nodeRolePermissions, nodeRoleTaskSelections }
  return drillApi.saveSettings(projectId, nextSettings, true).then((res) => {
    if (!res.status) throw new Error(res.message || '节点身份组权限保存失败')
    projectSettings.value = nextSettings
  })
}

function onSave() {
  formRef.value?.validate((valid) => {
    if (!valid) return
    const data = {
      id: id.value ? parseInt(id.value, 10) : undefined,
      name: form.name,
      code: form.code,
      status: form.status,
      remark: form.remark
    }
    api.saveProject(data, true).then((res) => {
      if (!res.status) return ElMessage.error(res.message || '保存失败')
      const savedId = parseSavedProjectId(res, id.value ? Number(id.value) : 0)
      if (!savedId) {
        ElMessage.success(res.message || '保存成功')
        return router.replace('/admin/project')
      }

      const resourceIdMap = new Map()
      const resourceItems = buildResourceSaveItems()
      const saveResourcePromise = resourceItems.length
        ? drillApi.saveProjectResources(savedId, resourceItems, true).then((saveResourceRes) => {
          if (!saveResourceRes.status) throw new Error(saveResourceRes.message || '项目资源保存失败')
          ;(saveResourceRes.data || []).forEach((x) => {
            const clientKey = x.clientKey ?? x.ClientKey
            const rid = Number(x.id ?? x.Id ?? 0)
            if (clientKey && rid > 0) resourceIdMap.set(clientKey, rid)
          })
        })
        : Promise.resolve()

      saveResourcePromise.then(() => {
        const nodeItems = buildNodeSaveItems()
          .map((x) => {
            const resourceId = x.resourceKey ? (resourceIdMap.get(x.resourceKey) || null) : null
            return {
              nodeCode: x.nodeCode,
              nodeName: x.nodeName,
              description: x.description,
              stage: x.stage,
              resourceId,
              videoStartSeconds: x.videoStartSeconds,
              videoEndSeconds: x.videoEndSeconds
            }
          })
        if (!nodeItems.length) return
        return drillApi.saveFlowNodes(savedId, nodeItems, true).then((saveNodeRes) => {
          if (!saveNodeRes.status) throw new Error(saveNodeRes.message || '节点配置保存失败')
        })
      }).then(() => {
        return syncMembers(savedId)
      }).then(() => {
        return syncNodeRolePermissions(savedId)
      }).then(() => {
        ElMessage.success(res.message || '保存成功')
        router.replace('/admin/project')
      }).catch((e) => {
        ElMessage.warning(e?.message || '项目已保存，但资源/节点配置未完全保存')
        router.replace('/admin/project')
      })
    }).catch((msg) => ElMessage.error(msg || '保存失败'))
  })
}

function onCancel() {
  router.replace('/admin/project')
}

onMounted(load)

watch(selectedNodeKey, () => syncSelectedRange())
watch(videoDuration, () => syncSelectedRange())
watch(currentEditorVideoUrl, () => {
  videoDuration.value = 0
  editorCurrentSeconds.value = 0
  syncSelectedRange()
})
</script>

<style scoped>
.project-edit {
  width: 100%;
  background: #fff;
  padding: 24px;
  border-radius: 8px;
  box-sizing: border-box;
}
.project-edit .block.title {
  font-size: 16px;
  font-weight: 600;
  margin-bottom: 20px;
  color: #303133;
}
.sub-menu {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-bottom: 14px;
}
.sub-menu-item {
  padding: 8px 14px;
  border-radius: 8px;
  border: 1px solid #dcdfe6;
  color: #606266;
  cursor: pointer;
  user-select: none;
  background: #fff;
  transition: all 0.2s ease;
}
.sub-menu-item.active {
  color: #409eff;
  border-color: #409eff;
  background: #ecf5ff;
}
.edit-form :deep(.el-form-item) {
  margin-bottom: 20px;
}
.edit-card {
  margin-bottom: 14px;
}
.edit-card :deep(.el-card__header) {
  font-weight: 600;
}
.video-upload-wrap {
  width: 100%;
  display: grid;
  grid-template-columns: auto 1fr;
  gap: 10px;
}
.resource-wrap {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.resource-toolbar {
  display: flex;
  justify-content: flex-end;
}
.resource-item {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  background: #fafafa;
}
.resource-item-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 13px;
  color: #606266;
}
.video-editor-wrap {
  margin-top: 10px;
  width: 100%;
}
.editor-video {
  width: 100%;
  max-height: 320px;
  border-radius: 8px;
  background: #000;
}
.video-meta {
  margin-top: 6px;
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: #606266;
}
.node-config-wrap {
  width: 100%;
  overflow-x: auto;
}
.node-toolbar {
  margin-bottom: 10px;
  display: flex;
  justify-content: flex-end;
}
.node-quick-editor {
  margin-bottom: 10px;
  padding: 10px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  display: grid;
  gap: 10px;
  background: #fafafa;
}
.quick-editor-line {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.quick-node-select {
  width: 280px;
}
.quick-editor-slider {
  width: 100%;
}
.quick-editor-slider :deep(.el-slider) {
  width: 100%;
}
.role-task-cell {
  min-height: 32px;
  padding: 4px 6px;
  border: 1px dashed #dcdfe6;
  border-radius: 6px;
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  flex-wrap: wrap;
}
.role-task-cell:hover {
  border-color: #409eff;
  background: #f5faff;
}
.role-task-cell-empty {
  color: #909399;
  font-size: 12px;
}
.role-chip {
  margin: 0;
}
.role-task-dialog {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 12px;
  min-height: 320px;
}
.role-task-dialog-left,
.role-task-dialog-right {
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 10px;
  background: #fafafa;
}
.dialog-panel-title {
  font-weight: 600;
  color: #303133;
  margin-bottom: 8px;
}
.role-check-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.role-check-item {
  padding: 6px 8px;
  border-radius: 6px;
  cursor: pointer;
}
.role-check-item.active {
  background: #ecf5ff;
}
.task-check-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.task-check-item {
  border: 1px solid #ebeef5;
  border-radius: 6px;
  padding: 8px;
  background: #fff;
}
.task-check-title {
  font-weight: 600;
}
.task-check-detail {
  margin: 6px 0 0 22px;
  color: #606266;
  font-size: 12px;
  line-height: 1.6;
}
.task-check-detail-item {
  white-space: normal;
  word-break: break-word;
}
.task-dialog-actions {
  margin-bottom: 8px;
  display: flex;
  gap: 8px;
}
.member-wrap {
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.member-toolbar {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
@media (max-width: 1200px) {
  .project-edit {
    padding: 16px;
  }
  .video-upload-wrap {
    grid-template-columns: 1fr;
  }
  .quick-node-select {
    width: 100%;
  }
}
</style>
