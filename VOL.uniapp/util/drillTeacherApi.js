import http from './http.js'

export function getProjects(loading = false) {
	return http.get('api/Drill/Projects', {}, loading)
}

export function getState(projectId, loading = false) {
	return http.get(`api/Drill/State?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function start(projectId, loading = '启动中...') {
	return http.post(`api/Drill/State/Start?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function pause(projectId, loading = '暂停中...') {
	return http.post(`api/Drill/State/Pause?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function resume(projectId, loading = '恢复中...') {
	return http.post(`api/Drill/State/Resume?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function end(projectId, loading = '结束中...') {
	return http.post(`api/Drill/State/End?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function nextNode(projectId, loading = '切换中...') {
	return http.post(`api/Drill/State/NextNode?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function setNode(projectId, nodeCode, loading = '切换中...') {
	return http.post(`api/Drill/State/SetNode?projectId=${encodeURIComponent(projectId)}&nodeCode=${encodeURIComponent(nodeCode)}`, {}, loading)
}

export function setStage(projectId, stage, loading = false) {
	return http.post(`api/Drill/State/SetStage?projectId=${encodeURIComponent(projectId)}&stage=${encodeURIComponent(stage)}`, {}, loading)
}

export function saveSettings(projectId, settings, loading = '保存中...') {
	return http.post(`api/Drill/State/Settings?projectId=${encodeURIComponent(projectId)}`, settings || {}, loading)
}

export function getFlowNodes(projectId, loading = false) {
	return http.get(`api/Drill/Flow/Nodes?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function getMembers(projectId, auditStatus = null, loading = false) {
	let url = `api/Drill/Members?projectId=${encodeURIComponent(projectId)}`
	if (auditStatus != null && auditStatus !== '') {
		url += `&auditStatus=${encodeURIComponent(auditStatus)}`
	}
	return http.get(url, {}, loading)
}

export function getMessages(projectId, channel = 'discussion', loading = false, nodeCode = '') {
	const q = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
	return http.get(`api/Drill/Messages?projectId=${encodeURIComponent(projectId)}&channel=${encodeURIComponent(channel)}${q}`, {}, loading)
}

export function sendMessage(projectId, channel = 'discussion', content = '', options = {}, loading = false) {
	return http.post(`api/Drill/Messages/Send?projectId=${encodeURIComponent(projectId)}&channel=${encodeURIComponent(channel)}`, {
		content,
		parentMessageId: options.parentMessageId || null,
		nodeCode: options.nodeCode || '',
		selfInitiated: options.selfInitiated === true
	}, loading)
}

export function getFlowActions(projectId, nodeCode = '', loading = false) {
	const q = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
	return http.get(`api/Drill/Flow/Actions?projectId=${encodeURIComponent(projectId)}${q}`, {}, loading)
}
