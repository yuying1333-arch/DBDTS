<template>
  <div class="project-detail">
    <template v-if="!projectId">
      <el-empty description="请先在项目列表中选择一项" />
    </template>
    <template v-else>
      <div class="detail-layout">
        <div class="left-block">
          <div class="block title">项目基本信息</div>
          <div v-if="info" class="info-form">
            <div class="info-row"><span class="label">项目名称：</span>{{ info.name }}</div>
            <div class="info-row"><span class="label">项目编号：</span>{{ info.code }}</div>
            <div class="info-row"><span class="label">状态：</span>{{ info.statusName }}</div>
            <div class="info-row"><span class="label">创建时间：</span>{{ info.createDate }}</div>
            <div class="info-row"><span class="label">备注：</span>{{ info.remark || '-' }}</div>
          </div>
        </div>
        <div class="right-block">
          <el-tabs v-model="activeTab">
            <el-tab-pane label="项目信息" name="info">
              <div v-if="info" class="tab-content">
                <p><strong>项目名称：</strong>{{ info.name }}</p>
                <p><strong>项目编号：</strong>{{ info.code }}</p>
                <p><strong>状态：</strong>{{ info.statusName }}</p>
                <p><strong>备注：</strong>{{ info.remark || '-' }}</p>
              </div>
            </el-tab-pane>
            <el-tab-pane label="项目成员管理" name="members">
              <div class="tab-content student-tab">
                <div class="student-toolbar">
                  <el-button type="primary" size="small" @click="showAddStudent">添加学生</el-button>
                  <el-button size="small" type="warning" :disabled="!selectedStudents.length" @click="showBatchEditRole">
                    批量改角色（{{ selectedStudents.length }}）
                  </el-button>
                  <el-button size="small" @click="loadStudents">刷新</el-button>
                </div>
                <el-table ref="studentTableRef" :data="students" stripe size="small" max-height="360" @selection-change="onStudentSelectionChange">
                  <el-table-column type="selection" width="48" />
                  <el-table-column type="index" label="序号" width="60" />
                  <el-table-column prop="userName" label="账号" width="120" />
                  <el-table-column prop="userTrueName" label="姓名" width="100" />
                  <el-table-column prop="roleName" label="角色" width="100" />
                  <el-table-column prop="auditStatusText" label="状态" width="90" />
                  <el-table-column prop="contact" label="联系方式" />
                  <el-table-column label="操作" width="170" fixed="right">
                    <template #default="{ row }">
                      <el-button type="primary" link size="small" @click="showEditRole(row)">编辑角色</el-button>
                      <el-button v-if="row.auditStatus === 0" type="primary" link size="small" @click="approveStudent(row)">审核通过</el-button>
                    </template>
                  </el-table-column>
                </el-table>
              </div>
            </el-tab-pane>
            <el-tab-pane label="项目资源" name="resources">
              <div class="tab-content">
                <el-empty v-if="!resources.length" description="未配置项目视频资源" />
                <div v-else class="resource-list">
                  <div v-for="res in resources" :key="res.id || res.videoUrl" class="resource-item">
                    <p><strong>资源名称：</strong>{{ res.resourceName || '项目视频资源' }}</p>
                    <p><strong>视频地址：</strong>{{ res.videoUrl }}</p>
                    <video :src="res.videoUrl" controls class="resource-video" />
                  </div>
                </div>
              </div>
            </el-tab-pane>
          </el-tabs>
        </div>
      </div>
    </template>

    <el-dialog v-model="addStudentVisible" title="添加学生" width="480px" @close="resetAddForm">
      <el-form :model="addForm" :rules="addRules" ref="addFormRef" label-width="90px">
        <el-form-item label="角色" prop="roleName">
          <el-select v-model="addForm.roleName" placeholder="请选择" style="width:100%" filterable>
            <el-option v-for="r in roleOptions" :key="r.id" :label="r.roleName" :value="r.roleName" />
          </el-select>
        </el-form-item>
        <el-form-item label="用户名" prop="userName">
          <el-input v-model="addForm.userName" placeholder="系统登录账号" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="addForm.password" type="password" placeholder="至少6位" show-password />
        </el-form-item>
        <el-form-item label="真实姓名" prop="userTrueName">
          <el-input v-model="addForm.userTrueName" placeholder="姓名" />
        </el-form-item>
        <el-form-item label="所在单位">
          <el-input v-model="addForm.org" placeholder="选填" />
        </el-form-item>
        <el-form-item label="职务">
          <el-input v-model="addForm.jobTitle" placeholder="选填" />
        </el-form-item>
        <el-form-item label="联系方式">
          <el-input v-model="addForm.contact" placeholder="手机号等" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="addStudentVisible=false">取消</el-button>
        <el-button type="primary" @click="submitAddStudent" :loading="addSubmitting">确定</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="editRoleVisible" title="编辑学生角色" width="420px" @close="resetEditRoleForm">
      <el-form :model="editRoleForm" :rules="editRoleRules" ref="editRoleFormRef" label-width="90px">
        <el-form-item label="学生姓名">
          <el-input v-model="editRoleForm.userTrueName" disabled />
        </el-form-item>
        <el-form-item label="角色" prop="roleName">
          <el-select v-model="editRoleForm.roleName" placeholder="请选择" style="width:100%" filterable>
            <el-option v-for="r in roleOptions" :key="r.id" :label="r.roleName" :value="r.roleName" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editRoleVisible = false">取消</el-button>
        <el-button type="primary" @click="submitEditRole" :loading="editRoleSubmitting">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="batchRoleVisible" title="批量修改角色" width="420px" @close="resetBatchRoleForm">
      <el-form :model="batchRoleForm" :rules="batchRoleRules" ref="batchRoleFormRef" label-width="110px">
        <el-form-item label="已选学生">
          <el-input :model-value="`${selectedStudents.length} 人`" disabled />
        </el-form-item>
        <el-form-item label="目标角色" prop="roleName">
          <el-select v-model="batchRoleForm.roleName" placeholder="请选择" style="width:100%" filterable>
            <el-option v-for="r in roleOptions" :key="r.id" :label="r.roleName" :value="r.roleName" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="batchRoleVisible = false">取消</el-button>
        <el-button type="primary" @click="submitBatchRole" :loading="batchRoleSubmitting">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { nextTick, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import * as api from '@/api/project'
import * as drillApi from '@/api/drill'

const props = defineProps({ projectId: [String, Number] })
const emit = defineEmits(['edit', 'refresh'])

const info = ref(null)
const students = ref([])
const studentTableRef = ref(null)
const selectedStudents = ref([])
const activeTab = ref('info')
const resources = ref([])

const roleOptions = ref([])

const addStudentVisible = ref(false)
const addSubmitting = ref(false)
const addFormRef = ref(null)
const addForm = ref({
  roleName: '',
  userName: '',
  password: '',
  userTrueName: '',
  org: '',
  jobTitle: '',
  contact: ''
})
const addRules = {
  roleName: [{ required: true, message: '请选择角色', trigger: 'change' }],
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }, { min: 6, message: '至少6位', trigger: 'blur' }],
  userTrueName: [{ required: true, message: '请输入真实姓名', trigger: 'blur' }]
}

const editRoleVisible = ref(false)
const editRoleSubmitting = ref(false)
const editRoleFormRef = ref(null)
const editRoleForm = ref({
  id: null,
  userTrueName: '',
  roleName: ''
})
const editRoleRules = {
  roleName: [{ required: true, message: '请选择角色', trigger: 'change' }]
}

const batchRoleVisible = ref(false)
const batchRoleSubmitting = ref(false)
const batchRoleFormRef = ref(null)
const batchRoleForm = ref({
  roleName: ''
})
const batchRoleRules = {
  roleName: [{ required: true, message: '请选择角色', trigger: 'change' }]
}

function getStatusText(status) {
  const s = Number(status ?? 0)
  if (s === 1) return '进行中'
  if (s === 2) return '已结束'
  return '未开始'
}

function normalizeProjectInfo(row) {
  if (!row) return null
  const status = row.status ?? row.Status ?? 0
  return {
    ...row,
    status,
    statusName: row.statusName || row.StatusName || getStatusText(status)
  }
}

function mergeRoleOption(roleName) {
  const name = String(roleName || '').trim()
  if (!name) return
  if (roleOptions.value.some(x => x.roleName === name)) return
  roleOptions.value.push({ id: `custom_${name}`, roleName: name, enable: 1 })
}

function loadRoleOptions() {
  drillApi.getDrillRoles(false).then((res) => {
    if (!res.status) return
    const list = (res.data || [])
      .filter(x => (x.enable ?? x.Enable ?? 0) === 1)
      .map(x => ({
        id: x.id ?? x.Id ?? x.roleNo ?? x.RoleNo ?? x.roleName ?? x.RoleName,
        roleName: x.roleName ?? x.RoleName ?? '',
        enable: x.enable ?? x.Enable ?? 1
      }))
      .filter(x => x.roleName)
    roleOptions.value = list
    students.value.forEach(x => mergeRoleOption(x.roleName))
  }).catch(() => { roleOptions.value = [] })
}

function load() {
  if (!props.projectId) {
    info.value = null
    students.value = []
    selectedStudents.value = []
    return
  }
  api.getProjectById(props.projectId).then((res) => {
    if (res.status && res.data) info.value = normalizeProjectInfo(res.data)
    else info.value = null
  })
  loadRoleOptions()
  loadStudents()
  loadProjectResources()
}

function loadStudents() {
  if (!props.projectId) return
  drillApi.getMembers(props.projectId, null, false).then((res) => {
    if (res.status && res.data) {
      students.value = (res.data || []).map(x => ({
        id: x.Id ?? x.id,
        userName: x.UserName ?? x.userName,
        userTrueName: x.UserTrueName ?? x.userTrueName,
        roleName: x.RoleName ?? x.roleName,
        contact: x.Contact ?? x.contact,
        auditStatus: x.AuditStatus ?? x.auditStatus ?? 0,
        auditStatusText: (x.AuditStatus ?? x.auditStatus) === 1 ? '已通过' : '待审核'
      }))
      students.value.forEach(x => mergeRoleOption(x.roleName))
      selectedStudents.value = []
      nextTick(() => { studentTableRef.value?.clearSelection() })
    } else {
      students.value = []
    }
  }).catch(() => {
    students.value = []
  })
}

function loadProjectResources() {
  if (!props.projectId) return
  drillApi.getProjectResources(props.projectId, false).then((res) => {
    if (res.status) {
      resources.value = (res.data || []).map(x => ({
        id: x.id ?? x.Id,
        resourceName: x.resourceName ?? x.ResourceName,
        videoUrl: x.videoUrl ?? x.VideoUrl,
        enable: x.enable ?? x.Enable ?? 1
      }))
    } else {
      resources.value = []
    }
  }).catch(() => {
    resources.value = []
  })
}

function showAddStudent() {
  if (!roleOptions.value.length) loadRoleOptions()
  addStudentVisible.value = true
}

function resetAddForm() {
  addForm.value = { roleName: '', userName: '', password: '', userTrueName: '', org: '', jobTitle: '', contact: '' }
  addFormRef.value?.resetFields()
}

function submitAddStudent() {
  addFormRef.value?.validate((valid) => {
    if (!valid) return
    addSubmitting.value = true
    drillApi.addMember({
      projectId: Number(props.projectId),
      roleName: addForm.value.roleName,
      userName: addForm.value.userName,
      password: addForm.value.password,
      userTrueName: addForm.value.userTrueName,
      org: addForm.value.org || undefined,
      jobTitle: addForm.value.jobTitle || undefined,
      contact: addForm.value.contact || undefined
    }, true).then((res) => {
      addSubmitting.value = false
      if (res.status) {
        ElMessage.success(res.message || '添加成功')
        addStudentVisible.value = false
        loadStudents()
        emit('refresh')
      } else ElMessage.error(res.message || '添加失败')
    }).catch(() => { addSubmitting.value = false })
  })
}

function approveStudent(row) {
  drillApi.approveMember(row.id, true).then((res) => {
    if (res.status) {
      ElMessage.success('已审核通过')
      loadStudents()
    } else ElMessage.error(res.message || '操作失败')
  })
}

function showEditRole(row) {
  if (!row?.id) return
  mergeRoleOption(row.roleName)
  if (!roleOptions.value.length) loadRoleOptions()
  editRoleForm.value = {
    id: row.id,
    userTrueName: row.userTrueName || row.userName || '',
    roleName: row.roleName || ''
  }
  editRoleVisible.value = true
}

function resetEditRoleForm() {
  editRoleForm.value = { id: null, userTrueName: '', roleName: '' }
  editRoleFormRef.value?.resetFields()
}

function submitEditRole() {
  editRoleFormRef.value?.validate((valid) => {
    if (!valid) return
    editRoleSubmitting.value = true
    drillApi.updateMemberRole(editRoleForm.value.id, editRoleForm.value.roleName, true).then((res) => {
      editRoleSubmitting.value = false
      if (res.status) {
        ElMessage.success(res.message || '角色已更新')
        editRoleVisible.value = false
        loadStudents()
      } else ElMessage.error(res.message || '更新失败')
    }).catch(() => { editRoleSubmitting.value = false })
  })
}

function onStudentSelectionChange(rows) {
  selectedStudents.value = Array.isArray(rows) ? rows : []
}

function showBatchEditRole() {
  if (!selectedStudents.value.length) return ElMessage.warning('请先勾选学生')
  if (!roleOptions.value.length) loadRoleOptions()
  batchRoleVisible.value = true
}

function resetBatchRoleForm() {
  batchRoleForm.value = { roleName: '' }
  batchRoleFormRef.value?.resetFields()
}

function submitBatchRole() {
  batchRoleFormRef.value?.validate((valid) => {
    if (!valid) return
    const ids = selectedStudents.value.map(x => Number(x.id)).filter(x => x > 0)
    if (!ids.length) return ElMessage.warning('未选择有效学生')
    batchRoleSubmitting.value = true
    drillApi.batchUpdateMemberRole({
      projectId: Number(props.projectId),
      ids,
      roleName: batchRoleForm.value.roleName
    }, true).then((res) => {
      batchRoleSubmitting.value = false
      if (res.status) {
        ElMessage.success(res.message || '批量修改成功')
        batchRoleVisible.value = false
        loadStudents()
      } else ElMessage.error(res.message || '批量修改失败')
    }).catch(() => { batchRoleSubmitting.value = false })
  })
}

watch(() => props.projectId, load, { immediate: true })
watch(activeTab, (v) => {
  if (v === 'members') loadStudents()
  if (v === 'resources') loadProjectResources()
})
</script>

<style scoped>
.project-detail {
  min-height: 320px;
}
.detail-layout {
  display: flex;
  gap: 24px;
}
.left-block {
  width: 320px;
  flex-shrink: 0;
}
.left-block .block {
  background: #f5f7fa;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 12px;
}
.left-block .block.title {
  font-weight: 600;
  color: #303133;
  margin-bottom: 8px;
}
.info-form .info-row {
  padding: 6px 0;
  font-size: 14px;
}
.info-form .label {
  color: #606266;
  margin-right: 8px;
}
.right-block {
  flex: 1;
  min-width: 0;
  background: #fff;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  padding: 16px;
}
.tab-content {
  padding: 8px 0;
}
.tab-content p { margin: 8px 0; }
.student-tab .student-toolbar { margin-bottom: 12px; display: flex; gap: 8px; }
.resource-list { display: flex; flex-direction: column; gap: 12px; }
.resource-item { border: 1px solid #ebeef5; border-radius: 6px; padding: 10px; background: #fafafa; }
.resource-video { width: 100%; max-width: 680px; max-height: 360px; background: #000; border-radius: 6px; }
</style>
