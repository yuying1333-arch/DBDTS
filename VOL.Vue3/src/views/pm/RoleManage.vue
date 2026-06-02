<template>
  <div class="page">
    <div class="header">
      <div class="left">
        <el-button type="primary" @click="openAdd">新增角色</el-button>
        <el-button @click="load">刷新</el-button>
      </div>
      <div class="right">
        <el-tag size="small">角色管理</el-tag>
      </div>
    </div>

    <el-table :data="rows" stripe style="width: 100%">
      <el-table-column prop="roleNo" label="角色编号" width="120" />
      <el-table-column prop="roleName" label="角色名" width="160" />
      <el-table-column label="标识" width="140">
        <template #default="{ row }">
          <el-tag v-if="row.marker" size="small" type="info">{{ row.marker }}</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="enable" label="启用" width="90">
        <template #default="{ row }">
          <el-tag :type="row.enable === 1 ? 'success' : 'info'">{{ row.enable === 1 ? '是' : '否' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <el-button size="small" @click="openEdit(row)">编辑</el-button>
          <el-button size="small" type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="720px" :close-on-click-modal="false">
      <el-form :model="form" label-width="120px">
        <el-form-item label="角色编号">
          <el-input v-model="form.roleNo" placeholder="" />
        </el-form-item>
        <el-form-item label="角色名" required>
          <el-input v-model="form.roleName" placeholder="输入角色名称" />
        </el-form-item>
        <el-form-item label="标识">
          <el-input v-model="form.marker" placeholder="" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.enable" :active-value="1" :inactive-value="0" />
        </el-form-item>
        <el-form-item label="角色任务" required>
          <div class="task-editor">
            <div class="task-block" v-for="(task, ti) in taskEditor" :key="task._key">
              <div class="task-head">
                <div class="task-title" style="margin-top: 6px;">任务 {{ ti + 1 }}</div>
                <el-input v-model="task.title" placeholder="任务标题" style="margin-top: 6px;" />
                <el-button
                  type="danger"
                  text
                  @click="removeTask(ti)"
                  :disabled="taskEditor.length <= 1"
                >删除任务</el-button>
                
              </div>
              
              <div class="task-items">
                <div class="task-item-row" v-for="(it, ii) in task.items" :key="it._key">
                  <el-input v-model="it.text" placeholder="任务内容" />
                  <el-button type="danger" text @click="removeItem(ti, ii)" :disabled="task.items.length <= 1">
                    删除内容
                  </el-button>
                </div>
              </div>
              <div class="task-actions">
                <el-button size="small" plain style="margin-top: 12px;" @click="addItem(ti)">新增任务内容</el-button>
              </div>
            </div>
            <el-button size="small" type="primary" plain style="margin-top: 12px;" @click="addTask">
              新增任务
            </el-button>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submit">
          {{ editId ? '保存' : '创建' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import * as drillApi from '@/api/drill'

const rows = ref([])

const dialogVisible = ref(false)
const saving = ref(false)
const editId = ref(null)
const dialogTitle = computed(() => (editId.value ? '编辑角色' : '新增角色'))

const form = reactive({
  roleNo: '',
  roleName: '',
  marker: '',
  enable: 1,
  taskBookJson: ''
})

const taskEditor = ref([])

function resetForm() {
  editId.value = null
  form.roleNo = ''
  form.roleName = ''
  form.marker = ''
  form.enable = 1
  form.taskBookJson = ''
  taskEditor.value = [
    {
      id: 't1',
      _key: Date.now() + '_t1',
      title: '',
      items: [{ id: 'i1', _key: Date.now() + '_i1', text: '' }]
    }
  ]
}

function load() {
  drillApi.getDrillRoles(false).then((res) => {
    if (res.status) rows.value = res.data || []
  }).catch(() => {})
}

function openAdd() {
  resetForm()
  dialogVisible.value = true
}

function openEdit(row) {
  resetForm()
  editId.value = row.id ?? row.Id ?? row.ID ?? null
  form.roleNo = row.roleNo ?? ''
  form.roleName = row.roleName ?? ''
  form.marker = row.marker ?? ''
  form.enable = row.enable ?? 1
  form.taskBookJson = row.taskBookJson ?? ''
  taskEditor.value = parseTaskBook(form.taskBookJson)
  dialogVisible.value = true
}

function parseTaskBook(taskBookJson) {
  if (!taskBookJson) return [
    { id: 't1', _key: Date.now() + '_t1', title: '', items: [{ id: 'i1', _key: Date.now() + '_i1', text: '' }] }
  ]
  try {
    const obj = typeof taskBookJson === 'string' ? JSON.parse(taskBookJson) : taskBookJson
    const tasks = obj?.tasks || []
    if (!Array.isArray(tasks) || tasks.length === 0) throw new Error('invalid')
    return tasks.map((t, ti) => {
      const taskId = t.id || `t${ti + 1}`
      const items = (t.items || []).map((it, ii) => {
        const itemId = it.id || `i${ti + 1}_${ii + 1}`
        return { id: String(itemId), _key: Date.now() + '_' + itemId + '_' + ii, text: it.text ?? '' }
      })
      if (items.length === 0) items.push({ id: `i${ti + 1}_1`, _key: Date.now() + '_i_' + ti + '_1', text: '' })
      return { id: String(taskId), _key: Date.now() + '_t_' + ti, title: t.title ?? '', items }
    })
  } catch (e) {
    return [
      { id: 't1', _key: Date.now() + '_t1', title: '', items: [{ id: 'i1', _key: Date.now() + '_i1', text: '' }] }
    ]
  }
}

function buildTaskBookJson() {
  const tasks = (taskEditor.value || []).map((t, ti) => {
    const taskId = t.id || `t${ti + 1}`
    return {
      id: String(taskId),
      title: (t.title ?? '').trim(),
      items: (t.items || []).map((it, ii) => ({
        id: String(it.id || `i${ti + 1}_${ii + 1}`),
        text: (it.text ?? '').trim()
      }))
    }
  })
  return JSON.stringify({ tasks })
}

function submit() {
  if (!form.roleName?.trim()) return ElMessage.warning('请输入角色名')
  if (!form.roleNo?.trim()) form.roleNo = ''
  if (!(taskEditor.value || []).length) return ElMessage.warning('请配置任务书')

  // 校验所有任务标题和条目内容非空
  for (const t of taskEditor.value) {
    if (!t.title?.trim()) return ElMessage.warning('请填写任务标题')
    for (const it of (t.items || [])) {
      if (!it.text?.trim()) return ElMessage.warning('请填写所有条目内容')
    }
  }

  form.taskBookJson = buildTaskBookJson()

  saving.value = true
  const payload = {
    roleNo: form.roleNo?.trim() || undefined,
    roleName: form.roleName.trim(),
    marker: form.marker?.trim() || undefined,
    enable: form.enable,
    taskBookJson: form.taskBookJson
  }

  const p = editId.value
    ? drillApi.updateDrillRole(editId.value, payload, true)
    : drillApi.addDrillRole(payload, true)

  p.then((res) => {
    saving.value = false
    if (res.status) {
      ElMessage.success(res.message || '保存成功')
      dialogVisible.value = false
      load()
    } else {
      ElMessage.error(res.message || '保存失败')
    }
  }).catch((e) => {
    saving.value = false
    ElMessage.error(e?.message || '保存失败')
  })
}

function addTask() {
  taskEditor.value.push({
    id: 't' + (taskEditor.value.length + 1),
    _key: Date.now() + '_' + Math.random().toString(16).slice(2),
    title: '',
    items: [{ id: 'i1', _key: Date.now() + '_i1_' + Math.random().toString(16).slice(2), text: '' }]
  })
}

function removeTask(ti) {
  if (taskEditor.value.length <= 1) return
  taskEditor.value.splice(ti, 1)
}

function addItem(ti) {
  const t = taskEditor.value[ti]
  if (!t) return
  const nextIndex = (t.items || []).length + 1
  t.items.push({ id: `i${ti + 1}_${nextIndex}`, _key: Date.now() + '_it_' + ti + '_' + nextIndex, text: '' })
}

function removeItem(ti, ii) {
  const t = taskEditor.value[ti]
  if (!t || (t.items || []).length <= 1) return
  t.items.splice(ii, 1)
}

function remove(row) {
  const id = row.id ?? row.Id ?? row.ID
  if (!id) return
  ElMessageBox.confirm('确认删除该角色？', '提示', { type: 'warning' }).then(() => {
    drillApi.deleteDrillRole(id, true).then((res) => {
      if (res.status) {
        ElMessage.success(res.message || '删除成功')
        load()
      } else {
        ElMessage.error(res.message || '删除失败')
      }
    }).catch((e) => ElMessage.error(e?.message || '删除失败'))
  }).catch(() => {})
}

onMounted(load)
</script>

<style scoped>
.page { display: flex; flex-direction: column; gap: 12px; }
.header {
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  padding: 10px 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.header .left { display: flex; gap: 12px; align-items: center; }
.task-head {
  display: flex;
  align-items: center;
  gap: 10px;
}
.task-head .task-title {
  flex: 0 0 auto;
  white-space: nowrap;
}
.task-head .el-input {
  flex: 1;
}
.task-item-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 8px;
}
.task-item-row .el-input {
  flex: 1;
}
</style>

