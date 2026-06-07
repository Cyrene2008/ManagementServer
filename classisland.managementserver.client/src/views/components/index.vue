<script setup lang="ts">
import { ref, reactive, onMounted, computed, h } from 'vue';
import { NCard, NButton, NSelect, useMessage, NSpace, NTag, NIcon, NModal, NForm, NFormItem, NInput, NCollapse, NCollapseItem, NSwitch, NPopconfirm } from 'naive-ui';
import { getClientComponents, updateClientComponents, requestClientUpload } from '@/api/components/index';
import { Alova } from '@/utils/http/alova/index';

const message = useMessage();
const selectedClient = ref('');
const clientOptions = ref<{ label: string; value: string }[]>([]);
const isLoading = ref(false);
const showJsonEditor = ref(false);
const rawJson = ref('');

// 已注册的组件类型（硬编码，后续可从 API 获取）
const componentTypes = [
  { id: 'ee8f66bd-1a3e-4a61-9a29-1ad4e5b5f1a1', name: '文本', icon: '📝', category: '基础' },
  { id: '9e1af71d-8b7e-4b4f-9c6a-7d8e9f0a1b2c', name: '时钟', icon: '🕐', category: '基础' },
  { id: 'df3f8295-5a3b-4c2d-8e1f-0a9b8c7d6e5f', name: '日期', icon: '📅', category: '基础' },
  { id: '1db2017d-4c5e-6f7a-8b9c-0d1e2f3a4b5c', name: '课表', icon: '📋', category: '基础' },
  { id: 'ca495086-2d3e-4f5a-6b7c-8d9e0f1a2b3c', name: '天气', icon: '🌤️', category: '基础' },
  { id: 'ab0f26d5-3e4f-5a6b-7c8d-9e0f1a2b3c4d', name: '分隔线', icon: '➖', category: '基础' },
  { id: '7c645d35-4f5a-6b7c-8d9e-0f1a2b3c4d5e', name: '倒计时', icon: '⏱️', category: '基础' },
  { id: '7e19a113-5a6b-7c8d-9e0f-1a2b3c4d5e6f', name: '轮播容器', icon: '🔄', category: '容器' },
  { id: '70fcd5ea-6b7c-8d9e-0f1a-2b3c4d5e6f70', name: '滚动容器', icon: '📜', category: '容器' },
  { id: 'c911d762-7c8d-9e0f-1a2b-3c4d5e6f7081', name: '分组容器', icon: '📦', category: '容器' },
  { id: '2d849ece-8d9e-0f1a-2b3c-4d5e6f708192', name: '堆叠容器', icon: '📚', category: '容器' },
];

const containerIds = new Set(['7e19a113-5a6b-7c8d-9e0f-1a2b3c4d5e6f', '70fcd5ea-6b7c-8d9e-0f1a-2b3c4d5e6f70', 'c911d762-7c8d-9e0f-1a2b-3c4d5e6f7081', '2d849ece-8d9e-0f1a-2b3c-4d5e6f708192']);

interface ComponentItem {
  Id: string;
  Settings?: any;
  Children?: ComponentItem[];
}

interface LineItem {
  IsMainLine: boolean;
  IsNotificationEnabled: boolean;
  Children: ComponentItem[];
}

const layout = ref<LineItem[]>([]);

const componentMap = computed(() => {
  const map: Record<string, typeof componentTypes[0]> = {};
  componentTypes.forEach(c => { map[c.id.toLowerCase()] = c; });
  return map;
});

function getComponentInfo(id: string) {
  return componentMap.value[id.toLowerCase()] || { id, name: '未知组件', icon: '❓', category: '未知' };
}

function isContainer(id: string) {
  return containerIds.has(id.toLowerCase());
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

async function loadConfig() {
  if (!selectedClient.value) return;
  isLoading.value = true;
  try {
    const config = await getClientComponents(selectedClient.value);
    const json = config?.layoutJson || '[]';
    try {
      const parsed = JSON.parse(json);
      if (parsed.Lines && Array.isArray(parsed.Lines)) {
        layout.value = parsed.Lines;
      } else if (Array.isArray(parsed)) {
        layout.value = parsed;
      } else {
        layout.value = [];
      }
    } catch {
      layout.value = [];
    }
  } catch (e) {
    console.error('加载配置失败', e);
  } finally {
    isLoading.value = false;
  }
}

async function saveConfig() {
  if (!selectedClient.value) return;
  isLoading.value = true;
  try {
    const json = JSON.stringify({ Lines: layout.value }, null, 2);
    await updateClientComponents(selectedClient.value, { layoutJson: json });
    message.success('配置已保存并推送到客户端');
  } catch (e: any) {
    message.error('保存失败: ' + (e.message || e));
  } finally {
    isLoading.value = false;
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

function addLine() {
  layout.value.push({
    IsMainLine: false,
    IsNotificationEnabled: false,
    Children: [],
  });
}

function removeLine(index: number) {
  layout.value.splice(index, 1);
}

function addComponent(lineIndex: number, componentId: string) {
  const newComponent: ComponentItem = {
    Id: componentId,
    Settings: {},
  };
  if (isContainer(componentId)) {
    newComponent.Children = [];
  }
  layout.value[lineIndex].Children.push(newComponent);
}

function removeComponent(lineIndex: number, compIndex: number) {
  layout.value[lineIndex].Children.splice(compIndex, 1);
}

function moveComponent(lineIndex: number, compIndex: number, direction: 'up' | 'down') {
  const children = layout.value[lineIndex].Children;
  const newIndex = direction === 'up' ? compIndex - 1 : compIndex + 1;
  if (newIndex < 0 || newIndex >= children.length) return;
  const temp = children[compIndex];
  children[compIndex] = children[newIndex];
  children[newIndex] = temp;
}

function addChildToContainer(lineIndex: number, compIndex: number, componentId: string) {
  const comp = layout.value[lineIndex].Children[compIndex];
  if (!comp.Children) comp.Children = [];
  comp.Children.push({ Id: componentId, Settings: {} });
}

function removeChildFromContainer(lineIndex: number, compIndex: number, childIndex: number) {
  const comp = layout.value[lineIndex].Children[compIndex];
  if (comp.Children) {
    comp.Children.splice(childIndex, 1);
  }
}

function openJsonEditor() {
  rawJson.value = JSON.stringify({ Lines: layout.value }, null, 2);
  showJsonEditor.value = true;
}

function applyJsonEdit() {
  try {
    const parsed = JSON.parse(rawJson.value);
    if (parsed.Lines && Array.isArray(parsed.Lines)) {
      layout.value = parsed.Lines;
    } else if (Array.isArray(parsed)) {
      layout.value = parsed;
    }
    showJsonEditor.value = false;
    message.success('JSON 已应用');
  } catch (e: any) {
    message.error('JSON 格式错误: ' + e.message);
  }
}

onMounted(loadClients);
</script>

<template>
  <div class="d-flex flex-col gap-y-4">
    <!-- 客户端选择 -->
    <n-card title="组件配置管理">
      <n-form label-placement="left" label-width="auto" style="max-width: 700px">
        <n-form-item label="选择客户端">
          <n-select v-model:value="selectedClient" :options="clientOptions" filterable placeholder="选择客户端实例" @update:value="loadConfig" />
        </n-form-item>
      </n-form>

      <div v-if="selectedClient">
        <n-space class="mb-4">
          <n-button @click="requestUpload" size="small">请求客户端上报</n-button>
          <n-button @click="loadConfig" size="small">刷新</n-button>
          <n-button @click="addLine" size="small" type="primary">添加行</n-button>
          <n-button @click="openJsonEditor" size="small">编辑 JSON</n-button>
          <n-button @click="saveConfig" size="small" type="success" :loading="isLoading">保存并推送</n-button>
        </n-space>
      </div>
    </n-card>

    <!-- 可视化编辑器 -->
    <n-card v-if="selectedClient && layout.length > 0" title="主界面布局">
      <div class="layout-editor">
        <div v-for="(line, lineIndex) in layout" :key="lineIndex" class="line-container">
          <!-- 行头部 -->
          <div class="line-header">
            <n-space align="center">
              <n-tag :type="line.IsMainLine ? 'success' : 'default'" size="small">
                {{ line.IsMainLine ? '主行' : '行 ' + (lineIndex + 1) }}
              </n-tag>
              <n-tag v-if="line.IsNotificationEnabled" type="info" size="small">通知</n-tag>
              <n-space size="small">
                <n-button size="tiny" @click="line.IsMainLine = !line.IsMainLine">
                  {{ line.IsMainLine ? '取消主行' : '设为主行' }}
                </n-button>
                <n-button size="tiny" @click="line.IsNotificationEnabled = !line.IsNotificationEnabled">
                  {{ line.IsNotificationEnabled ? '关闭通知' : '开启通知' }}
                </n-button>
                <n-popconfirm @positive-click="removeLine(lineIndex)">
                  <template #trigger>
                    <n-button size="tiny" type="error">删除行</n-button>
                  </template>
                  确定删除此行？
                </n-popconfirm>
              </n-space>
            </n-space>
          </div>

          <!-- 组件列表 -->
          <div class="components-row">
            <div v-for="(comp, compIndex) in line.Children" :key="compIndex" class="component-card">
              <div class="component-info">
                <span class="component-icon">{{ getComponentInfo(comp.Id).icon }}</span>
                <span class="component-name">{{ getComponentInfo(comp.Id).name }}</span>
              </div>
              <div class="component-actions">
                <n-button size="tiny" @click="moveComponent(lineIndex, compIndex, 'up')" :disabled="compIndex === 0">↑</n-button>
                <n-button size="tiny" @click="moveComponent(lineIndex, compIndex, 'down')" :disabled="compIndex === line.Children.length - 1">↓</n-button>
                <n-popconfirm @positive-click="removeComponent(lineIndex, compIndex)">
                  <template #trigger>
                    <n-button size="tiny" type="error">×</n-button>
                  </template>
                  确定删除此组件？
                </n-popconfirm>
              </div>

              <!-- 容器子组件 -->
              <div v-if="isContainer(comp.Id) && comp.Children" class="container-children">
                <div class="container-header">子组件:</div>
                <div class="children-list">
                  <div v-for="(child, childIndex) in comp.Children" :key="childIndex" class="child-card">
                    <span>{{ getComponentInfo(child.Id).icon }} {{ getComponentInfo(child.Id).name }}</span>
                    <n-button size="tiny" type="error" @click="removeChildFromContainer(lineIndex, compIndex, childIndex)">×</n-button>
                  </div>
                  <n-select
                    size="tiny"
                    placeholder="添加子组件"
                    :options="componentTypes.map(c => ({ label: `${c.icon} ${c.name}`, value: c.id }))"
                    @update:value="(val: string) => addChildToContainer(lineIndex, compIndex, val)"
                    style="width: 150px"
                  />
                </div>
              </div>
            </div>

            <!-- 添加组件 -->
            <n-select
              size="small"
              placeholder="+ 添加组件"
              :options="componentTypes.map(c => ({ label: `${c.icon} ${c.name}`, value: c.id }))"
              @update:value="(val: string) => addComponent(lineIndex, val)"
              style="width: 150px"
            />
          </div>
        </div>
      </div>
    </n-card>

    <!-- JSON 编辑器弹窗 -->
    <n-modal v-model:show="showJsonEditor" title="编辑 JSON" preset="dialog" style="width: 800px">
      <n-input v-model:value="rawJson" type="textarea" :rows="20" placeholder="组件布局 JSON" />
      <template #action>
        <n-button @click="applyJsonEdit" type="primary">应用</n-button>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.layout-editor {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.line-container {
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 12px;
  background: #fafafa;
}

.line-header {
  margin-bottom: 8px;
  padding-bottom: 8px;
  border-bottom: 1px solid #e0e0e0;
}

.components-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: flex-start;
}

.component-card {
  border: 1px solid #d0d0d0;
  border-radius: 6px;
  padding: 8px;
  background: white;
  min-width: 120px;
}

.component-info {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
}

.component-icon {
  font-size: 18px;
}

.component-name {
  font-size: 13px;
  font-weight: 500;
}

.component-actions {
  display: flex;
  gap: 4px;
}

.container-children {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px dashed #c0c0c0;
}

.container-header {
  font-size: 11px;
  color: #888;
  margin-bottom: 4px;
}

.children-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
}

.child-card {
  display: flex;
  align-items: center;
  gap: 4px;
  border: 1px solid #e0e0e0;
  border-radius: 4px;
  padding: 2px 6px;
  background: #f5f5f5;
  font-size: 12px;
}
</style>
