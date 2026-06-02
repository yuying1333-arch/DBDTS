/**
 * 学员端 API（小程序）
 * 登录、注册、签到等，无需验证码
 */
import http from './http.js'
import { getBaseUrl, getToken } from './http.js'

export function studentLogin(data, loading) {
	return http.post('api/Drill/Student/Login', data, loading || '登录中...')
}

export function studentRegister(data, loading) {
	return http.post('api/Drill/Student/Register', data, loading || '注册中...')
}

export function getStudentRoles(loading) {
	return http.get('api/Drill/Student/Roles', {}, loading)
}

export function getRoleMe(projectId, loading = false) {
	return http.get(`api/Drill/Student/RoleMe?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function studentSignIn(projectId, loading) {
	return http.post('api/Drill/Student/SignIn?projectId=' + encodeURIComponent(projectId), {}, loading || '签到中...')
}

export function getNodeTasks(projectId, nodeCode = '', loading = false) {
	const q = nodeCode ? `&nodeCode=${encodeURIComponent(nodeCode)}` : ''
	return http.get(`api/Drill/Student/NodeTasks?projectId=${encodeURIComponent(projectId)}${q}`, {}, loading)
}

export function submitNodeTask(projectId, data, loading = '提交中...') {
	return http.post(`api/Drill/Student/NodeTaskSubmit?projectId=${encodeURIComponent(projectId)}`, data, loading)
}

export function submitTaskbook(projectId, data, loading = '提交中...') {
	return http.post(`api/Drill/Student/Taskbook/Submit?projectId=${encodeURIComponent(projectId)}`, data, loading)
}

export function getDialogRoleTargets(projectId, loading = false) {
	return http.get(`api/Drill/Student/Dialog/RoleTargets?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function sendDialogMessage(projectId, data, loading = '发送中...') {
	return http.post(`api/Drill/Student/Dialog/Send?projectId=${encodeURIComponent(projectId)}`, data, loading)
}

export function getDialogInbox(projectId, lastId = 0, loading = false) {
	return http.get(`api/Drill/Student/Dialog/Inbox?projectId=${encodeURIComponent(projectId)}&lastId=${encodeURIComponent(lastId)}`, {}, loading)
}

export function getDrillState(projectId, loading = false) {
	return http.get(`api/Drill/State?projectId=${encodeURIComponent(projectId)}`, {}, loading)
}

export function submitStudentReport(projectId, data, loading = '提交中...') {
	return http.post(`api/Drill/Student/Report/Submit?projectId=${encodeURIComponent(projectId)}`, data, loading)
}

export function getMyReports(projectId, reportType = '', loading = false) {
	const q = reportType ? `&reportType=${encodeURIComponent(reportType)}` : ''
	return http.get(`api/Drill/Student/Report/My?projectId=${encodeURIComponent(projectId)}${q}`, {}, loading)
}

export function uploadStudentReportImage(filePath) {
	if (!filePath) return Promise.reject(new Error('缺少图片文件'))
	const token = getToken()
	if (!token) return Promise.reject(new Error('请先登录'))
	let url = getBaseUrl()
	if (url.endsWith('/')) url = url.slice(0, -1)
	url += '/api/Drill/Student/Report/UploadImage'
	return new Promise((resolve, reject) => {
		uni.uploadFile({
			url,
			filePath,
			name: 'file',
			header: {
				Authorization: token,
				uapp: '1'
			},
			success: (res) => {
				try {
					const body = typeof res.data === 'string' ? JSON.parse(res.data) : res.data
					if (res.statusCode !== 200) {
						reject(new Error(body?.message || body?.Message || '上传失败'))
						return
					}
					if (body?.status === false) {
						reject(new Error(body?.message || '上传失败'))
						return
					}
					resolve(String(body?.data || body?.Data || ''))
				} catch (e) {
					reject(new Error('解析上传响应失败'))
				}
			},
			fail: (err) => reject(err || new Error('上传失败'))
		})
	})
}

// 学员在现场讨论通道发送消息（与教师端讨论页同步）
export function sendDiscussionMessage(projectId, content, options = {}, loading = '提交中...') {
	return http.post(
		`api/Drill/Messages/Send?projectId=${encodeURIComponent(projectId)}&channel=discussion`,
		{
			content,
			nodeCode: options.nodeCode || '',
			selfInitiated: options.selfInitiated === true
		},
		loading
	)
}
