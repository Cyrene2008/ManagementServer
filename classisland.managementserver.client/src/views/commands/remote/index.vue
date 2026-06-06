<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, h } from 'vue';
import { NCard, NForm, NFormItem, NSelect, NButton, NDataTable, NInputNumber, useMessage, NTag, NSpace, NInput } from 'naive-ui';
import { executeCommand, listCommands, type RemoteCommand } from '@/api/commands/remote';
import { ObjectsAssignee } from '@/api/globals';

const message = useMessage();
const targets = ref<Array<ObjectsAssignee>>([]);
const form = reactive({
  command: '',
  shell: 0,
  timeoutSeconds: 30,
  pin: '',
});
const isSending = ref(false);
const commands = ref<RemoteCommand[]>([]);
const loading = ref(false);
const expandedRows = ref<Set<number>>(new Set());
let autoRefreshTimer: ReturnType<typeof setTimeout> | null = null;

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

function toggleExpand(id: number) {
  if (expandedRows.value.has(id)) {
    expandedRows.value.delete(id);
  } else {
    expandedRows.value.add(id);
  }
}

const columns = [
  { title: 'ID', key: 'id', width: 60 },
  { title: '目标客户端', key: 'clientCuid', width: 200, ellipsis: { tooltip: true } },
  { title: '命令', key: 'command', width: 200, ellipsis: { tooltip: true } },
  {
    title: 'Shell',
    key: 'shell',
    width: 80,
    render(row: any) { return row.shell === 1 ? 'PS' : 'CMD'; },
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
    render(row: any) {
      const output = row.stdout || row.stderr || '';
      if (!output) return '-';
      const isExpanded = expandedRows.value.has(row.id);
      const truncated = output.length > 80 ? output.substring(0, 80) + '...' : output;
      return h('div', [
        h('div', {
          style: {
            whiteSpace: isExpanded ? 'pre-wrap' : 'nowrap',
            overflow: 'hidden',
            textOverflow: 'ellipsis',
            maxHeight: isExpanded ? '400px' : '24px',
            overflowY: isExpanded ? 'auto' : 'hidden',
            cursor: 'pointer',
            fontFamily: 'monospace',
            fontSize: '12px',
            background: '#f5f5f5',
            padding: '4px 8px',
            borderRadius: '4px',
          },
          onClick: () => toggleExpand(row.id),
        }, isExpanded ? output : truncated),
      ]);
    },
  },
  {
    title: '时间',
    key: 'createdTime',
    width: 150,
    render(row: any) { return new Date(row.createdTime).toLocaleString('zh-CN'); },
  },
];

async function sendCommand() {
  if (!targets.value || targets.value.length === 0) {
    message.warning('请选择目标客户端');
    return;
  }
  if (!form.command) {
    message.warning('请输入命令');
    return;
  }
  if (!form.pin) {
    message.warning('请输入远程协助 PIN');
    return;
  }

  isSending.value = true;
  try {
    // 对每个目标发送命令
    for (const target of targets.value) {
      const clientCuid = target.targetClientCuid || target.targetClientId;
      if (!clientCuid) continue;

      await executeCommand({
        clientCuid,
        command: form.command,
        shell: form.shell,
        timeoutSeconds: form.timeoutSeconds,
        pin: form.pin,
      });
    }
    message.success('命令已发送');
    form.command = '';
    await loadCommands();
    scheduleAutoRefresh();
  } catch (e: any) {
    message.error('发送失败: ' + (e.message || e));
  } finally {
    isSending.value = false;
  }
}

function scheduleAutoRefresh() {
  if (autoRefreshTimer) clearTimeout(autoRefreshTimer);
  autoRefreshTimer = setTimeout(() => {
    loadCommands();
    autoRefreshTimer = null;
  }, 15000);
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

onUnmounted(() => {
  if (autoRefreshTimer) clearTimeout(autoRefreshTimer);
});
</script>

<template>
  <div class="d-flex flex-col gap-y-4">
    <n-card title="远程命令执行">
      <n-form :model="form" label-placement="left" label-width="auto" style="max-width: 700px">
        <n-form-item label="目标">
          <AssigneeList v-model:value="targets" />
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
        <n-form-item label="远程协助 PIN">
          <n-input v-model:value="form.pin" placeholder="输入客户端显示的 6 位 PIN" maxlength="6" show-count />
        </n-form-item>
        <n-form-item :show-label="false">
          <n-space>
            <n-button type="primary" @click="sendCommand" :loading="isSending">执行命令</n-button>
          </n-space>
        </n-form-item>
      </n-form>
    </n-card>

    <n-card title="命令执行历史">
      <template #header-extra>
        <n-button @click="loadCommands" :loading="loading" size="small">刷新</n-button>
      </template>
      <n-data-table :columns="columns" :data="commands" :loading="loading" :bordered="false" size="small" :max-height="500" />
    </n-card>
  </div>
</template>
