<template>
  <div class="message-operation">
    <el-card class="section">
      <template #header><span>接收消息</span></template>
      <div class="row">
        <span class="label">接收来自：</span>
        <el-radio-group v-model="receiveTargetId" @change="loadReceivedMessages">
          <el-radio v-for="item in receiveTargets" :key="'r-' + item.id" :label="item.id">{{ item.name }}</el-radio>
        </el-radio-group>
      </div>
      <div class="content-box receive-box">
        <div v-for="(msg, idx) in receivedMessages" :key="idx" class="msg-item">
          <div class="msg-time">{{ formatTime(msg.createTime) }}</div>
          <div class="msg-content">{{ msg.content }}</div>
        </div>
        <div v-if="receivedMessages.length === 0" class="empty-tip">暂无消息</div>
      </div>
    </el-card>

    <el-card class="section">
      <template #header><span>发出消息</span></template>
      <div class="row">
        <span class="label">发消息给：</span>
        <el-radio-group v-model="sendTargetId">
          <el-radio v-for="item in sendTargets" :key="'s-' + item.id" :label="item.id">{{ item.name }}</el-radio>
        </el-radio-group>
      </div>
    </el-card>

    <el-card class="section">
      <template #header><span>语音输入</span></template>
      <div class="row">
        <el-button type="primary" size="small" :disabled="voiceRecording" @click="startVoice">开始录音</el-button>
        <el-button v-if="voiceRecording" type="danger" size="small" @click="stopVoice">停止并识别</el-button>
        <el-upload
          :auto-upload="false"
          :show-file-list="false"
          accept="audio/*,.mp3,.wav,.pcm,.aac"
          @change="onVoiceFileChange"
        >
          <el-button size="small">或选择录音文件</el-button>
        </el-upload>
      </div>
      <el-input v-model="voiceText" type="textarea" :rows="3" placeholder="语音识别内容" readonly />
    </el-card>

    <el-card class="section">
      <template #header><span>手写/图片上传</span></template>
      <el-upload
        :auto-upload="false"
        :show-file-list="false"
        accept="image/*"
        @change="onImageFileChange"
      >
        <el-button size="small">选择图片</el-button>
      </el-upload>
      <div v-if="uploadedImageUrl" class="thumb-wrap">
        <el-image :src="uploadedImageUrl" fit="contain" style="width:120px;height:120px" />
      </div>
      <el-input v-model="imageOcrText" type="textarea" :rows="3" placeholder="图片文字识别内容（待接入）" readonly />
    </el-card>

    <el-card class="section">
      <template #header><span>合并内容（可修改）</span></template>
      <el-input v-model="mergedContent" type="textarea" :rows="4" placeholder="点击【合并】将语音与图片识别内容合并到此" />
      <div class="btn-row">
        <el-button type="primary" @click="mergeContent">合并</el-button>
      </div>
    </el-card>

    <div class="action-btns">
      <el-button type="warning" @click="resetAll">重置</el-button>
      <el-button type="success" @click="submitMessage">提交</el-button>
    </div>
  </div>
</template>

<script>
import http from '@/api/http.js';

export default {
  name: 'MessageOperation',
  data() {
    return {
      receiveTargets: [],
      sendTargets: [],
      receiveTargetId: null,
      sendTargetId: null,
      receivedMessages: [],
      voiceText: '',
      imageOcrText: '',
      mergedContent: '',
      uploadedImageUrl: '',
      voiceRecording: false,
      mediaRecorder: null,
      audioChunks: []
    };
  },
  mounted() {
    this.loadTargets();
  },
  methods: {
    async loadTargets() {
      try {
        const r = await http.post('api/MessageOperation/getReceiveTargets', {}, true);
        this.receiveTargets = Array.isArray(r) ? r : (r?.data || []);
        if (this.receiveTargets.length && this.receiveTargetId == null) {
          this.receiveTargetId = this.receiveTargets[0].id;
          this.loadReceivedMessages();
        }
      } catch (e) {}
      try {
        const s = await http.post('api/MessageOperation/getSendTargets', {}, false);
        this.sendTargets = Array.isArray(s) ? s : (s?.data || []);
        if (this.sendTargets.length && this.sendTargetId == null) this.sendTargetId = this.sendTargets[0].id;
      } catch (e) {}
    },
    async loadReceivedMessages() {
      if (!this.receiveTargetId) return;
      try {
        const r = await http.post('api/MessageOperation/getReceivedMessages', { senderId: this.receiveTargetId }, false);
        this.receivedMessages = Array.isArray(r) ? r : (r?.data || []);
      } catch (e) {}
    },
    formatTime(str) {
      if (!str) return '';
      const d = new Date(str);
      return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0') + ' ' +
        String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0');
    },
    async startVoice() {
      if (!navigator.mediaDevices?.getUserMedia) {
        this.$message.warning('当前浏览器不支持录音');
        return;
      }
      try {
        const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
        this.audioChunks = [];
        this.mediaRecorder = new MediaRecorder(stream);
        this.mediaRecorder.ondataavailable = (e) => { if (e.data.size) this.audioChunks.push(e.data); };
        this.mediaRecorder.onstop = () => {
          stream.getTracks().forEach(t => t.stop());
          if (this.audioChunks.length) {
            const blob = new Blob(this.audioChunks, { type: 'audio/webm' });
            this.uploadVoiceBlob(blob);
          }
        };
        this.mediaRecorder.start();
        this.voiceRecording = true;
      } catch (e) {
        this.$message.error('无法获取麦克风');
      }
    },
    stopVoice() {
      if (this.mediaRecorder && this.voiceRecording) {
        this.mediaRecorder.stop();
        this.voiceRecording = false;
      }
    },
    onVoiceFileChange(file) {
      const raw = file.raw;
      if (!raw) return;
      this.uploadVoiceFile(raw);
    },
    uploadVoiceFile(file) {
      const form = new FormData();
      form.append('file', file, file.name || 'audio.mp3');
      http.post('api/MessageOperation/voiceRecognize', form, '转写中...', { headers: { 'Content-Type': 'multipart/form-data' } })
        .then((res) => {
          this.voiceText = (res && res.text) ? res.text : '';
          if (res && res.message) this.$message.info(res.message);
        })
        .catch(() => {});
    },
    uploadVoiceBlob(blob) {
      const form = new FormData();
      form.append('file', blob, 'audio.webm');
      http.post('api/MessageOperation/voiceRecognize', form, '转写中...', { headers: { 'Content-Type': 'multipart/form-data' } })
        .then((res) => {
          this.voiceText = (res && res.text) ? res.text : '';
          if (res && res.message) this.$message.info(res.message);
        })
        .catch(() => {});
    },
    onImageFileChange(file) {
      const raw = file.raw;
      if (!raw) return;
      this.uploadedImageUrl = URL.createObjectURL(raw);
      const form = new FormData();
      form.append('file', raw, raw.name || 'image.png');
      http.post('api/MessageOperation/imageTextRecognize', form, false, { headers: { 'Content-Type': 'multipart/form-data' } })
        .then((res) => {
          this.imageOcrText = (res && res.text) ? res.text : '';
          if (res && res.message) this.$message.info(res.message);
        })
        .catch(() => {});
    },
    mergeContent() {
      const parts = [];
      if (this.voiceText) parts.push(this.voiceText);
      if (this.imageOcrText) parts.push(this.imageOcrText);
      this.mergedContent = parts.join('\n\n');
      this.$message.success('已合并');
    },
    resetAll() {
      this.receiveTargetId = this.receiveTargets.length ? this.receiveTargets[0].id : null;
      this.sendTargetId = this.sendTargets.length ? this.sendTargets[0].id : null;
      this.loadReceivedMessages();
      this.voiceText = '';
      this.imageOcrText = '';
      this.mergedContent = '';
      this.uploadedImageUrl = '';
      this.$message.success('已恢复默认');
    },
    async submitMessage() {
      if (!this.sendTargetId) {
        this.$message.warning('请选择发消息给谁');
        return;
      }
      const content = (this.mergedContent || '').trim();
      if (!content) {
        this.$message.warning('请先合并或输入要发送的内容');
        return;
      }
      try {
        const res = await http.post('api/MessageOperation/submitMessage', { receiverId: this.sendTargetId, content }, true);
        const ok = res && (res.status === true || res.status === 'true');
        if (ok) {
          this.$message.success('提交成功');
          this.mergedContent = '';
        } else {
          this.$message.warning((res && res.message) || '提交失败');
        }
      } catch (e) {}
    }
  }
};
</script>

<style scoped>
.message-operation { padding: 20px; max-width: 900px; margin: 0 auto; }
.section { margin-bottom: 20px; }
.row { display: flex; align-items: flex-start; margin-bottom: 12px; }
.label { width: 100px; flex-shrink: 0; line-height: 32px; }
.content-box { border: 1px solid #eee; border-radius: 8px; padding: 12px; min-height: 80px; background: #fafafa; }
.receive-box { max-height: 200px; overflow-y: auto; }
.msg-item { padding: 8px 0; border-bottom: 1px solid #f0f0f0; }
.msg-time { font-size: 12px; color: #999; margin-bottom: 4px; }
.msg-content { font-size: 14px; color: #333; }
.empty-tip { text-align: center; color: #999; padding: 20px 0; }
.btn-row { margin-top: 12px; }
.thumb-wrap { margin-top: 8px; }
.action-btns { margin-top: 20px; }
.action-btns .el-button { margin-right: 12px; }
</style>
