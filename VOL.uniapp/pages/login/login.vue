<template>
	<view class="login-page">
		<view class="header">
			<view class="title">统一登录</view>
		</view>
		<view class="form-box">
			<view class="role-tabs">
				<view class="tab" :class="{ active: loginType==='teacher' }" @click="switchType('teacher')">教师登录</view>
				<view class="tab" :class="{ active: loginType==='student' }" @click="switchType('student')">学员登录</view>
			</view>
			<view class="form-item" v-if="loginType==='student'">
				<u--input v-model="form.projectCode" placeholder="项目编号" border="bottom" clearable></u--input>
			</view>
			<view class="form-item">
				<u--input v-model="form.userName" placeholder="用户名" border="bottom" clearable></u--input>
			</view>
			<view class="form-item">
				<u--input v-model="form.password" type="password" placeholder="密码" border="bottom" clearable></u--input>
			</view>
			<view class="btn-login">
				<u-button type="primary" text="登录" :loading="loading" @click="onLogin" :customStyle="{width:'100%',height:'88rpx',fontSize:'32rpx'}"></u-button>
			</view>
			<view class="links">
				<view class="link" v-if="loginType==='student'" @click="goRegister">没有账号？去注册</view>
				<view class="link network-link" @click="openNetworkConfig">网络配置</view>
			</view>
			<view class="server-tip">当前服务：{{ currentServer }}</view>
		</view>
	</view>
</template>

<script>
import { studentLogin } from '@/util/studentApi.js'
import { getBaseUrl } from '@/util/http.js'
export default {
	data() {
		return {
			loginType: 'teacher',
			loading: false,
			currentServer: '',
			form: {
				projectCode: '',
				userName: '',
				password: ''
			}
		}
	},
	onShow() {
		this.currentServer = getBaseUrl()
	},
	methods: {
		resetForm() {
			this.form = {
				projectCode: '',
				userName: '',
				password: ''
			}
		},
		switchType(type) {
			if (this.loginType === type) return
			this.loginType = type
			this.resetForm()
		},
		parseRole(user) {
			const roleName = String(user?.roleName || user?.RoleName || '')
			const roleId = Number(user?.role_Id ?? user?.roleId ?? user?.Role_Id ?? 0)
			const isTeacher = roleName.includes('教师') || roleId === 5
			const isStudent = roleName.includes('学生') || roleId === 3
			return { roleName, roleId, isTeacher, isStudent }
		},
		onLogin() {
			if (!this.form.userName?.trim()) return this.$toast('请输入用户名')
			if (!this.form.password?.trim()) return this.$toast('请输入密码')
			this.loading = true
			const done = () => { this.loading = false }
			if (this.loginType === 'student') {
				if (!this.form.projectCode?.trim()) {
					done()
					return this.$toast('请输入项目编号')
				}
				return studentLogin({
					projectCode: this.form.projectCode.trim(),
					userName: this.form.userName.trim(),
					password: this.form.password.trim()
				}, '登录中...').then((result) => {
					done()
					if (!result.status || !result.data) return this.$toast(result.message || '登录失败')
					this.$store.commit('setUserInfo', {
						token: result.data.token,
						userName: result.data.userName,
						roleName: result.data.roleName,
						projectId: result.data.projectId,
						projectName: result.data.projectName,
						projectCode: result.data.projectCode
					})
					uni.redirectTo({ url: '/pages/student/initial/initial' })
				}).catch(() => {
					done()
					this.$toast('登录失败')
				})
			}
			const payload = {
				userName: this.form.userName.trim(),
				password: this.form.password.trim()
			}
			this.http.post('api/User/loginApp', payload, '登录中...').then((result) => {
				done()
				if (!result.status || !result.data) {
					return this.$toast(result.message || '登录失败')
				}
				const data = result.data || {}
				const role = this.parseRole(data)
				if (!role.isTeacher) {
					this.$store.commit('clearUserInfo')
					return this.$toast('当前账号不是教师身份')
				}
				this.$store.commit('setUserInfo', data)
				uni.redirectTo({ url: '/pages/teacher/control/control' })
			}).catch(() => {
				done()
				this.$toast(`登录失败，请检查网络配置(${this.currentServer})`)
			})
		},
		goRegister() {
			if (this.loginType !== 'student') return
			uni.navigateTo({ url: '/pages/register/register' })
		},
		openNetworkConfig() {
			uni.switchTab({ url: '/pages/login/networkConfig' })
		}
	}
}
</script>

<style lang="less" scoped>
.login-page {
	min-height: 100vh;
	background: linear-gradient(160deg, #096dd9 0%, #1890ff 40%, #69c0ff 100%);
	padding: 60rpx 40rpx 40rpx;
	padding-top: calc(60rpx + env(safe-area-inset-top));
	.header { margin-bottom: 48rpx; }
	.title { color: #fff; font-size: 44rpx; font-weight: bold; margin-bottom: 12rpx; letter-spacing: 2rpx; }
	.form-box {
		background: #fff;
		border-radius: 20rpx;
		padding: 40rpx 36rpx;
		box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08);
		.role-tabs {
			display: flex;
			gap: 12rpx;
			margin-bottom: 22rpx;
		}
		.tab {
			flex: 1;
			text-align: center;
			padding: 12rpx 0;
			border-radius: 10rpx;
			background: #f2f3f5;
			color: #4e5969;
			font-size: 26rpx;
		}
		.tab.active {
			background: #1890ff;
			color: #fff;
		}
		.form-item { margin-bottom: 36rpx; padding: 8rpx 0; }
		.btn-login { margin-top: 48rpx; }
		.links { display: flex; justify-content: space-between; margin-top: 32rpx; }
		.link { color: #1890ff; font-size: 28rpx; }
		.network-link { margin-left: auto; }
		.server-tip {
			margin-top: 18rpx;
			color: #86909c;
			font-size: 22rpx;
			word-break: break-all;
		}
	}
}
</style>
