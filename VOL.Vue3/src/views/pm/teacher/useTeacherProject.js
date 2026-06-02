import { computed, ref } from 'vue'

const KEY = 'teacherProjectId'
const META_KEY = 'teacherProjectMeta'
const sharedProjectId = ref(parseInt(localStorage.getItem(KEY) || '0', 10) || 0)
const sharedProjectMeta = ref((() => {
  try {
    const raw = localStorage.getItem(META_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
})())

export function useTeacherProject() {
  const hasProject = computed(() => sharedProjectId.value > 0)
  function setProjectId(id, meta = null) {
    const v = parseInt(id, 10) || 0
    sharedProjectId.value = v
    localStorage.setItem(KEY, String(v))
    if (meta && typeof meta === 'object') {
      sharedProjectMeta.value = {
        id: v,
        name: meta.name || '',
        code: meta.code || ''
      }
      localStorage.setItem(META_KEY, JSON.stringify(sharedProjectMeta.value))
    }
  }
  function clearProjectId() {
    sharedProjectId.value = 0
    sharedProjectMeta.value = null
    localStorage.removeItem(KEY)
    localStorage.removeItem(META_KEY)
  }
  return { projectId: sharedProjectId, projectMeta: sharedProjectMeta, hasProject, setProjectId, clearProjectId }
}

