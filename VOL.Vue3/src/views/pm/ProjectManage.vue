<template>
  <div class="project-manage">
    <!-- 工具栏：新增/编辑/删除 | 导入/导出 | 项目列表/项目详情 -->
    <div class="toolbar">
      <div class="tool-row">
        <el-button type="primary" @click="onAdd">新增项目</el-button>
        <el-button :disabled="!selectedId" @click="onEdit">编辑项目</el-button>
        <el-button type="danger" :disabled="!selectedId" @click="onDelete">删除项目</el-button>
      </div>
      <div class="tool-row">
        <el-button @click="onImport">导入</el-button>
        <el-button @click="onExport">导出</el-button>
      </div>
      <div class="tool-row view-toggle">
        <el-radio-group v-model="viewMode" size="default">
          <el-radio-button value="list">项目列表</el-radio-button>
          <el-radio-button value="detail">项目详情</el-radio-button>
        </el-radio-group>
      </div>
    </div>
    <input ref="importInputRef" type="file" accept=".txt,text/plain" style="display:none" @change="onImportFileChange" />
    <el-dialog v-model="importPreviewVisible" title="导入预览" width="760px">
      <div v-if="importPreviewData" class="import-preview">
        <div class="preview-row">
          <span class="label">项目名称：</span>
          <span>{{ importPreviewData.project?.name || '-' }}</span>
        </div>
        <div class="preview-row">
          <span class="label">项目编号：</span>
          <span>{{ importPreviewData.project?.code || '-' }}</span>
        </div>
        <div class="preview-row">
          <span class="label">项目状态：</span>
          <span>{{ importPreviewData.project?.status ?? 0 }}</span>
        </div>
        <div class="preview-row">
          <span class="label">视频链接：</span>
          <span class="break">{{ importPreviewData.resource?.videoUrl || '-' }}</span>
        </div>
        <div class="preview-row">
          <span class="label">节点数量：</span>
          <span>{{ importPreviewData.nodes?.length || 0 }}</span>
        </div>
        <div class="preview-row">
          <span class="label">成员数量：</span>
          <span>{{ importPreviewData.members?.length || 0 }}</span>
        </div>
        <el-divider />
        <div class="preview-row">
          <span class="label">编号冲突策略：</span>
          <el-radio-group v-model="importConflictAction">
            <el-radio value="overwrite">覆盖现有项目</el-radio>
            <el-radio value="new_copy">创建新副本</el-radio>
            <el-radio value="cancel">取消导入</el-radio>
          </el-radio-group>
        </div>
        <p v-if="importConflictProject" class="warn-tip">
          检测到同编号项目：{{ importConflictProject.name }}（ID:{{ importConflictProject.id }}）
        </p>
        <el-divider />
        <el-tabs class="preview-tabs">
          <el-tab-pane :label="`节点清单（${importPreviewData.nodes?.length || 0}）`">
            <el-table :data="importPreviewData.nodes || []" border size="small" height="220px" style="width:100%">
              <el-table-column type="index" label="#" width="50" />
              <el-table-column prop="nodeCode" label="节点编码" width="150" />
              <el-table-column prop="nodeName" label="节点名称" min-width="160" />
              <el-table-column prop="videoStartSeconds" label="开始秒" width="90" />
              <el-table-column prop="videoEndSeconds" label="结束秒" width="90">
                <template #default="{ row }">
                  <span>{{ row.videoEndSeconds == null ? '到末尾' : row.videoEndSeconds }}</span>
                </template>
              </el-table-column>
            </el-table>
          </el-tab-pane>
          <el-tab-pane :label="`成员清单（${importPreviewData.members?.length || 0}）`">
            <el-table :data="importPreviewData.members || []" border size="small" height="220px" style="width:100%">
              <el-table-column type="index" label="#" width="50" />
              <el-table-column prop="userName" label="账号" width="130" />
              <el-table-column prop="userTrueName" label="姓名" width="120" />
              <el-table-column prop="roleName" label="项目角色" width="140" />
              <el-table-column prop="contact" label="联系方式" min-width="140" />
              <el-table-column prop="password" label="初始密码" width="120" />
            </el-table>
          </el-tab-pane>
        </el-tabs>
      </div>
      <template #footer>
        <el-button @click="importPreviewVisible=false">取消</el-button>
        <el-button type="primary" :disabled="importConflictAction==='cancel'" @click="confirmImport">确认导入</el-button>
      </template>
    </el-dialog>

    <!-- 内容区 -->
    <div class="content">
      <ProjectList
        v-if="viewMode === 'list'"
        :selected-id="selectedId"
        @select="onSelect"
        @refresh="loadList"
      />
      <ProjectDetail
        v-else
        :project-id="selectedId"
        @edit="onEdit"
        @refresh="loadList"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import ProjectList from './ProjectList.vue'
import ProjectDetail from './ProjectDetail.vue'
import * as api from '@/api/project'
import * as drillApi from '@/api/drill'

const router = useRouter()
const viewMode = ref('list')
const selectedId = ref(null)
const importInputRef = ref(null)
const importPreviewVisible = ref(false)
const importPreviewData = ref(null)
const importConflictProject = ref(null)
const importConflictAction = ref('new_copy')

function onAdd() {
  router.push('/admin/project/edit')
}

function onEdit() {
  if (selectedId.value) router.push({ name: 'adminProjectEdit', params: { id: selectedId.value } })
}

function onDelete() {
  if (!selectedId.value) return
  ElMessageBox.confirm('确认删除所选项目？', '提示', {
    type: 'warning'
  }).then(() => {
    api.deleteProject([selectedId.value], true).then((res) => {
      if (res.status) {
        ElMessage.success(res.message || '删除成功')
        selectedId.value = null
        loadList()
      } else {
        ElMessage.error(res.message || '删除失败')
      }
    }).catch(() => ElMessage.error('删除失败'))
  }).catch(() => {})
}

function onImport() {
  importInputRef.value && (importInputRef.value.value = '')
  importInputRef.value?.click()
}

function onExport() {
  if (!selectedId.value) return ElMessage.warning('请先选择要导出的项目')
  exportProjectAsTxt(Number(selectedId.value))
}

function onSelect(id) {
  selectedId.value = id
}

function loadList() {
  // 子组件自行请求；这里仅用于删除后通知列表刷新
  selectedId.value = selectedId.value
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

function parseStatusText(v) {
  const t = String(v ?? '').trim()
  if (t === '进行中' || t === '1') return 1
  if (t === '已结束' || t === '2') return 2
  return 0
}

function toTxtContent(bundle) {
  const project = bundle.project || {}
  const resource = bundle.resource || {}
  const nodes = bundle.nodes || []
  const members = bundle.members || []
  const lines = []
  lines.push('# 项目导出模板 v1')
  lines.push('# 可直接编辑后用于导入')
  lines.push('')
  lines.push('[项目信息]')
  lines.push(`项目名称: ${project.name || ''}`)
  lines.push(`项目编号: ${project.code || ''}`)
  lines.push(`项目状态: ${project.statusName || (project.status ?? 0)}`)
  lines.push(`项目备注: ${project.remark || ''}`)
  lines.push('')
  lines.push('[项目资源]')
  lines.push(`资源名称: ${resource.resourceName || '项目视频资源'}`)
  lines.push(`视频链接: ${resource.videoUrl || ''}`)
  lines.push('')
  lines.push('[节点时间段]')
  nodes.forEach((n) => {
    lines.push(`- 节点编码: ${n.nodeCode || ''} | 节点名称: ${n.nodeName || ''} | 开始秒: ${n.videoStartSeconds ?? 0} | 结束秒: ${n.videoEndSeconds ?? ''}`)
  })
  lines.push('')
  lines.push('[成员信息]')
  members.forEach((m) => {
    lines.push(`- 账号: ${m.userName || ''} | 姓名: ${m.userTrueName || ''} | 项目角色: ${m.roleName || ''} | 联系方式: ${m.contact || ''} | 初始密码: 123456`)
  })
  lines.push('')
  lines.push('[导入说明]')
  lines.push('1. 保留分节标题与字段名，按“键: 值”修改')
  lines.push('2. 节点时间段中，结束秒留空表示播放到下一个节点开始或视频末尾')
  lines.push('3. 成员“初始密码”可改，导入时默认6位及以上')
  return lines.join('\n')
}

function parseKeyValueLine(line) {
  const idx = line.indexOf(':') >= 0 ? line.indexOf(':') : line.indexOf('：')
  if (idx < 0) return null
  const key = line.slice(0, idx).trim()
  const value = line.slice(idx + 1).trim()
  return { key, value }
}

function parsePipeItems(line) {
  return String(line || '')
    .replace(/^\-\s*/, '')
    .split('|')
    .map(x => parseKeyValueLine(x.trim()))
    .filter(Boolean)
    .reduce((acc, kv) => {
      acc[kv.key] = kv.value
      return acc
    }, {})
}

function parseProjectTxt(text) {
  const lines = String(text || '').split(/\r?\n/)
  const result = { project: {}, resource: {}, nodes: [], members: [] }
  let section = ''
  for (const raw of lines) {
    const line = raw.trim()
    if (!line || line.startsWith('#')) continue
    if (line.startsWith('[') && line.endsWith(']')) {
      section = line.slice(1, -1).trim()
      continue
    }
    if (section === '项目信息') {
      const kv = parseKeyValueLine(line)
      if (!kv) continue
      if (kv.key === '项目名称') result.project.name = kv.value
      if (kv.key === '项目编号') result.project.code = kv.value
      if (kv.key === '项目状态') result.project.status = parseStatusText(kv.value)
      if (kv.key === '项目备注') result.project.remark = kv.value
    } else if (section === '项目资源') {
      const kv = parseKeyValueLine(line)
      if (!kv) continue
      if (kv.key === '资源名称') result.resource.resourceName = kv.value
      if (kv.key === '视频链接') result.resource.videoUrl = kv.value
    } else if (section === '节点时间段' && line.startsWith('-')) {
      const m = parsePipeItems(line)
      result.nodes.push({
        nodeCode: m['节点编码'] || '',
        nodeName: m['节点名称'] || '',
        videoStartSeconds: Number(m['开始秒'] || 0),
        videoEndSeconds: m['结束秒'] === '' ? null : (m['结束秒'] == null ? null : Number(m['结束秒']))
      })
    } else if (section === '成员信息' && line.startsWith('-')) {
      const m = parsePipeItems(line)
      result.members.push({
        userName: m['账号'] || '',
        userTrueName: m['姓名'] || '',
        roleName: m['项目角色'] || '',
        contact: m['联系方式'] || '',
        password: (m['初始密码'] || '123456')
      })
    }
  }
  return result
}

async function exportProjectAsTxt(projectId) {
  try {
    const [p, rs, ns, ms] = await Promise.all([
      api.getProjectById(projectId, true),
      drillApi.getProjectResources(projectId, false),
      drillApi.getFlowNodes(projectId, false),
      drillApi.getMembers(projectId, null, false)
    ])
    if (!p?.status) return ElMessage.error('导出失败：未获取到项目信息')
    const resource = ((rs?.data || []).find(x => (x.enable ?? x.Enable ?? 1) === 1) || (rs?.data || [])[0] || null)
    const bundle = {
      project: p.data,
      resource: resource ? {
        resourceName: resource.resourceName ?? resource.ResourceName,
        videoUrl: resource.videoUrl ?? resource.VideoUrl
      } : {},
      nodes: (ns?.data || []).map(x => ({
        nodeCode: x.nodeCode ?? x.NodeCode,
        nodeName: x.nodeName ?? x.NodeName,
        videoStartSeconds: x.videoStartSeconds ?? x.VideoStartSeconds ?? 0,
        videoEndSeconds: x.videoEndSeconds ?? x.VideoEndSeconds ?? null
      })),
      members: (ms?.data || []).map(x => ({
        userName: x.UserName ?? x.userName,
        userTrueName: x.UserTrueName ?? x.userTrueName,
        roleName: x.RoleName ?? x.roleName,
        contact: x.Contact ?? x.contact
      }))
    }
    const content = toTxtContent(bundle)
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `${bundle.project?.name || '项目'}_导出.txt`
    document.body.appendChild(a)
    a.click()
    URL.revokeObjectURL(a.href)
    document.body.removeChild(a)
    ElMessage.success('导出完成')
  } catch (e) {
    ElMessage.error(e?.message || '导出失败')
  }
}

async function onImportFileChange(e) {
  const file = e?.target?.files?.[0]
  if (!file) return
  try {
    const text = await file.text()
    const parsed = parseProjectTxt(text)
    if (!parsed.project?.name || !parsed.project?.code) {
      return ElMessage.error('导入失败：缺少项目名称或项目编号')
    }
    importPreviewData.value = parsed
    importConflictProject.value = await findProjectByCode(parsed.project.code)
    importConflictAction.value = importConflictProject.value ? 'overwrite' : 'new_copy'
    importPreviewVisible.value = true
  } catch (err) {
    ElMessage.error(err?.message || '导入失败')
  }
}

async function findProjectByCode(code) {
  const res = await api.getProjectPage({ page: 1, rows: 100, code }, false)
  const rows = res?.data?.rows || []
  return rows.find(x => String(x.code || '').trim() === String(code || '').trim()) || null
}

function buildImportNodeItems(parsed) {
  return (parsed.nodes || []).map((x, idx) => ({
    nodeCode: x.nodeCode || `node_${String(idx + 1).padStart(2, '0')}`,
    nodeName: x.nodeName || `节点${idx + 1}`,
    stage: 'scene',
    videoStartSeconds: Number(x.videoStartSeconds || 0),
    videoEndSeconds: x.videoEndSeconds == null || Number.isNaN(Number(x.videoEndSeconds)) ? null : Number(x.videoEndSeconds)
  }))
}

async function saveMembersToProject(projectId, members) {
  let memberOk = 0
  let memberFail = 0
  const existingRes = await drillApi.getMembers(projectId, null, false)
  const existingRows = existingRes?.data || []
  const existingMap = new Map(existingRows.map(x => [String(x.UserName ?? x.userName), x]))

  for (const m of (members || [])) {
    if (!m.userName || !m.userTrueName || !m.roleName) { memberFail++; continue }
    const pwd = String(m.password || '123456')
    if (pwd.length < 6) { memberFail++; continue }
    const old = existingMap.get(String(m.userName))
    try {
      if (old) {
        const r = await drillApi.updateMemberRole(old.Id ?? old.id, m.roleName, true)
        if (r?.status) memberOk++
        else memberFail++
      } else {
        const r = await drillApi.addMember({
          projectId,
          roleName: m.roleName,
          userName: m.userName,
          password: pwd,
          userTrueName: m.userTrueName,
          contact: m.contact || undefined
        }, true)
        if (r?.status) memberOk++
        else memberFail++
      }
    } catch {
      memberFail++
    }
  }
  return { memberOk, memberFail }
}

async function confirmImport() {
  const parsed = importPreviewData.value
  if (!parsed) return
  if (importConflictAction.value === 'cancel') {
    importPreviewVisible.value = false
    return
  }
  try {
    let projectId = 0
    let projectName = parsed.project.name
    let projectCode = parsed.project.code

    if (importConflictAction.value === 'overwrite' && importConflictProject.value) {
      const id = Number(importConflictProject.value.id)
      const saveRes = await api.saveProject({
        id,
        name: parsed.project.name,
        code: parsed.project.code,
        status: parsed.project.status ?? 0,
        remark: parsed.project.remark || ''
      }, true)
      if (!saveRes?.status) return ElMessage.error(saveRes?.message || '覆盖失败')
      projectId = id
    } else {
      if (importConflictProject.value && importConflictAction.value === 'new_copy') {
        const stamp = new Date()
        const suffix = `${stamp.getMonth() + 1}${stamp.getDate()}${stamp.getHours()}${stamp.getMinutes()}`
        projectCode = `${projectCode}_copy_${suffix}`
        projectName = `${projectName}(副本)`
      }
      const saveRes = await api.saveProject({
        name: projectName,
        code: projectCode,
        status: parsed.project.status ?? 0,
        remark: parsed.project.remark || ''
      }, true)
      if (!saveRes?.status) return ElMessage.error(saveRes?.message || '新建失败')
      projectId = parseSavedProjectId(saveRes, 0)
    }

    if (!projectId) return ElMessage.error('导入失败：未获取项目ID')

    if (parsed.resource?.videoUrl) {
      await drillApi.saveProjectResource(projectId, {
        resourceName: parsed.resource.resourceName || '项目视频资源',
        videoUrl: parsed.resource.videoUrl,
        enable: 1
      }, true)
    }
    const nodeItems = buildImportNodeItems(parsed)
    if (nodeItems.length) await drillApi.saveFlowNodes(projectId, nodeItems, true)
    const memberResult = await saveMembersToProject(projectId, parsed.members || [])

    selectedId.value = projectId
    importPreviewVisible.value = false
    ElMessage.success(`导入完成：项目ID ${projectId}，成员成功 ${memberResult.memberOk}，失败 ${memberResult.memberFail}`)
  } catch (err) {
    ElMessage.error(err?.message || '导入失败')
  }
}

watch(viewMode, (v) => {
  if (v === 'detail' && !selectedId.value) {
    ElMessage.warning('请先在项目列表中选择一项再查看详情')
  }
})
</script>

<style scoped>
.project-manage {
  background: #fff;
  border-radius: 8px;
  padding: 16px 18px;
  min-height: 400px;
  border: 1px solid #ebeef5;
}
.toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  align-items: center;
  margin-bottom: 14px;
  padding: 10px 12px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  background: #fafafa;
}
.tool-row {
  display: flex;
  gap: 8px;
  align-items: center;
}
.view-toggle {
  margin-left: auto;
}
.content {
  min-height: 360px;
  border: 1px solid #ebeef5;
  border-radius: 8px;
  padding: 12px;
  background: #fff;
}
.import-preview .preview-row {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
  align-items: flex-start;
}
.import-preview .label {
  width: 90px;
  color: #606266;
  flex-shrink: 0;
}
.import-preview .break {
  word-break: break-all;
}
.warn-tip {
  margin-top: 8px;
  color: #e6a23c;
  font-size: 12px;
}
.preview-tabs {
  margin-top: 4px;
}
</style>
