<template>
  <div class="pm-layout" :class="{ 'scene-fullscreen': isSceneFullscreen, 'admin-theme': isAdmin }" @mousemove="onLayoutMouseMove" @mouseleave="onLayoutMouseLeave">
    <header
      v-show="headerVisible"
      class="pm-header"
      :class="{ overlay: isHoverLayout, hidden: isHoverLayout && !headerVisible }"
      @mouseenter="headerHover = true"
      @mouseleave="headerHover = false"
    >
      <span class="logo">{{ layoutTitle }}</span>
      <div v-if="centerProjectName" class="header-center">{{ centerProjectName }}</div>
      <div class="user">
        <span class="user-name">{{ userName }}</span>
        <el-tag size="small" type="info">{{ roleName }}</el-tag>
        <el-button link type="danger" @click="logout">退出</el-button>
      </div>
    </header>
    <div class="pm-body" :class="{ 'with-overlay': true }">
      <aside
        v-show="sidebarVisible"
        class="pm-sidebar"
        :class="{ overlay: isHoverLayout, hidden: isHoverLayout && !sidebarVisible }"
        @mouseenter="sidebarHover = true"
        @mouseleave="sidebarHover = false"
      >
        <el-menu
          :default-active="activeMenu"
          class="pm-menu"
          background-color="#001529"
          text-color="rgba(255,255,255,0.65)"
          active-text-color="#fff"
          router
        >
          <template v-for="item in menuList" :key="item.path">
            <el-menu-item :index="item.path">
              <span>{{ item.title }}</span>
            </el-menu-item>
          </template>
        </el-menu>
      </aside>
      <main class="pm-main" :class="{ full: isSceneFullscreen, 'scene-header-visible': isSceneFullscreen && isHoverLayout && headerHover }">
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </main>
    </div>
  </div>
</template>

<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import store from '@/store/index'
import { useTeacherProject } from './teacher/useTeacherProject'

const router = useRouter()
const route = useRoute()

const userInfo = computed(() => store.getters.getUserInfo?.() || {})
const headerVisible = ref(false)
const sidebarVisible = ref(false)
const headerHover = ref(false)
const sidebarHover = ref(false)
const userName = computed(() => userInfo.value.userName ?? userInfo.value.UserTrueName ?? '用户')
const roleId = computed(() => userInfo.value.role_Id ?? userInfo.value.roleId ?? 0)
const roleName = computed(() => userInfo.value.roleName ?? userInfo.value.RoleName ?? ((roleId.value === 1 || roleId.value === 2) ? '超级管理员' : '普通用户'))
const isAdmin = computed(() => roleId.value === 1 || roleId.value === 2 || (roleName.value && roleName.value.includes('管理员')))
const isTeacher = computed(() => roleId.value === 5 || (roleName.value && roleName.value.includes('教师')))
const { projectId, projectMeta } = useTeacherProject()
const centerProjectName = computed(() => {
  if (!isTeacher.value) return ''
  if (projectMeta.value?.name) return projectMeta.value.name
  if (projectId.value > 0) return String(projectId.value)
  return ''
})

const layoutTitle = computed(() => {
  if (isAdmin.value) return '管理后台'
  if (isTeacher.value) return '教师工作台'
  return '管理平台'
})

const menuList = computed(() => {
  if (isAdmin.value) {
    return [
      { path: '/admin/user-manage', title: '用户管理' },
      { path: '/admin/project', title: '项目管理' },
      { path: '/admin/drill-role', title: '角色管理' }
    ]
  }
  if (isTeacher.value) {
    return [
      { path: '/teacher/project-select', title: '选择项目' },
      { path: '/teacher/scene', title: '事故场景展示' },
      { path: '/teacher/discussion', title: '现场讨论' },
      { path: '/teacher/review', title: '演练复盘' },
      { path: '/teacher/report', title: '演练总结报告' },
      { path: '/teacher/students', title: '学生身份' }
    ]
  }
  return []
})

const activeMenu = computed(() => {
  const path = route.path
  if (path.startsWith('/admin/project/edit')) return '/admin/project'
  if (path.startsWith('/teacher/')) return path
  return path
})
const isSceneFullscreen = computed(() => route.path === '/teacher/scene')
const isHoverLayout = computed(() => isTeacher.value)
const HEADER_TRIGGER = 15
const SIDEBAR_TRIGGER = 36
const HEADER_SHOW_DELAY = 280
const HEADER_HIDE_DELAY = 120
let lastMouseY = 9999
let headerShowTimer = null
let headerHideTimer = null
let headerTriggerEnterAt = 0

function clearHeaderShowTimer() {
  if (headerShowTimer) {
    clearTimeout(headerShowTimer)
    headerShowTimer = null
  }
}
function clearHeaderHideTimer() {
  if (headerHideTimer) {
    clearTimeout(headerHideTimer)
    headerHideTimer = null
  }
}
function scheduleHeaderHide() {
  clearHeaderShowTimer()
  clearHeaderHideTimer()
  headerHideTimer = setTimeout(() => {
    if (!headerHover.value) headerVisible.value = false
    headerHideTimer = null
  }, HEADER_HIDE_DELAY)
}
function diagnoseHeaderDwellAndShow() {
  if (!headerTriggerEnterAt) headerTriggerEnterAt = Date.now()
  const stayed = Date.now() - headerTriggerEnterAt
  if (stayed >= HEADER_SHOW_DELAY) {
    clearHeaderShowTimer()
    headerVisible.value = true
    return
  }
  if (!headerShowTimer) {
    headerShowTimer = setTimeout(() => {
      headerVisible.value = true
      headerShowTimer = null
    }, HEADER_SHOW_DELAY - stayed)
  }
}

function onLayoutMouseMove(e) {
  if (!isHoverLayout.value) return
  const x = Number(e?.clientX || 0)
  const y = Number(e?.clientY || 0)
  lastMouseY = y
  if (y <= HEADER_TRIGGER) {
    clearHeaderHideTimer()
    diagnoseHeaderDwellAndShow()
  } else if (!headerHover.value) {
    headerTriggerEnterAt = 0
    clearHeaderShowTimer()
    scheduleHeaderHide()
  }

  if (x <= SIDEBAR_TRIGGER) sidebarVisible.value = true
  else if (!sidebarHover.value) sidebarVisible.value = false
}

function onLayoutMouseLeave() {
  if (!isHoverLayout.value) return
  headerTriggerEnterAt = 0
  clearHeaderShowTimer()
  if (!headerHover.value) scheduleHeaderHide()
  if (!sidebarHover.value) sidebarVisible.value = false
}

watch([isHoverLayout, isSceneFullscreen], ([hover, scene]) => {
  if (!hover) {
    headerVisible.value = true
    sidebarVisible.value = true
    return
  }
  headerVisible.value = !scene
  sidebarVisible.value = !scene
}, { immediate: true })

watch(headerHover, (hover) => {
  if (hover) {
    clearHeaderHideTimer()
    clearHeaderShowTimer()
    headerVisible.value = true
    return
  }
  headerTriggerEnterAt = 0
  clearHeaderShowTimer()
  if (isHoverLayout.value && lastMouseY > HEADER_TRIGGER) scheduleHeaderHide()
})

onBeforeUnmount(() => {
  clearHeaderShowTimer()
  clearHeaderHideTimer()
})

function logout() {
  store.commit('clearUserInfo')
  router.replace('/login')
}
</script>

<style scoped>
.pm-layout {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: #0b1220;
}
.pm-header {
  height: 56px;
  padding: 0 20px 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: rgba(8, 16, 30, 0.82);
  box-shadow: 0 6px 24px rgba(0,0,0,0.25);
  backdrop-filter: blur(8px);
  z-index: 10;
  position: relative;
}
.pm-header.overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 1002;
  transition: transform 0.2s ease, opacity 0.2s ease;
}
.pm-header.overlay.hidden {
  transform: translateY(-100%);
  opacity: 0;
}
.pm-header .logo {
  font-size: 18px;
  font-weight: 600;
  color: #eef4ff;
  letter-spacing: 0.5px;
}
.pm-header .user {
  display: flex;
  align-items: center;
  gap: 12px;
}
.header-center {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  max-width: 46%;
  color: #eef4ff;
  font-size: 16px;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.user-name {
  color: #dbe7ff;
  font-size: 14px;
}
.pm-body {
  flex: 1;
  display: flex;
  overflow: hidden;
}
.pm-body.with-overlay {
  position: relative;
}
.pm-sidebar {
  width: 220px;
  flex-shrink: 0;
  background: rgba(8, 16, 30, 0.82);
  overflow-y: auto;
  transition: transform 0.2s ease, opacity 0.2s ease;
  backdrop-filter: blur(8px);
  box-shadow: 4px 0 24px rgba(0,0,0,0.24);
}
.pm-sidebar.overlay {
  position: fixed;
  top: 0;
  bottom: 0;
  left: 0;
  z-index: 1001;
  padding-top: 56px;
}
.pm-sidebar.overlay.hidden {
  transform: translateX(-100%);
  opacity: 0;
}
.pm-menu {
  border-right: none;
  padding: 12px 0;
}
.pm-menu .el-menu-item {
  height: 48px;
  line-height: 48px;
  margin: 2px 8px;
  border-radius: 6px;
}
.pm-menu .el-menu-item:hover {
  background-color: rgba(255,255,255,0.08) !important;
  color: #fff !important;
}
.pm-menu .el-menu-item.is-active {
  background-color: #1890ff !important;
  color: #fff !important;
}
.pm-main {
  flex: 1;
  padding: 20px;
  overflow: auto;
  background: #0f172a;
  transition: padding-top 0.2s ease;
}
.pm-main.full {
  padding: 0;
  background: #000;
}
.pm-main.full.scene-header-visible {
  padding-top: 56px;
}
.pm-main.full.scene-header-visible :deep(.scene-page) {
  height: calc(100vh - 56px);
}
.admin-theme {
  background: #f0f2f5;
}
.admin-theme .pm-header {
  background: #fff;
  box-shadow: 0 1px 4px rgba(0,0,0,0.06);
  backdrop-filter: none;
}
.admin-theme .pm-header .logo {
  color: #1a1a1a;
}
.admin-theme .user-name {
  color: #333;
}
.admin-theme .pm-sidebar {
  background: #001529;
  backdrop-filter: none;
  box-shadow: none;
}
.admin-theme .pm-main {
  background: #f0f2f5;
}
.scene-fullscreen .pm-body {
  height: 100vh;
}
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.15s ease;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
