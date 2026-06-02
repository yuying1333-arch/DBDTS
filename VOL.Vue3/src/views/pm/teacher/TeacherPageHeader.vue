<template>
  <div class="header">
    <div class="left">
      <slot name="left" />
    </div>
    <div class="center-project">{{ projectTitle }}</div>
    <div class="right">
      <slot name="right" />
      <el-button size="small" @click="goProjectSelect">切换项目</el-button>
      <el-button size="small" @click="goScene">返回场景</el-button>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import * as drillApi from '@/api/drill'
import { useTeacherProject } from './useTeacherProject'

const router = useRouter()
const { projectId, projectMeta } = useTeacherProject()

const projectTitle = computed(() => {
  if (projectMeta.value?.name) {
    return `${projectMeta.value.name}（${projectMeta.value.code || projectId.value}）`
  }
  return projectId.value || '未选择'
})

function goProjectSelect() {
  router.push('/teacher/project-select')
}

function goScene() {
  if (projectId.value) drillApi.setStage(projectId.value, 'scene', false)
  router.push('/teacher/scene')
}
</script>

<style scoped>
.header {
  position: relative;
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  padding: 10px 12px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.header .left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
  min-width: 120px;
}
.header .right {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 120px;
  justify-content: flex-end;
}
.center-project {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  max-width: 52%;
  font-size: 16px;
  font-weight: 600;
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  pointer-events: none;
}
</style>
