<template>
  <div class="user-manage">
    <div class="toolbar">
      <el-form :inline="true" :model="filter" class="filter-form">
        <el-form-item label="用户名">
          <el-input v-model="filter.userName" placeholder="用户名" clearable style="width:140px" />
        </el-form-item>
        <el-form-item label="姓名">
          <el-input v-model="filter.userTrueName" placeholder="姓名" clearable style="width:140px" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="load">查询</el-button>
        </el-form-item>
      </el-form>
    </div>
    <el-table :data="tableData" stripe style="width:100%">
      <el-table-column prop="userName" label="用户名" width="120" />
      <el-table-column prop="userTrueName" label="姓名" width="100" />
      <el-table-column prop="roleName" label="角色" width="100" />
      <el-table-column label="账号权限" width="100">
        <template #default="{ row }">
          <el-tag :type="row.enable === 1 ? 'success' : 'info'" size="small">
            {{ row.enable === 1 ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createDate" label="创建时间" width="160" />
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button
            v-if="!isSuperAdminUser(row)"
            link
            type="primary"
            @click="openManage(row)"
          >管理用户</el-button>
          <span v-else class="muted">不可修改</span>
        </template>
      </el-table-column>
    </el-table>
    <div class="pagination-wrap">
      <el-pagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50]"
        :total="total"
        layout="total, sizes, prev, pager, next"
        @size-change="load"
        @current-change="load"
      />
    </div>

    <el-dialog v-model="manageVisible" title="管理用户" width="460px" @closed="resetManageForm">
      <el-form label-width="90px">
        <el-form-item label="用户名">
          <el-input :model-value="manageForm.userName" disabled />
        </el-form-item>
        <el-form-item label="姓名" required>
          <el-input v-model="manageForm.userTrueName" placeholder="请输入姓名" maxlength="20" clearable />
        </el-form-item>
        <el-form-item label="角色" required>
          <el-select v-model="manageForm.role_Id" placeholder="请选择角色" style="width:100%">
            <el-option v-for="r in roleList" :key="r.role_Id" :label="r.roleName" :value="r.role_Id" />
          </el-select>
        </el-form-item>
        <el-form-item label="账号权限" required>
          <el-radio-group v-model="manageForm.enable">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">禁用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="新密码">
          <el-input
            v-model="manageForm.password"
            type="password"
            placeholder="留空则不修改，不少于6位"
            show-password
            clearable
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="manageVisible = false">取消</el-button>
        <el-button type="primary" :loading="manageSaving" @click="saveManage">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import * as userApi from '@/api/user'

const filter = reactive({ userName: '', userTrueName: '' })
const tableData = ref([])
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)

const manageVisible = ref(false)
const manageSaving = ref(false)
const roleList = ref([])
const manageForm = reactive({
  user_Id: null,
  userName: '',
  userTrueName: '',
  role_Id: null,
  roleName: '',
  enable: 1,
  password: ''
})

function isSuperAdminUser(row) {
  return (row?.role_Id ?? row?.Role_Id) === 1
}

function load() {
  const params = { page: page.value, rows: pageSize.value, ...filter }
  userApi.getUserPage(params, true).then((res) => {
    if (res.status && res.data) {
      tableData.value = res.data.rows || []
      total.value = res.data.total ?? 0
    }
  }).catch(() => {
    tableData.value = []
    total.value = 0
  })
}

function resetManageForm() {
  manageForm.user_Id = null
  manageForm.userName = ''
  manageForm.userTrueName = ''
  manageForm.role_Id = null
  manageForm.roleName = ''
  manageForm.enable = 1
  manageForm.password = ''
}

function openManage(row) {
  if (isSuperAdminUser(row)) {
    ElMessage.warning('超级管理员不可修改')
    return
  }
  manageForm.user_Id = row.user_Id
  manageForm.userName = row.userName
  manageForm.userTrueName = row.userTrueName
  manageForm.role_Id = row.role_Id
  manageForm.roleName = row.roleName
  manageForm.enable = row.enable ?? 1
  manageForm.password = ''
  userApi.getRoleList().then((list) => {
    roleList.value = list || []
    manageVisible.value = true
  }).catch(() => ElMessage.error('获取角色列表失败'))
}

async function saveManage() {
  if (!manageForm.userTrueName?.trim()) {
    ElMessage.warning('请输入姓名')
    return
  }
  if (manageForm.role_Id == null) {
    ElMessage.warning('请选择角色')
    return
  }
  if (manageForm.password && manageForm.password.length < 6) {
    ElMessage.warning('密码不少于6位')
    return
  }

  const role = roleList.value.find(x => x.role_Id === manageForm.role_Id)
  manageSaving.value = true
  try {
    const updateRes = await userApi.updateUser({
      user_Id: manageForm.user_Id,
      userName: manageForm.userName,
      userTrueName: manageForm.userTrueName.trim(),
      role_Id: manageForm.role_Id,
      roleName: role?.roleName ?? '',
      enable: manageForm.enable
    }, true)
    if (!updateRes.status) {
      ElMessage.error(updateRes.message || '更新失败')
      return
    }

    if (manageForm.password) {
      const pwdRes = await userApi.modifyUserPwd(manageForm.userName, manageForm.password, true)
      if (!pwdRes.status) {
        ElMessage.error(pwdRes.message || '密码更新失败')
        return
      }
    }

    ElMessage.success('用户信息已更新')
    manageVisible.value = false
    load()
  } catch (e) {
    ElMessage.error(e?.message || '更新失败')
  } finally {
    manageSaving.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.user-manage { width: 100%; }
.toolbar { margin-bottom: 16px; }
.pagination-wrap { margin-top: 16px; display: flex; justify-content: flex-end; }
.muted { color: #909399; font-size: 13px; }
</style>
