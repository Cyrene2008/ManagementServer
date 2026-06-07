<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { NCard, NButton, NDataTable, NSpace, NInput, useMessage, NModal, NForm, NFormItem, NSelect } from 'naive-ui';
import { listTemplates, createTemplate, deleteTemplate, getClientComponents, updateClientComponents, requestClientUpload, type ComponentTemplate, type ClientComponentConfig } from '@/api/components/index';
import { Alova } from '@/utils/http/alova/index';

const message = useMessage();
const templates = ref<ComponentTemplate[]>([]);
const loading = ref(false);
const showCreateModal = ref(false);
const showEditModal = ref(false);
const selectedClient = ref('');
const clientConfig = ref<ClientComponentConfig | null>(null);
const clientOptions = ref<{ label: string; value: string }[]>([]);

const newTemplate = ref({
  name: '',
  layoutJson: '[]',
});

const editConfig = ref({
  layoutJson: '[]',
});

const templateColumns = [
  { title: '名称', key: 'name', width: 200 },
  { title: 'ID', key: 'id', width: 300, ellipsis: { tooltip: true } },
  {
    title: '创建时间',
    key: 'createdTime',
    width: 180,
    render(row: any) { return new Date(row.createdTime).toLocaleString('zh-CN'); },
  },
  {
    title: '操作',
    key: 'actions',
    width: 200,
    render(row: any) {
      return h(NSpace, {}, {
        default: () => [
          h(NButton, { size: 'small', onClick: () => viewTemplate(row) }, { default: () => '查看' }),
          h(NButton, { size: 'small', type: 'error', onClick: () => removeTemplate(row.id) }, { default: () => '删除' }),
        ],
      });
    },
  },
];

async function loadTemplates() {
  loading.value = true;
  try {
    templates.value = await listTemplates();
  } catch (e) {
    console.error('加载模板失败', e);
  } finally {
    loading.value = false;
  }
}

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

async function addTemplate() {
  try {
    await createTemplate(newTemplate.value);
    showCreateModal.value = false;
    newTemplate.value = { name: '', layoutJson: '[]' };
    await loadTemplates();
    message.success('模板已创建');
  } catch (e: any) {
    message.error('创建失败: ' + (e.message || e));
  }
}

async function removeTemplate(id: string) {
  try {
    await deleteTemplate(id);
    await loadTemplates();
    message.success('模板已删除');
  } catch (e: any) {
    message.error('删除失败: ' + (e.message || e));
  }
}

function viewTemplate(template: ComponentTemplate) {
  editConfig.value.layoutJson = template.layoutJson;
  showEditModal.value = true;
}

async function loadClientConfig() {
  if (!selectedClient.value) return;
  try {
    clientConfig.value = await getClientComponents(selectedClient.value);
    editConfig.value.layoutJson = clientConfig.value?.layoutJson || '[]';
  } catch (e) {
    console.error('加载客户端配置失败', e);
  }
}

async function saveClientConfig() {
  if (!selectedClient.value) return;
  try {
    await updateClientComponents(selectedClient.value, {
      layoutJson: editConfig.value.layoutJson,
    });
    message.success('配置已保存并推送到客户端');
  } catch (e: any) {
    message.error('保存失败: ' + (e.message || e));
  }
}

async function requestUpload() {
  if (!selectedClient.value) return;
  try {
    await requestClientUpload(selectedClient.value);
    message.success('已请求客户端上报配置，请稍后刷新');
  } catch (e: any) {
    message.error('请求失败: ' + (e.message || e));
  }
}

onMounted(() => {
  loadTemplates();
  loadClients();
});
</script>

<template>
  <div class="d-flex flex-col gap-y-4">
    <!-- 模板管理 -->
    <n-card title="组件配置模板">
      <template #header-extra>
        <n-space>
          <n-button @click="showCreateModal = true" type="primary" size="small">新建模板</n-button>
          <n-button @click="loadTemplates" size="small">刷新</n-button>
        </n-space>
      </template>
      <n-data-table :columns="templateColumns" :data="templates" :loading="loading" :bordered="false" size="small" />
    </n-card>

    <!-- 客户端配置 -->
    <n-card title="客户端组件配置">
      <n-form label-placement="left" label-width="auto" style="max-width: 700px">
        <n-form-item label="选择客户端">
          <n-select v-model:value="selectedClient" :options="clientOptions" filterable placeholder="选择客户端实例" @update:value="loadClientConfig" />
        </n-form-item>
      </n-form>

      <div v-if="selectedClient">
        <n-space class="mb-4">
          <n-button @click="requestUpload" size="small">请求客户端上报配置</n-button>
          <n-button @click="loadClientConfig" size="small">刷新配置</n-button>
        </n-space>

        <n-form v-if="clientConfig" label-placement="left" label-width="auto">
          <n-form-item label="组件布局 JSON">
            <n-input v-model:value="editConfig.layoutJson" type="textarea" :rows="10" placeholder="组件布局 JSON" />
          </n-form-item>
          <n-form-item :show-label="false">
            <n-space>
              <n-button type="primary" @click="saveClientConfig">保存并推送到客户端</n-button>
            </n-space>
          </n-form-item>
        </n-form>
      </div>
    </n-card>

    <!-- 创建模板弹窗 -->
    <n-modal v-model:show="showCreateModal" title="新建组件配置模板" preset="dialog" style="width: 600px">
      <n-form>
        <n-form-item label="模板名称">
          <n-input v-model:value="newTemplate.name" placeholder="输入模板名称" />
        </n-form-item>
        <n-form-item label="组件布局 JSON">
          <n-input v-model:value="newTemplate.layoutJson" type="textarea" :rows="10" placeholder="组件布局 JSON" />
        </n-form-item>
      </n-form>
      <template #action>
        <n-button @click="addTemplate" type="primary">创建</n-button>
      </template>
    </n-modal>

    <!-- 查看模板弹窗 -->
    <n-modal v-model:show="showEditModal" title="查看模板" preset="dialog" style="width: 600px">
      <n-form>
        <n-form-item label="组件布局 JSON">
          <n-input v-model:value="editConfig.layoutJson" type="textarea" :rows="15" readonly />
        </n-form-item>
      </n-form>
    </n-modal>
  </div>
</template>

<script lang="ts">
import { h } from 'vue';
export default { name: 'ComponentManagement' };
</script>
