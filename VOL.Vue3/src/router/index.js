import { createRouter, createWebHashHistory } from 'vue-router'
import store from '../store/index'

function getRoleState(user) {
  const roleId = user?.role_Id ?? user?.roleId ?? 0
  const roleName = String(user?.roleName || user?.RoleName || '')
  const isAdmin = roleId === 1 || roleId === 2 || roleName.includes('管理员')
  const isTeacher = roleId === 5 || roleName.includes('教师')
  return { roleId, roleName, isAdmin, isTeacher }
}

function getDefaultRoute() {
  const user = store.getters.getUserInfo?.()
  if (!user) return '/login'
  const { isAdmin, isTeacher } = getRoleState(user)
  if (isAdmin) return '/admin'
  if (isTeacher) return '/teacher/project-select'
  return '/login'
}

const routes = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/pm/Login.vue'),
    meta: { anonymous: true }
  },
  {
    path: '/',
    component: () => import('@/views/pm/MainLayout.vue'),
    redirect: () => getDefaultRoute(),
    children: [
      { path: '', redirect: () => getDefaultRoute() },
      { path: 'home', redirect: () => getDefaultRoute() },
      // 超管
      { path: 'admin', redirect: '/admin/user-manage' },
      { path: 'admin/user-manage', name: 'userManage', component: () => import('@/views/pm/UserManage.vue'), meta: { role: 'admin' } },
      { path: 'admin/project', name: 'adminProject', component: () => import('@/views/pm/ProjectManage.vue'), meta: { role: 'admin' } },
      { path: 'admin/drill-role', name: 'adminDrillRole', component: () => import('@/views/pm/RoleManage.vue'), meta: { role: 'admin' } },
      { path: 'admin/project/edit', name: 'adminProjectAdd', component: () => import('@/views/pm/ProjectEdit.vue'), meta: { role: 'admin' } },
      { path: 'admin/project/edit/:id', name: 'adminProjectEdit', component: () => import('@/views/pm/ProjectEdit.vue'), meta: { role: 'admin' } },
      // 教师
      { path: 'teacher', redirect: '/teacher/project-select' },
      { path: 'teacher/project-select', name: 'teacherProjectSelect', component: () => import('@/views/pm/teacher/ProjectSelect.vue'), meta: { role: 'teacher' } },
      { path: 'teacher/scene', name: 'teacherScene', component: () => import('@/views/pm/teacher/SceneDisplay.vue'), meta: { role: 'teacher' } },
      { path: 'teacher/discussion', name: 'teacherDiscussion', component: () => import('@/views/pm/teacher/Discussion.vue'), meta: { role: 'teacher' } },
      { path: 'teacher/report', name: 'teacherReport', component: () => import('@/views/pm/teacher/SummaryReport.vue'), meta: { role: 'teacher' } },
      { path: 'teacher/review', name: 'teacherReview', component: () => import('@/views/pm/teacher/Review.vue'), meta: { role: 'teacher' } },
      { path: 'teacher/students', name: 'teacherStudents', component: () => import('@/views/pm/teacher/Students.vue'), meta: { role: 'teacher' } },
    ]
  },
  {
    path: '/404',
    name: '404',
    component: () => import('@/components/redirect/404'),
    meta: { anonymous: true }
  }
]

const router = createRouter({
  history: createWebHashHistory(),
  routes
})

router.beforeEach((to, from, next) => {
  if (to.matched.length === 0) return next({ path: '/404' })
  store.dispatch('onLoading', true)
  if (to.meta.anonymous) return next()
  if (!store.getters.isLogin()) return next({ path: '/login', query: { redirect: to.fullPath } })
  if (to.path === '/') return next(getDefaultRoute())
  const user = store.getters.getUserInfo?.()
  const { isAdmin, isTeacher } = getRoleState(user)
  if (!isAdmin && !isTeacher) {
    store.commit('clearUserInfo')
    return next({ path: '/login' })
  }
  if (!isAdmin && to.path.startsWith('/teacher/')) {
    const pid = parseInt(localStorage.getItem('teacherProjectId') || '0', 10) || 0
    const allowWithoutProject = ['/teacher/project-select']
    if (!pid && !allowWithoutProject.includes(to.path)) {
      return next({ path: '/teacher/project-select', query: { redirect: to.fullPath } })
    }
  }
  next()
})

router.afterEach(() => {
  store.dispatch('onLoading', false)
})

router.onError((err) => {
  console.error(err)
  if (process.env.NODE_ENV === 'development') alert(err?.message)
  window.location.href = '/'
})

export default router
