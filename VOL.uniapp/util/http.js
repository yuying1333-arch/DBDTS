import store from '../store/index.js'

// 开发模式配置：true=使用本地服务器，false=使用生产服务器
const DEV_MODE = true;

var defaultIp;
if (DEV_MODE) {
	defaultIp = "http://127.0.0.1:9991/";
} else {
	defaultIp = "https://api.volcore.xyz/";
}
function getBaseUrl() {
	try {
		const cfg = uni.getStorageSync('drill_server_config') || {};
		let ip = (cfg.ip || '').trim();
		let port = (cfg.port || '').toString().trim();

		// 允许用户直接填完整 baseUrl（如 http://192.168.1.10:9991/）
		if (/^https?:\/\//i.test(ip)) {
			return ip.endsWith('/') ? ip : ip + '/';
		}

		// 清洗端口：去掉前导冒号、非数字字符
		port = port.replace(/^:/, '').replace(/[^\d]/g, '');

		// 清洗 IP：去掉末尾斜杠/协议片段（用户可能误填 http://）
		ip = ip.replace(/^https?:\/\//i, '').replace(/\/+$/g, '');

		if (ip && port) return `http://${ip}:${port}/`;
		if (ip) return ip.endsWith('/') ? ip : ip + '/';
	} catch (e) {}
	return defaultIp;
}
var ipAddress = defaultIp;
let redirectingToLogin = false;
function forceToLogin() {
	if (redirectingToLogin) return;
	redirectingToLogin = true;
	try {
		store.commit('clearUserInfo');
	} catch (e) {}
	uni.reLaunch({
		url: "/pages/login/login",
		complete: () => {
			setTimeout(() => {
				redirectingToLogin = false;
			}, 800);
		}
	});
}
function post(url, data, loading) {
	return request(url, 'POST', data, loading);
}

async function get(url, data, loading) {
	return request(url, 'GET', data, loading);
}

function getToken() {
	return store.getters.getToken();
}

function request(url, method, data, loading) {
	if (loading) {
		uni.showLoading({
			title: typeof loading == 'boolean' ? "正在处理..." : loading
		})
	}
	if (url.startsWith("/")) {
		url = url.substr(1)
	}
	url = getBaseUrl() + url;
	var _header = {};
	var _token = getToken();
	if (_token) {
		_header['Authorization'] = _token;

	}
	return new Promise((reslove, reject) => {
		_header.uapp = '1';
		
		// 请求配置
		const requestConfig = {
			url: url,
			method: method,
			data: data,
			header: _header,
			timeout: 30000, // 设置30秒超时
		};
		
		// 开发环境（本地HTTP）不需要SSL验证，生产环境（HTTPS）需要
		if (DEV_MODE) {
			// 本地开发环境：HTTP协议，不需要SSL验证
			requestConfig.sslVerify = false;
		} else {
			// 生产环境：HTTPS协议，启用SSL验证
			requestConfig.sslVerify = true;
		}
		
		// 添加 success 和 fail 回调
		requestConfig.success = (res) => {

				if (loading) {
					uni.hideLoading();
				}
				if (res.statusCode == 500) {
					console.log(JSON.stringify(res))
					uni.showToast({
						icon: "none",
						title: "服务器内部错误"
					})
					reject({
						statusCode: res.statusCode,
						message: "服务器内部错误",
						url
					});
					return;
				}

				if (res.statusCode == 404) {
					uni.showToast({
						icon: "none",
						title: "未找到请求接口"
					})
					reject({
						statusCode: res.statusCode,
						message: "未找到请求接口",
						url
					});
					return
				}
				if (res.statusCode == 202 || res.statusCode == 401) {
					if (res.data && res.data.message && res.data.code != '401') {
						uni.showToast({
							icon: "none",
							title: res.data.message
						})
						reject({
							statusCode: res.statusCode,
							message: res.data.message,
							url
						});
						return;
					};
					forceToLogin();
					reject({
						statusCode: res.statusCode,
						message: "登录已失效",
						url
					});
					return;
				}

				if (res.header.vol_exp == "1") {
					post('api/User/replaceToken', "POST").then(async result => {
						let userInfo = store.getters.getUserInfo();
						userInfo.token = result.data;
						store.commit('setUserInfo', userInfo);
					});
				}
				reslove(res.data)
		};
		
		requestConfig.fail = (err) => {
				if (loading) {
					uni.hideLoading();
				}
				console.log('请求失败:', JSON.stringify(err))
				
				// 处理401未授权
				if ((err.hasOwnProperty('statusCode') && err.statusCode == 401) ||
					(err.data && err.data.code == 401)) {
					forceToLogin();
					return;
				}
				
				// 处理网络连接错误
				let errorMsg = "网络请求失败";
				if (err.errMsg) {
					if (err.errMsg.indexOf('timeout') !== -1) {
						errorMsg = "请求超时，请检查网络连接";
					} else if (err.errMsg.indexOf('fail') !== -1) {
						if (err.errMsg.indexOf('abort') !== -1) {
							errorMsg = "网络连接中断，请检查网络设置";
						} else if (err.errMsg.indexOf('connect') !== -1) {
							errorMsg = "无法连接到服务器，请检查网络或服务器地址";
						} else {
							errorMsg = "网络请求失败，请稍后重试";
						}
					}
				}
				
				uni.showToast({
					icon: "none",
					title: errorMsg,
					duration: 3000
				})
				
				// 将错误传递给调用者
				reject(err);
		};
		
		// 发起请求
		uni.request(requestConfig);
	})
}



export { getBaseUrl, getToken }

export default {
	get,
	post,
	request,
	ipAddress
}
