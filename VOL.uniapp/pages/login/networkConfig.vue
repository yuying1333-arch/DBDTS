<template>
	<view class="network-config">
		<view class="header">
			<view class="title">网络配置</view>
			<view class="link-back" @click="goBack">返回</view>
		</view>
		<view class="form-box">
			<view class="form-item">
				<u--input v-model="serverIp" placeholder="服务器IP（如 192.168.1.100）" border="bottom" clearable></u--input>
			</view>
			<view class="form-item">
				<u--input v-model="serverPort" placeholder="端口号（如 9991）" border="bottom" clearable></u--input>
			</view>
			<u-button type="primary" text="保存" @click="onSave" :customStyle="{width:'100%',marginTop:'48rpx',height:'88rpx'}"></u-button>
		</view>
	</view>
</template>

<script>
	export default {
		data() {
			return { serverIp: '', serverPort: '' }
		},
		onBackPress() {
			this.goBack()
			return true
		},
		onLoad() {
			const cfg = uni.getStorageSync('drill_server_config') || {}
			this.serverIp = cfg.ip || ''
			this.serverPort = cfg.port || ''
		},
		methods: {
			goBack() {
				uni.switchTab({ url: '/pages/login/login' })
			},
			onSave() {
				const ip = (this.serverIp || '').trim()
				const port = (this.serverPort || '').trim()
				uni.setStorageSync('drill_server_config', { ip, port })
				this.$toast('已保存')
				setTimeout(() => this.goBack(), 1000)
			}
		}
	}
</script>

<style lang="less" scoped>
	.network-config {
		min-height: 100vh;
		background: linear-gradient(160deg, #096dd9 0%, #1890ff 40%, #69c0ff 100%);
		padding: 40rpx;
		padding-top: calc(40rpx + env(safe-area-inset-top));
		.header { margin-bottom: 40rpx; position: relative; }
		.title { color: #fff; font-size: 40rpx; font-weight: bold; margin-bottom: 12rpx; }
		.link-back { position: absolute; right: 0; top: 0; color: #fff; font-size: 28rpx; }
		.form-box {
			background: #fff;
			border-radius: 20rpx;
			padding: 40rpx 36rpx;
			box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08);
		}
		.form-item { margin-bottom: 32rpx; }
	}
</style>
