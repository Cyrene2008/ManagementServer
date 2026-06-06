<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import { NCard, NForm, NFormItem, NInput, NSelect, NButton, NDataTable, NInputNumber, useMessage, NTag } from 'naive-ui';
import { executeCommand, listCommands, type RemoteCommand } from '@/api/commands/remote';

const message = useMessage();
const form = reactive({
  clientCuid: '',
  command: '',
  shell: 0,
  timeoutSeconds: 30,
});
const isSending = ref(false);
const commands = ref<RemoteCommand[]>([]);
const loading = ref(false);

const shellOptions = [
  { label: 'CMD', value: 0 },
  { label: 'PowerShell', value: 1 },
];

const statusMap: Record<number, { label: string; type: 'default' | 'success' | 'error' | 'warning' }> = {
  0: { label: '等待中', type: 'default' },
  1: { label: '执行中', type: 'warning' },
  2: { label: '已完成', type: 'success' },
  3: { label: '失败', type: 'error' },
  4: { label: '超时', type: 'error' },
};

const columns = [
  { title: 'ID', key: 'id', width: 60 },
  { title: '目标客户端', key: 'clientCuid', width: 260, ellipsis: { tooltip: true } },
  { title: '命令', key: 'command', ellipsis: { tooltip: true } },
  {
    title: 'Shell',
    key: 'shell',
    width: 80,
    render(row: any) { return row.shell === 1 ? 'PowerShell' : 'CMD'; },
  },
  {
    title: '状态',
    key: 'status',
    width: 80,
    render(row: any) {
      const s = statusMap[row.status] || { label: '未知', type: 'default' };
      return h(NTag, { type: s.type, size: 'small' }, { default: () => s.label });
    },
  },
  {
    title: '退出码',
    key: 'exitCode',
    width: 70,
    render(row: any) { return row.exitCode ?? '-'; },
  },
  {
    title: '输出',
    key: 'stdout',
    width: 300,
    ellipsis: { tooltip: true },
    render(row: any) { return row.stdout || row.stderr || '-'; },
  },
  {
    title: '时间',
    key: 'createdTime',
    width: 160,
    render(row: any) { return new Date(row.createdTime).toLocaleString('zh-CN'); },
  },
];

async function sendCommand() {
  if (!form.clientCuid || !form.command) {
    message.warning('请填写客户端 CUID 和命令');
    return;
  }
  isSending.value = true;
  try {
    await executeCommand(form);
    message.success('命令已发送');
    form.command = '';
    await loadCommands();
  } catch (e: any) {
    message.error('发送失败: ' + (e.message || e));
  } finally {
    isSending.value = false;
  }
}

async function loadCommands() {
  loading.value = true;
  try {
    const res = await listCommands({ pageIndex: 1, pageSize: 50 });
    commands.value = res.items || [];
  } catch (e) {
    console.error('加载命令历史失败', e);
  } finally {
    loading.value = false;
  }
}

onMounted(loadCommands);
</script>

<template>
  <div class="d-flex flex-col gap-y-4">
    <n-card title="远程命令执行">
      <n-form :model="form" label-placement="left" label-width="auto" style="max-width: 700px">
        <n-form-item label="目标客户端 CUID">
          <n-input v-model:value="form.clientCuid" placeholder="输入客户端 CUID" />
        </n-form-item>
        <n-form-item label="Shell 类型">
          <n-select v-model:value="form.shell" :options="shellOptions" />
        </n-form-item>
        <n-form-item label="命令">
          <n-input v-model:value="form.command" type="textarea" placeholder="输入要执行的命令" :rows="3" />
        </n-form-item>
        <n-form-item label="超时（秒）">
          <n-input-number v-model:value="form.timeoutSeconds" :min="5" :max="300" />
        </n-form-item>
        <n-form-item :show-label="false">
          <n-button type="primary" @click="sendCommand" :loading="isSending">执行命令</n-button>
        </n-form-item>
      </n-form>
    </n-card>

    <n-card title="命令执行历史">
      <n-data-table :columns="columns" :data="commands" :loading="loading" :bordered="false" size="small" />
    </n-card>
  </div>
</template>

<script lang="ts">
import { h } from 'vue';
export default { name: 'RemoteCommand' };
</script>
