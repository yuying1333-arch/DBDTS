<template>
  <div class="pm-login">
    <div class="pm-login-card">
      <h1 class="title">系统登录</h1>
      <el-form ref="formRef" :model="form" :rules="rules" label-width="0" class="form">
        <el-form-item prop="userName">
          <el-input v-model="form.userName" placeholder="用户名" size="large" clearable />
        </el-form-item>
        <el-form-item prop="password">
          <el-input v-model="form.password" type="password" placeholder="密码" size="large" show-password clearable @keyup.enter="onLogin" />
        </el-form-item>
        <el-form-item prop="verificationCode">
          <div class="captcha-row">
            <el-input
              v-model="form.verificationCode"
              placeholder="请输入验证码"
              size="large"
              clearable
              maxlength="6"
              @keyup.enter="onLogin"
            />
            <div class="captcha-img" @click="getVerificationCode" title="点击刷新">
              <img v-if="codeImgSrc" :src="codeImgSrc" alt="验证码" />
              <span v-else class="captcha-tip">加载验证码</span>
            </div>
          </div>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" size="large" style="width:100%" :loading="loading" @click="onLogin">登录</el-button>
        </el-form-item>
        <el-form-item class="footer-links">
          <el-button link type="primary" @click="showRegister">注册</el-button>
          <el-button link type="info" @click="onForgetPwd">忘记密码</el-button>
        </el-form-item>
      </el-form>
    </div>

    <!-- 注册弹窗 -->
    <el-dialog v-model="registerVisible" title="用户注册" width="400px" :close-on-click-modal="false" @closed="resetRegisterForm">
      <el-form ref="registerFormRef" :model="registerForm" :rules="registerRules" label-width="80px">
        <el-form-item label="用户名" prop="userName">
          <el-input v-model="registerForm.userName" placeholder="请输入用户名" clearable />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="registerForm.password" type="password" placeholder="不少于6位" show-password clearable />
        </el-form-item>
        <el-form-item label="确认密码" prop="confirmPassword">
          <el-input v-model="registerForm.confirmPassword" type="password" placeholder="再次输入密码" show-password clearable />
        </el-form-item>
        <el-form-item label="姓名" prop="userTrueName">
          <el-input v-model="registerForm.userTrueName" placeholder="请输入姓名" clearable />
        </el-form-item>
        <el-form-item label="验证码" prop="verificationCode">
          <div class="captcha-row">
            <el-input v-model="registerForm.verificationCode" placeholder="验证码" clearable maxlength="6" />
            <div class="captcha-img" @click="getRegisterCode" title="点击刷新">
              <img v-if="registerCodeImg" :src="registerCodeImg" alt="验证码" />
              <span v-else class="captcha-tip">点击加载</span>
            </div>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="registerVisible = false">取消</el-button>
        <el-button type="primary" :loading="registerLoading" @click="onRegister">确定注册</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import store from '@/store/index'
import * as userApi from '@/api/user'

const router = useRouter()
const formRef = ref(null)
const loading = ref(false)
const codeImgSrc = ref('')
const form = reactive({
  userName: '',
  password: '',
  verificationCode: '',
  uuid: ''
})
const rules = {
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  verificationCode: [{ required: true, message: '请输入验证码', trigger: 'blur' }]
}

function getVerificationCode() {
  userApi.getVerificationCode().then((x) => {
    codeImgSrc.value = x.img ? 'data:image/png;base64,' + x.img : ''
    form.uuid = x.uuid != null ? String(x.uuid) : ''
    form.verificationCode = ''
  }).catch(() => {
    ElMessage.error('验证码加载失败')
  })
}

function redirectByRole(data) {
  const roleId = data.role_Id ?? data.roleId ?? 0
  const roleName = String(data.roleName || data.RoleName || '')
  const isAdmin = roleId === 1 || roleId === 2 || roleName.includes('管理员')
  const isTeacher = roleId === 5 || roleName.includes('教师')
  if (isAdmin) return '/admin'
  if (!isTeacher) {
    store.commit('clearUserInfo')
    ElMessage.error('仅管理员和教师可登录Web端')
    getVerificationCode()
    return ''
  }
  localStorage.removeItem('teacherProjectId')
  localStorage.removeItem('teacherProjectMeta')
  return '/teacher/project-select'
}

function onLogin() {
  formRef.value?.validate((valid) => {
    if (!valid) return
    if (!form.uuid) {
      ElMessage.warning('请等待验证码加载完成')
      getVerificationCode()
      return
    }
    loading.value = true
    const payload = {
      userName: form.userName,
      password: form.password,
      verificationCode: form.verificationCode,
      uuid: form.uuid
    }
    userApi.login(payload, '正在登录…').then((res) => {
      if (res.status && res.data) {
        const targetPath = redirectByRole(res.data)
        if (targetPath) {
          store.commit('setUserInfo', res.data)
          ElMessage.success('登录成功')
          router.replace(targetPath)
        }
      } else {
        ElMessage.error(res.message || '登录失败')
        getVerificationCode()
      }
    }).catch((e) => {
      ElMessage.error(e?.message || '登录失败')
      getVerificationCode()
    }).finally(() => {
      loading.value = false
    })
  })
}

const registerVisible = ref(false)
const registerFormRef = ref(null)
const registerLoading = ref(false)
const registerCodeImg = ref('')
const registerForm = reactive({
  userName: '',
  password: '',
  confirmPassword: '',
  userTrueName: '',
  verificationCode: '',
  uuid: ''
})
const validateConfirm = (rule, value, callback) => {
  if (value !== registerForm.password) callback(new Error('两次输入的密码不一致'))
  else callback()
}
const registerRules = {
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }, { min: 6, message: '密码不少于6位', trigger: 'blur' }],
  confirmPassword: [{ required: true, message: '请确认密码', trigger: 'blur' }, { validator: validateConfirm, trigger: 'blur' }],
  userTrueName: [{ required: true, message: '请输入姓名', trigger: 'blur' }],
  verificationCode: [{ required: true, message: '请输入验证码', trigger: 'blur' }]
}

function showRegister() {
  registerVisible.value = true
  getRegisterCode()
}
function getRegisterCode() {
  userApi.getVerificationCode().then((x) => {
    registerCodeImg.value = x.img ? 'data:image/png;base64,' + x.img : ''
    registerForm.uuid = x.uuid != null ? String(x.uuid) : ''
    registerForm.verificationCode = ''
  }).catch(() => ElMessage.error('验证码加载失败'))
}
function resetRegisterForm() {
  registerForm.userName = ''
  registerForm.password = ''
  registerForm.confirmPassword = ''
  registerForm.userTrueName = ''
  registerForm.verificationCode = ''
  registerForm.uuid = ''
  registerFormRef.value?.resetFields()
}
function onRegister() {
  registerFormRef.value?.validate((valid) => {
    if (!valid) return
    if (!registerForm.uuid) {
      ElMessage.warning('请先获取验证码')
      getRegisterCode()
      return
    }
    registerLoading.value = true
    userApi.register({
      userName: registerForm.userName,
      password: registerForm.password,
      confirmPassword: registerForm.confirmPassword,
      userTrueName: registerForm.userTrueName,
      verificationCode: registerForm.verificationCode,
      uuid: registerForm.uuid
    }, true).then((res) => {
      if (res.status) {
        ElMessage.success(res.message || '注册成功，请登录')
        registerVisible.value = false
      } else {
        ElMessage.error(res.message || '注册失败')
        getRegisterCode()
      }
    }).catch((e) => {
      ElMessage.error(e?.message || '注册失败')
      getRegisterCode()
    }).finally(() => {
      registerLoading.value = false
    })
  })
}

function onForgetPwd() {
  ElMessage.info('请联系管理员重置密码')
}

onMounted(() => {
  store.commit('clearUserInfo', '')
  getVerificationCode()
})
</script>

<style scoped>
.pm-login {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1e3a5f 0%, #2d5a87 100%);
}
.pm-login-card {
  width: 360px;
  padding: 40px;
  background: #fff;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0,0,0,0.2);
}
.pm-login .title {
  margin: 0 0 28px 0;
  font-size: 22px;
  text-align: center;
  color: #1a1a1a;
}
.footer-links {
  margin-bottom: 0;
}
.footer-links :deep(.el-form-item__content) {
  justify-content: center;
  gap: 16px;
}
.captcha-row {
  display: flex;
  gap: 8px;
  width: 100%;
  align-items: center;
}
.captcha-row .el-input {
  flex: 1;
}
.captcha-img {
  width: 110px;
  height: 40px;
  flex-shrink: 0;
  cursor: pointer;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f5f7fa;
}
.captcha-img img {
  width: 100%;
  height: 100%;
  object-fit: fill;
  display: block;
}
.captcha-tip {
  font-size: 12px;
  color: #909399;
}
</style>
