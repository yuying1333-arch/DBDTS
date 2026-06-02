<template>

	<u-tabbar @change="onChange" activeColor="rgb(0 161 252)" :zIndex="99999" :value="index" :placeholder="false"
		:fixed="true">
		<u-tabbar-item text="学员入口" icon="account" :name="0"></u-tabbar-item>
		<u-tabbar-item text="网络配置" icon="setting" :name="1"></u-tabbar-item>
	</u-tabbar>
</template>

<script>
	export default {
		props: {
			index: {
				type: Number,
				default: 0
			},
		},
		name: "vol-tabbar",
		data() {
			return {
				list: [
					"/pages/login/login",
					"/pages/login/networkConfig"
				]
			};
		},
		methods: {
			onChange(nameIndex) {
				uni.switchTab({
					url: this.list[nameIndex]
				})
			}
		}
	}
</script>

<style>

</style>
