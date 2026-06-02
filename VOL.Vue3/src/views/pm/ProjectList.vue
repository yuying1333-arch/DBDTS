<template>
  <div class="project-list">
    <div class="filter-bar">
      <el-form :inline="true" :model="filter" class="filter-form">
        <el-form-item label="项目名称">
          <el-input v-model="filter.name" placeholder="请输入" clearable style="width:160px" />
        </el-form-item>
        <el-form-item label="项目编号">
          <el-input v-model="filter.code" placeholder="请输入" clearable style="width:140px" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="filter.status" placeholder="全部" clearable style="width:100px">
            <el-option label="未开始" :value="0" />
            <el-option label="进行中" :value="1" />
            <el-option label="已结束" :value="2" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="search">查询</el-button>
          <el-button @click="resetFilter">重置</el-button>
        </el-form-item>
      </el-form>
    </div>

    <el-table
      ref="tableRef"
      :data="tableData"
      stripe
      highlight-current-row
      style="width:100%"
      @current-change="onCurrentChange"
      @sort-change="onSortChange"
    >
      <el-table-column type="index" label="序号" width="60" />
      <el-table-column prop="name" label="项目名称" min-width="160" sortable="custom" />
      <el-table-column prop="code" label="项目编号" width="120" sortable="custom" />
      <el-table-column prop="statusName" label="状态" width="100" />
      <el-table-column prop="createDate" label="创建时间" width="120" sortable="custom" />
      <el-table-column prop="remark" label="备注" min-width="120" show-overflow-tooltip />
    </el-table>

    <div class="pagination-wrap">
      <el-pagination
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :page-sizes="[10, 20, 50]"
        :total="total"
        layout="total, sizes, prev, pager, next"
        @size-change="load"
        @current-change="load"
      />
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, watch, nextTick } from 'vue'
import * as api from '@/api/project'

const props = defineProps({ selectedId: [String, Number] })
const emit = defineEmits(['select', 'refresh'])
const tableRef = ref(null)

const filter = reactive({ name: '', code: '', status: null })
const tableData = ref([])
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)
const sortField = ref('')
const sortOrder = ref('')

function load() {
  const params = {
    page: page.value,
    rows: pageSize.value,
    name: filter.name || undefined,
    code: filter.code || undefined,
    status: filter.status,
    sort: sortField.value || undefined,
    order: sortOrder.value || undefined
  }
  api.getProjectPage(params, true).then((res) => {
    const data = res?.data
    const rows = data?.rows ?? []
    const totalCount = data?.total ?? 0
    if (Array.isArray(rows) && (res.status || rows.length > 0 || totalCount >= 0)) {
      tableData.value = rows
      total.value = totalCount
      nextTick(() => setCurrentRowBySelected())
    }
  }).catch(() => {
    tableData.value = []
    total.value = 0
  })
}

function search() {
  page.value = 1
  load()
}

function resetFilter() {
  filter.name = ''
  filter.code = ''
  filter.status = null
  page.value = 1
  load()
}

function onCurrentChange(row) {
  emit('select', row ? row.id : null)
}

function onSortChange({ prop, order }) {
  sortField.value = prop || ''
  sortOrder.value = order === 'ascending' ? 'asc' : order === 'descending' ? 'desc' : ''
  load()
}

function setCurrentRowBySelected() {
  if (!tableRef.value) return
  if (!props.selectedId) {
    tableRef.value.setCurrentRow()
    return
  }
  const row = tableData.value.find(r => r.id === props.selectedId || r.id === parseInt(props.selectedId, 10))
  tableRef.value.setCurrentRow(row || null)
}

watch(() => props.selectedId, () => nextTick(setCurrentRowBySelected))

load()
</script>

<style scoped>
.project-list {
  width: 100%;
}
.filter-bar {
  margin-bottom: 16px;
}
.filter-form :deep(.el-form-item) {
  margin-bottom: 8px;
}
.pagination-wrap {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
