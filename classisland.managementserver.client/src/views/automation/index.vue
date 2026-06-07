<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { NCard, NButton, NSelect, NInput, useMessage, NSpace } from 'naive-ui';
import { getClientAutomation, updateClientAutomation, requestAutomationUpload } from '@/api/automation/index';
import { Alova } from '@/utils/http/alova/index';

const message = useMessage();
const selectedClient = ref('');
const clientOptions = ref<{ label: string; value: string }[]>([]);
const workflowsJson = ref('[]');
const isLoading = ref(false);

async function loadClients() {
  try {
    const res = await Alova.Get('/api/v1/clients_registry/all', { params: { pageIndex: 1, pageSize: 100 } });
    clientOptions.value = (res.items || []).map((c: any) => ({
      label: `${c.id} (${c.cuid})`,
      value: c.cuid,
    }));
  } catch (e) {
    console.error('加载客户端失败', e);
  }
}

async function loadConfig() {
  if (!selectedClient.value) return;
  isLoading.value = true;
  try {
    const config = await getClientAutomation(selectedClient.value);
    workflowsJson.value = config?.workflowsJson || '[]';
  } catch (e) {
    console.error('加载自动化配置失败', e);
  } finally {
    isLoading.value = false;
  }
}

async function saveConfig() {
  if (!selectedClient.value) return;
  isLoading.value = true;
  try {
    await updateClientAutomation(selectedClient.value, { workflowsJson: workflowsJson.value });
    message.success('自动化配置已保存并推送到客户端');
  } catch (e: any) {
    message.error('保存失败: ' + (e.message || e));
  } finally {
    isLoading.value = false;
  }
}

async function requestUpload() {
  if (!selectedClient.value) return;
  try {
    await requestAutomationUpload(selectedClient.value);
    message.success('已请求客户端上报自动化配置，请稍后刷新');
  } catch (e: any) {
    message.error('请求失败: ' + (e.message || e));
  }
}

onMounted(loadClients);
</script>

<template>
  <div class="d-flex flex-col gap-y-4">
    <n-card title="自动化管理">
      <n-form label-placement="left" label-width="auto" style="max-width: 700px">
        <n-form-item label="选择客户端">
          <n-select v-model:value="selectedClient" :options="clientOptions" filterable placeholder="选择客户端实例" @update:value="loadConfig" />
        </n-form-item>
      </n-form>

      <div v-if="selectedClient">
        <n-space class="mb-4">
          <n-button @click="requestUpload" size="small">请求客户端上报配置</n-button>
          <n-button @click="loadConfig" size="small">刷新</n-button>
        </n-space>

        <n-form label-placement="left" label-width="auto">
          <n-form-item label="工作流 JSON">
            <n-input v-model:value="workflowsJson" type="textarea" :rows="15" placeholder="自动化工作流 JSON" />
          </n-form-item>
          <n-form-item :show-label="false">
            <n-button type="primary" @click="saveConfig" :loading="isLoading">保存并推送到客户端</n-button>
          </n-form-item>
        </n-form>
      </div>
    </n-card>
  </div>
</template>

<script lang="ts">
export default { name: 'AutomationManagement' };
</script>
