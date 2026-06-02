/**
 * 用户相关 API：登录、注册、用户管理（超管）、重置密码
 */
import http from './http'

export function login(payload, loading = true) {
  return http.post('/api/User/login', payload, loading)
}

export function register(payload, loading = true) {
  return http.post('/api/User/register', payload, loading)
}

export function getVerificationCode() {
  return http.get('/api/User/getVierificationCode', {}, false)
}

/** 超管：重置用户密码（明文传入，后端密文存储） */
export function modifyUserPwd(userName, password, loading = true) {
  return http.post(`/api/User/modifyUserPwd?userName=${encodeURIComponent(userName)}&password=${encodeURIComponent(password)}`, {}, loading)
}

/** 用户分页（超管用户管理用） */
export function getUserPage(params, loading = true) {
  const body = {
    Page: params.page || 1,
    Rows: params.rows || 10,
    Sort: params.sort || 'User_Id',
    Order: params.order || 'desc',
    TableName: 'Sys_User',
    Filter: [
      ...(params.userName ? [{ Name: 'UserName', Value: params.userName, DisplayType: 'like' }] : []),
      ...(params.userTrueName ? [{ Name: 'UserTrueName', Value: params.userTrueName, DisplayType: 'like' }] : [])
    ]
  }
  return http.post('/api/Sys_User/GetPageData', body, loading).then(res => {
    const list = res?.rows ?? res?.Rows ?? []
    const total = res?.total ?? res?.Total ?? 0
    const rows = (list || []).map(r => ({
      user_Id: r.User_Id ?? r.user_Id,
      userName: r.UserName ?? r.userName,
      userTrueName: r.UserTrueName ?? r.userTrueName,
      role_Id: r.Role_Id ?? r.role_Id,
      roleName: r.RoleName ?? r.roleName,
      enable: r.Enable ?? r.enable,
      createDate: r.CreateDate ?? r.createDate
    }))
    return { status: true, data: { rows, total } }
  })
}

/** 更新用户（含角色） */
export function updateUser(data, loading = true) {
  const mainData = {
    User_Id: data.user_Id,
    UserName: data.userName,
    UserTrueName: data.userTrueName,
    Role_Id: data.role_Id,
    RoleName: data.roleName,
    Enable: data.enable ?? 1
  }
  const body = { MainData: mainData, DetailData: [], DelKeys: [] }
  return http.post('/api/Sys_User/Update', body, loading)
}

/** 角色列表（用于下拉） */
export function getRoleList(loading = false) {
  const body = {
    Page: 1,
    Rows: 500,
    Sort: 'Role_Id',
    Order: 'asc',
    TableName: 'Sys_Role',
    Filter: []
  }
  return http.post('/api/Sys_Role/GetPageData', body, loading).then(res => {
    const list = res?.rows ?? res?.Rows ?? []
    return (list || []).map(r => ({
      role_Id: r.Role_Id ?? r.role_Id,
      roleName: r.RoleName ?? r.roleName
    })).filter(r => r.role_Id !== 1) // 不显示超管角色给分配
  })
}
