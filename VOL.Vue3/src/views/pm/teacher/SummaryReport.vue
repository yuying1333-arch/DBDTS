<template>
  <div class="page">
    <TeacherPageHeader>
      <template #right>
        <el-button size="small" @click="refresh">刷新</el-button>
      </template>
    </TeacherPageHeader>

    <div class="body">
      <div class="report-list">
        <div class="card-title">学员提交列表</div>
        <div class="list-scroll">
          <div v-for="row in studentReports" :key="row.id" class="report-item">
            <div class="report-meta">
              <div class="name">{{ row.userTrueName || '-' }}</div>
              <div class="role">{{ row.roleName || '-' }}</div>
            </div>
            <div class="report-title">{{ row.title || '未命名报告' }}</div>
            <div class="report-actions">
              <el-button size="small" type="primary" @click="viewReport(row)">查看</el-button>
            </div>
          </div>
          <el-empty v-if="!studentReports.length" description="暂无提交记录" />
        </div>
      </div>

      <div class="report-detail">
        <div class="card-title">报告详情</div>
        <div v-if="currentReport" class="detail-scroll">
          <div class="detail-row"><span class="label">姓名：</span>{{ currentReport.userTrueName || '-' }}</div>
          <div class="detail-row"><span class="label">身份组：</span>{{ currentReport.roleName || '-' }}</div>
          <div class="detail-row"><span class="label">标题：</span>{{ currentReport.title || '-' }}</div>
          <div class="detail-row"><span class="label">类型：</span>演练总结</div>
          <div class="detail-content">{{ currentReport.content || '暂无内容' }}</div>
          <div v-if="currentReport.imageUrls?.length" class="image-wrap">
            <div class="image-title">上传图片</div>
            <div class="image-list">
              <el-image
                v-for="(url, idx) in currentReport.imageUrls"
                :key="`${url}_${idx}`"
                class="report-image"
                :src="url"
                fit="cover"
                :preview-src-list="currentReport.imageUrls"
                preview-teleported
              />
            </div>
          </div>
        </div>
        <el-empty v-else description="请在左侧点击查看报告" />
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'
import TeacherPageHeader from './TeacherPageHeader.vue'

const { projectId } = useTeacherProject()

const studentReports = ref([])
const currentReport = ref(null)

function resolveUploadUrl(url) {
  const raw = String(url || '').trim()
  if (!raw) return ''
  if (/^https?:\/\//i.test(raw)) return raw
  return `${window.location.origin}${raw.startsWith('/') ? '' : '/'}${raw}`
}
function parseReportImages(extraJson) {
  if (!extraJson) return []
  try {
    const obj = typeof extraJson === 'string' ? JSON.parse(extraJson) : extraJson
    const rows = Array.isArray(obj?.imageUrls) ? obj.imageUrls : []
    return rows.map(resolveUploadUrl).filter(Boolean)
  } catch (e) {
    return []
  }
}

function refresh() {
  if (!projectId.value) return
  loadStudentReports()
}

function loadStudentReports() {
  if (!projectId.value) return
  drillApi.getStudentReports(projectId.value, '', false).then((res) => {
    if (!res.status) return
    studentReports.value = (res.data || []).map(x => ({
      id: x.id ?? x.Id,
      roleName: x.roleName ?? x.RoleName,
      userTrueName: x.userTrueName ?? x.UserTrueName,
      title: x.title ?? x.Title,
      content: x.content ?? x.Content,
      extraJson: x.extraJson ?? x.ExtraJson,
      imageUrls: parseReportImages(x.extraJson ?? x.ExtraJson),
      createDate: x.createDate ?? x.CreateDate,
      reportType: String(x.reportType ?? x.ReportType ?? '').toLowerCase(),
      submitStatus: x.submitStatus ?? x.SubmitStatus ?? 1,
      reviewScore: x.reviewScore ?? x.ReviewScore,
      reviewComment: x.reviewComment ?? x.ReviewComment
    })).sort((a, b) => {
      const r = String(a.roleName || '').localeCompare(String(b.roleName || ''), 'zh-Hans-CN')
      if (r !== 0) return r
      const n = String(a.userTrueName || '').localeCompare(String(b.userTrueName || ''), 'zh-Hans-CN')
      if (n !== 0) return n
      return String(b.createDate || '').localeCompare(String(a.createDate || ''))
    })
    if (!currentReport.value && studentReports.value.length) {
      currentReport.value = studentReports.value[0]
    }
  })
}

function viewReport(row) {
  currentReport.value = { ...row }
}

onMounted(() => {
  if (projectId.value) refresh()
  else ElMessage.warning('请先选择项目')
})
</script>

<style scoped>
.page { height:100%; display:flex; flex-direction:column; gap:12px; }
.body { flex:1; display:grid; grid-template-columns: 0.9fr 1.1fr; gap:12px; min-height:0; }
.report-list, .report-detail {
  background:#fff;
  border:1px solid #e8e8e8;
  border-radius:8px;
  padding:12px;
  min-height:0;
  display:flex;
  flex-direction:column;
}
.card-title {
  font-weight:600;
  color:#303133;
  margin-bottom:10px;
}
.list-scroll, .detail-scroll {
  flex:1;
  overflow:auto;
}
.report-item {
  border:1px solid #ebeef5;
  border-radius:8px;
  padding:10px;
  display:flex;
  align-items:center;
  gap:10px;
  margin-bottom:8px;
}
.report-meta {
  min-width:120px;
}
.report-meta .name { font-weight:600; color:#303133; }
.report-meta .role { font-size:12px; color:#909399; margin-top:2px; }
.report-title {
  flex:1;
  color:#606266;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.detail-row { margin-bottom:8px; color:#303133; }
.detail-row .label { color:#909399; }
.detail-content {
  margin-top:10px;
  border:1px solid #ebeef5;
  border-radius:8px;
  background:#fafafa;
  padding:12px;
  min-height:180px;
  white-space: pre-wrap;
  color:#303133;
}
.image-wrap { margin-top: 12px; }
.image-title { color:#606266; margin-bottom: 8px; }
.image-list { display:flex; flex-wrap: wrap; gap: 8px; }
.report-image {
  width: 120px;
  height: 120px;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  overflow: hidden;
}
</style>
