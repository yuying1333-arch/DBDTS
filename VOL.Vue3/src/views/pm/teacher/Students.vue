<template>
  <div class="page">
    <TeacherPageHeader>
      <template #left>
        <el-button type="primary" size="small" @click="load">刷新</el-button>
      </template>
    </TeacherPageHeader>
    <el-table :data="rows" stripe style="width:100%" height="calc(100vh - 220px)">
      <el-table-column type="index" label="序号" width="60" />
      <el-table-column prop="userId" label="用户ID" width="90" />
      <el-table-column prop="userName" label="账号" width="140" />
      <el-table-column prop="userTrueName" label="姓名" width="120" />
      <el-table-column prop="roleName" label="身份组" width="160" />
      <el-table-column prop="contact" label="联系方式" width="160" />
      <el-table-column prop="auditStatusText" label="审核状态" width="100" />
      <el-table-column prop="createDate" label="注册时间" width="170" />
    </el-table>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'
import TeacherPageHeader from './TeacherPageHeader.vue'

const rows = ref([])
const { projectId } = useTeacherProject()
function load() {
  if (!projectId.value) {
    rows.value = []
    return ElMessage.warning('请先选择项目')
  }
  drillApi.getMembers(projectId.value, null, true).then((res) => {
    if (res.status) {
      rows.value = (res.data || []).map(x => ({
        id: x.Id ?? x.id,
        userId: x.UserId ?? x.userId,
        userName: x.UserName ?? x.userName,
        userTrueName: x.UserTrueName ?? x.userTrueName,
        roleName: x.RoleName ?? x.roleName,
        contact: x.Contact ?? x.contact,
        auditStatusText: (x.AuditStatus ?? x.auditStatus ?? 0) === 1 ? '已通过' : '待审核',
        createDate: x.CreateDate ?? x.createDate
      }))
    }
  })
}
onMounted(load)
</script>

<style scoped>
.page { height: 100%; display:flex; flex-direction: column; gap:12px; }
</style>

