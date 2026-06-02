/**
 * 项目管理 API（对接后端 /api/Project、/api/ProjectMember）
 * 使用真实接口前请先执行 VOL.WebApi/Scripts/CreateProjectTables.sql 建表
 */
import http from './http'

// 使用真实数据库接口（需后端已启动且已建 Project/ProjectMember 表）
const MOCK = false

// 后端可能返回 PascalCase，统一转为成功标志
function isSuccess (res) {
  if (!res) return false
  const s = res.status ?? res.Status
  return s === true || s === 1
}
function getMessage (res) {
  return res?.message ?? res?.Message ?? ''
}

const mockProjects = [
  { id: 1, name: '应急演练项目A', code: 'PRJ-001', status: 1, statusName: '进行中', createDate: '2024-01-15', remark: '' },
  { id: 2, name: '指挥系统建设项目', code: 'PRJ-002', status: 0, statusName: '未开始', createDate: '2024-02-20', remark: '' },
  { id: 3, name: '培训考核项目', code: 'PRJ-003', status: 2, statusName: '已结束', createDate: '2024-03-10', remark: '' }
]

const mockMembers = (projectId) => [
  { id: 1, projectId, name: '张三', role: '总指挥', phone: '13800138000' },
  { id: 2, projectId, name: '李四', role: '协调员', phone: '13800138001' }
]

function buildFilter (params) {
  const filter = []
  if (params.name) filter.push({ Name: 'Name', Value: params.name, DisplayType: 'like' })
  if (params.code) filter.push({ Name: 'Code', Value: params.code, DisplayType: 'like' })
  if (params.status != null && params.status !== '') filter.push({ Name: 'Status', Value: String(params.status), DisplayType: 'equal' })
  return filter
}

export function getProjectPage (params, loading = true) {
  if (MOCK) {
    return Promise.resolve({
      status: true,
      data: { rows: mockProjects, total: mockProjects.length }
    })
  }
  const body = {
    Page: params.page || 1,
    Rows: params.rows || 10,
    Sort: params.sort || 'Id',
    Order: params.order || 'desc',
    TableName: 'Project',
    Filter: buildFilter(params)
  }
  return http.post('/api/Project/GetPageData', body, loading).then(res => {
    // 后端 GetPageData 直接返回 { rows, total }，无 status，有数据即视为成功
    const list = res?.rows ?? res?.Rows ?? []
    const hasData = Array.isArray(list) || typeof (res?.total ?? res?.Total) === 'number'
    const ok = isSuccess(res) || hasData
    const rows = (list || []).map(r => ({
      id: r.Id ?? r.id,
      name: r.Name ?? r.name,
      code: r.Code ?? r.code,
      status: r.Status ?? r.status,
      statusName: r.StatusName ?? r.statusName,
      createDate: r.CreateDate ?? r.createDate,
      remark: r.Remark ?? r.remark
    }))
    return { status: ok, data: { rows, total: res?.total ?? res?.Total ?? 0 } }
  })
}

export function getProjectById (id, loading = false) {
  if (MOCK) {
    const row = mockProjects.find(p => p.id === parseInt(id, 10))
    return Promise.resolve({ status: true, data: row || {} })
  }
  return http.get(`/api/Project/Get?id=${id}`, {}, loading).then(res => {
    const data = res.data ?? res.Data
    if (!data) return { status: isSuccess(res), data: {} }
    const d = typeof data === 'string' ? JSON.parse(data) : data
    const entity = d.data ?? d
    return {
      status: isSuccess(res),
      data: {
        id: entity.Id ?? entity.id,
        name: entity.Name ?? entity.name,
        code: entity.Code ?? entity.code,
        status: entity.Status ?? entity.status,
        statusName: entity.StatusName ?? entity.statusName,
        createDate: entity.CreateDate ?? entity.createDate,
        remark: entity.Remark ?? entity.remark
      }
    }
  })
}

export function saveProject (data, loading = true) {
  if (MOCK) {
    return Promise.resolve({ status: true, data: { id: data.id || Date.now(), ...data }, message: '保存成功' })
  }
  const mainData = {
    Name: data.name,
    Code: data.code,
    Status: data.status ?? 0,
    Remark: data.remark || ''
  }
  if (data.id) mainData.Id = data.id
  const body = { MainData: mainData, DetailData: [], DelKeys: [] }
  const url = data.id ? '/api/Project/Update' : '/api/Project/Add'
  return http.post(url, body, loading).then(res => ({
    status: isSuccess(res),
    data: res.data ?? res.Data,
    message: getMessage(res)
  })).catch(err => {
    const msg = (typeof err === 'string' ? err : err?.message) || '保存失败，请检查后端是否启动且已执行建表脚本'
    return Promise.reject(msg)
  })
}

export function deleteProject (ids, loading = true) {
  if (MOCK) {
    return Promise.resolve({ status: true, message: '删除成功' })
  }
  const keyArray = Array.isArray(ids) ? ids : [ids]
  return http.post('/api/Project/Del', keyArray, loading).then(res => ({
    status: isSuccess(res),
    message: getMessage(res)
  }))
}

export function getProjectMembers (projectId, loading = false) {
  if (MOCK) {
    return Promise.resolve({ status: true, data: mockMembers(projectId) })
  }
  return http.get(`/api/Drill/Members?projectId=${projectId}`, {}, loading).then(res => {
    const raw = res.data ?? res.Data ?? []
    const list = (Array.isArray(raw) ? raw : []).map(r => ({
      id: r.Id ?? r.id,
      projectId: r.ProjectId ?? r.projectId,
      name: r.UserTrueName ?? r.userTrueName ?? r.UserName ?? r.userName,
      role: r.RoleName ?? r.roleName,
      phone: r.Contact ?? r.contact
    }))
    return { status: isSuccess(res), data: list }
  })
}

export function saveProjectMember (data, loading = true) {
  if (MOCK) {
    return Promise.resolve({ status: true, data: { id: data.id || Date.now(), ...data }, message: '保存成功' })
  }
  const mainData = {
    ProjectId: data.projectId,
    Name: data.name,
    Role: data.role || '',
    Phone: data.phone || ''
  }
  if (data.id) mainData.Id = data.id
  const body = { MainData: mainData, DetailData: [], DelKeys: [] }
  const url = data.id ? '/api/ProjectMember/Update' : '/api/ProjectMember/Add'
  return http.post(url, body, loading).then(res => ({
    status: isSuccess(res),
    data: res.data ?? res.Data,
    message: getMessage(res)
  }))
}

export function deleteProjectMember (ids, loading = true) {
  if (MOCK) {
    return Promise.resolve({ status: true, message: '删除成功' })
  }
  const keyArray = Array.isArray(ids) ? ids : [ids]
  return http.post('/api/ProjectMember/Del', keyArray, loading).then(res => ({
    status: isSuccess(res),
    message: getMessage(res)
  }))
}

export function exportProject (params, loading = true) {
  if (MOCK) {
    return Promise.resolve({ status: true, message: '导出功能需对接后端' })
  }
  const body = {
    Page: 1,
    Rows: 50000,
    Sort: 'Id',
    Order: 'desc',
    TableName: 'Project',
    Filter: buildFilter(params || {}),
    Export: true
  }
  return http.post('/api/Project/Export', body, loading)
}

export function importProject (file, loading = true) {
  if (MOCK) {
    return Promise.resolve({ status: true, message: '导入功能需对接后端' })
  }
  const form = new FormData()
  form.append('fileInput', file)
  return http.post('/api/Project/Import', form, loading)
}
