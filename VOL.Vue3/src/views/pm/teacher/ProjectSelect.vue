<template>
  <div class="page">
    <el-card class="card" shadow="never">
      <template #header>
        <div class="hd">选择演练项目</div>
      </template>
      <div class="bd">
        <el-form label-width="90px">
          <el-form-item label="项目">
            <el-select
              v-model="selectedId"
              filterable
              style="width: 420px"
              placeholder="请选择项目"
            >
              <el-option
                v-for="p in projects"
                :key="p.id"
                :label="`${p.code} - ${p.name}`"
                :value="p.id"
              />
            </el-select>
          </el-form-item>
        </el-form>
        <div class="actions">
          <el-button @click="load">刷新项目</el-button>
          <el-button type="primary" :disabled="!selectedId" @click="confirmSelect">进入事故场景</el-button>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'

const router = useRouter()
const route = useRoute()
const { projectId, setProjectId } = useTeacherProject()

const projects = ref([])
const selectedId = ref(projectId.value || 0)

function sortBySequence(list) {
  return [...list].sort((a, b) => {
    const an = parseInt(String(a.code || '').replace(/[^\d]/g, ''), 10)
    const bn = parseInt(String(b.code || '').replace(/[^\d]/g, ''), 10)
    const aNum = Number.isNaN(an) ? Number.MAX_SAFE_INTEGER : an
    const bNum = Number.isNaN(bn) ? Number.MAX_SAFE_INTEGER : bn
    if (aNum !== bNum) return aNum - bNum
    return (a.id || 0) - (b.id || 0)
  })
}

function pickDefaultProject(list) {
  if (!list.length) return 0
  if (projectId.value > 0 && list.some(p => p.id === projectId.value)) return projectId.value
  return list[0].id
}

function load() {
  drillApi.getProjects(true).then((res) => {
    if (!res.status) return
    const mapped = (res.data || []).map(p => ({
      id: p.id ?? p.Id,
      name: p.name ?? p.Name,
      code: p.code ?? p.Code
    }))
    projects.value = sortBySequence(mapped)
    selectedId.value = pickDefaultProject(projects.value)
  })
}

function confirmSelect() {
  const p = projects.value.find(x => x.id === selectedId.value)
  if (!p) return ElMessage.warning('请先选择项目')
  setProjectId(p.id, p)
  const redirect = route.query.redirect ? String(route.query.redirect) : '/teacher/scene'
  router.replace(redirect)
}

onMounted(load)
</script>

<style scoped>
.page { display: flex; justify-content: center; padding-top: 24px; }
.card { width: 680px; border: 1px solid #e8e8e8; }
.hd { font-weight: 600; }
.actions { display: flex; gap: 10px; justify-content: flex-end; }
</style>

