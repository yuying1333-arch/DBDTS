<template>
	<view class="student-register">
		<view class="header">
			<view class="title">学员注册</view>
			<view class="tip">注册后等待管理员审核即可登录</view>
			<view class="link-back" @click="goBack">返回登录</view>
		</view>
		<view class="form-box">
			<view class="form-row">
				<view class="form-item flex1">
					<u--input v-model="form.projectCode" placeholder="项目编号" border="bottom" clearable></u--input>
				</view>
				<view class="form-item flex1">
					<view class="role-selector" @click="showRolePicker = true">
						<text class="role-selector-text" :class="{ placeholder: !form.roleName }">{{ form.roleName || '请选择角色' }}</text>
						<text class="role-selector-arrow">▼</text>
					</view>
				</view>
			</view>
			<view class="form-row">
				<view class="form-item flex1">
					<u--input v-model="form.userName" placeholder="系统用户名" border="bottom" clearable></u--input>
				</view>
				<view class="form-item flex1">
					<u--input v-model="form.password" type="password" placeholder="系统登录密码" border="bottom" clearable></u--input>
				</view>
			</view>
			<view class="form-row">
				<view class="form-item flex1">
					<u--input v-model="form.userTrueName" placeholder="真实姓名" border="bottom" clearable></u--input>
				</view>
				<view class="form-item flex1">
					<u--input v-model="form.contact" placeholder="联系方式" border="bottom" clearable></u--input>
				</view>
			</view>
			<view class="btn-row">
				<u-button type="primary" text="提交注册" :loading="loading" @click="onSave" :customStyle="{flex:1,height:'88rpx'}"></u-button>
				<u-button text="取消" @click="onCancel" :customStyle="{flex:1,height:'88rpx'}"></u-button>
			</view>
		</view>
		<view class="network-section" v-if="showNetwork">
			<view class="network-label">网络配置</view>
			<view class="form-item"><u--input v-model="serverIp" placeholder="服务器IP地址" border="bottom"></u--input></view>
			<view class="form-item"><u--input v-model="serverPort" placeholder="服务器端口号" border="bottom"></u--input></view>
		</view>
		<u-picker :show="showRolePicker" :columns="roleColumns" keyName="text" @confirm="onRoleConfirm" @cancel="showRolePicker=false"></u-picker>
	</view>
</template>

<script>
	import { studentRegister, getStudentRoles } from '@/util/studentApi.js'
	export default {
		data() {
			return {
				loading: false,
				showRolePicker: false,
				showNetwork: false,
				roles: [],
				roleColumns: [],
				serverIp: '',
				serverPort: '',
				form: {
					projectCode: '', roleName: '', userName: '', password: '',
					userTrueName: '', contact: ''
				}
			}
		},
		onLoad() {
			getStudentRoles(false).then(res => {
				if (res.status && res.data) {
					this.roles = res.data
					this.roleColumns = [this.roles.map(r => ({ text: r, value: r }))]
				} else {
					this.roles = ['总指挥', '110', '政府办公室', '群众', '观察者']
					this.roleColumns = [this.roles.map(r => ({ text: r, value: r }))]
				}
			}).catch(() => {
				this.roles = ['总指挥', '110', '政府办公室', '群众', '观察者']
				this.roleColumns = [this.roles.map(r => ({ text: r, value: r }))]
			})
		},
		methods: {
			onRoleConfirm(e) {
				const item = e.value?.[0] || e.value
				this.form.roleName = item?.text || item || ''
				this.showRolePicker = false
			},
			onSave() {
				if (!this.form.projectCode?.trim()) return this.$toast('请输入项目编号')
				if (!this.form.roleName?.trim()) return this.$toast('请选择角色')
				if (!this.form.userName?.trim()) return this.$toast('请输入系统用户名')
				if (!this.form.password?.trim()) return this.$toast('请输入密码')
				if (this.form.password.length < 6) return this.$toast('密码不能少于6位')
				if (!this.form.userTrueName?.trim()) return this.$toast('请输入真实姓名')
				this.loading = true
				studentRegister({
					projectCode: this.form.projectCode.trim(),
					roleName: this.form.roleName.trim(),
					userName: this.form.userName.trim(),
					password: this.form.password.trim(),
					userTrueName: this.form.userTrueName.trim(),
					contact: this.form.contact?.trim()
				}, '注册中...').then(res => {
					this.loading = false
					if (!res.status) return this.$toast(res.message || '注册失败')
					this.$toast('注册成功，请等待管理员审核')
					setTimeout(() => uni.navigateBack(), 1500)
				}).catch(() => { this.loading = false })
			},
			onCancel() {
				uni.navigateBack()
			},
			goBack() {
				uni.navigateBack()
			}
		}
	}
</script>

<style lang="less" scoped>
	.student-register {
		min-height: 100vh;
		background: linear-gradient(160deg, #096dd9 0%, #1890ff 40%, #69c0ff 100%);
		padding: 40rpx;
		padding-top: calc(40rpx + env(safe-area-inset-top));
		.header { margin-bottom: 32rpx; position: relative; }
		.title { color: #fff; font-size: 40rpx; font-weight: bold; margin-bottom: 12rpx; }
		.tip { color: rgba(255,255,255,0.9); font-size: 24rpx; }
		.link-back { position: absolute; right: 0; top: 0; color: #fff; font-size: 28rpx; }
		.form-box {
			background: #fff;
			border-radius: 20rpx;
			padding: 36rpx 32rpx;
			box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08);
			.form-row { display: flex; gap: 24rpx; }
			.form-item { margin-bottom: 28rpx; }
			.flex1 { flex: 1; min-width: 0; }
			.role-selector {
				height: 78rpx;
				border-bottom: 1rpx solid #dcdfe6;
				display: flex;
				align-items: center;
				justify-content: space-between;
				gap: 12rpx;
			}
			.role-selector-text {
				flex: 1;
				font-size: 30rpx;
				color: #303133;
				overflow: hidden;
				text-overflow: ellipsis;
				white-space: nowrap;
			}
			.role-selector-text.placeholder { color: #c0c4cc; }
			.role-selector-arrow { color: #909399; font-size: 22rpx; }
			.btn-row { display: flex; gap: 24rpx; margin-top: 48rpx; }
		}
		.network-section { margin-top: 32rpx; background: rgba(255,255,255,0.2); border-radius: 16rpx; padding: 24rpx; }
		.network-label { color: #fff; font-size: 28rpx; margin-bottom: 16rpx; }
	}
</style>
