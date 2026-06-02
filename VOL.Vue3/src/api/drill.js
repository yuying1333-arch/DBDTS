import http from './http'

export function getState(projectId, loading = false) {
  return http.get(`/api/Drill/State?projectId=${projectId}`, {}, loading)
}

export function start(projectId, loading = true) {
  return http.post(`/api/Drill/State/Start?projectId=${projectId}`, {}, loading)
}

export function pause(projectId, loading = true) {
  return http.post(`/api/Drill/State/Pause?projectId=${projectId}`, {}, loading)
}

export function resume(projectId, loading = true) {
  return http.post(`/api/Drill/State/Resume?projectId=${projectId}`, {}, loading)
}

export function end(projectId, loading = true) {
  return http.post(`/api/Drill/State/End?projectId=${projectId}`, {}, loading)
}

export function nextNode(projectId, loading = true) {
  return http.post(`/api/Drill/State/NextNode?projectId=${projectId}`, {}, loading)
}

export function setStage(projectId, stage, loading = false) {
  return http.post(`/api/Drill/State/SetStage?projectId=${projectId}&stage=${encodeURIComponent(stage)}`, {}, loading)
}

export function setNode(projectId, nodeCode, loading = true) {
  return http.post(`/api/Drill/State/SetNode?projectId=${projectId}&nodeCode=${encodeURIComponent(nodeCode)}`, {}, loading)
}

export function saveSettings(projectId, settings, loading = true) {
  return http.post(`/api/Drill/State/Settings?projectId=${projectId}`, settings, loading)
}

export function getEvents(projectId, stage, loading = false) {
  const q = stage ? `&stage=${encodeURIComponent(stage)}` : ''
  return http.get(`/api/Drill/Events?projectId=${projectId}${q}`, {}, loading)
}

export function getStudentReports(projectId, reportType, loading = false) {
  const q = reportType ? `&reportType=${encodeURIComponent(reportType)}` : ''
  return http.get(`/api/Drill/StudentReports?projectId=${projectId}${q}`, {}, loading)
}

export function reviewStudentReport(id, data, loading = true) {
  return http.post(`/api/Drill/StudentReports/Review?id=${id}`, data, loading)
}

export function addEvent(payload, loading = true) {
  return http.post('/api/Drill/Events/Add', payload, loading)
}

export function getMessages(projectId, channel = 'discussion', loading = false, nodeCode = '') {
  const nodeQuery = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
  return http.get(`/api/Drill/Messages?projectId=${projectId}&channel=${encodeURIComponent(channel)}${nodeQuery}`, {}, loading)
}

export function sendMessage(projectId, channel, content, loading = false, options = {}) {
  const payload = {
    content,
    parentMessageId: options.parentMessageId || null,
    nodeCode: options.nodeCode || ''
  }
  return http.post(`/api/Drill/Messages/Send?projectId=${projectId}&channel=${encodeURIComponent(channel)}`, payload, loading)
}

export function getRecovery(projectId, loading = false) {
  return http.get(`/api/Drill/Recovery?projectId=${projectId}`, {}, loading)
}

export function updateRecovery(item, loading = true) {
  return http.post('/api/Drill/Recovery/Update', item, loading)
}

export function getStudents(loading = false) {
  return http.get('/api/Drill/Students', {}, loading)
}

export function getMembers(projectId, auditStatus, loading = false) {
  let url = `/api/Drill/Members?projectId=${projectId}`
  if (auditStatus != null && auditStatus !== '') url += `&auditStatus=${auditStatus}`
  return http.get(url, {}, loading)
}

export function addMember(data, loading = true) {
  return http.post('/api/Drill/Members/Add', data, loading)
}

export function exportSimpleMembers(projectId, loading = false) {
  return http.get(`/api/Drill/Members/ExportSimple?projectId=${projectId}`, {}, loading)
}

export function importSimpleMembers(projectId, items, loading = true) {
  return http.post(`/api/Drill/Members/ImportSimple?projectId=${projectId}`, { items }, loading)
}

export function approveMember(id, loading = true) {
  return http.post(`/api/Drill/Members/Approve?id=${id}`, {}, loading)
}

export function updateMemberRole(id, roleName, loading = true) {
  return http.post(`/api/Drill/Members/UpdateRole?id=${id}`, { roleName }, loading)
}

export function batchUpdateMemberRole(data, loading = true) {
  return http.post('/api/Drill/Members/BatchUpdateRole', data, loading)
}

export function getProjectResources(projectId, loading = false) {
  return http.get(`/api/Drill/ProjectResources?projectId=${projectId}`, {}, loading)
}

export function saveProjectResource(projectId, data, loading = true) {
  return http.post(`/api/Drill/ProjectResources/Save?projectId=${projectId}`, data, loading)
}

export function saveProjectResources(projectId, items, loading = true) {
  return http.post(`/api/Drill/ProjectResources/SaveBatch?projectId=${projectId}`, { items }, loading)
}

export function uploadProjectVideo(file, loading = true) {
  const form = new FormData()
  form.append('file', file)
  return http.post('/api/Drill/ProjectResources/UploadVideo', form, loading)
}

export function getProjects(loading = false) {
  return http.get('/api/Drill/Projects', {}, loading)
}

export function getFlowNodes(projectId, loading = false) {
  return http.get(`/api/Drill/Flow/Nodes?projectId=${projectId}`, {}, loading)
}

export function saveFlowNodes(projectId, items, loading = true) {
  return http.post(`/api/Drill/Flow/Nodes/Save?projectId=${projectId}`, { items }, loading)
}

export function saveNodeVideoTimes(projectId, items, loading = true) {
  return http.post(`/api/Drill/Flow/NodeVideoTimes?projectId=${projectId}`, { items }, loading)
}

export function getFlowAssignments(projectId, nodeCode, loading = false) {
  const q = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
  return http.get(`/api/Drill/Flow/Assignments?projectId=${projectId}${q}`, {}, loading)
}

export function getFlowProgress(projectId, nodeCode, loading = false) {
  const q = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
  return http.get(`/api/Drill/Flow/Progress?projectId=${projectId}${q}`, {}, loading)
}

export function getFlowActions(projectId, nodeCode, loading = false) {
  const q = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
  return http.get(`/api/Drill/Flow/Actions?projectId=${projectId}${q}`, {}, loading)
}

export function submitFlowAction(payload, loading = true) {
  return http.post('/api/Drill/Flow/Action/Submit', payload, loading)
}

// -----------------------
// 管理员：演练全局角色
// -----------------------
export function getDrillRoles(loading = false) {
  return http.get('/api/Drill/Roles', {}, loading)
}

export function addDrillRole(data, loading = true) {
  return http.post('/api/Drill/Roles/Add', data, loading)
}

export function updateDrillRole(id, data, loading = true) {
  return http.post(`/api/Drill/Roles/Update?id=${id}`, data, loading)
}

export function deleteDrillRole(id, loading = true) {
  return http.post(`/api/Drill/Roles/Delete?id=${id}`, {}, loading)
}

