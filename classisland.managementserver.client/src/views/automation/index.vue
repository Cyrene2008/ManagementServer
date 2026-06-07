<script setup lang="ts">
import { ref, onMounted, h } from 'vue';
import { NCard, NButton, NSelect, useMessage, NSpace, NTag, NModal, NForm, NFormItem, NInput, NPopconfirm, NDivider } from 'naive-ui';
import { getClientAutomation, updateClientAutomation, requestAutomationUpload } from '@/api/automation/index';
import { Alova } from '@/utils/http/alova/index';

const message = useMessage();
const selectedClient = ref('');
const clientOptions = ref<{ label: string; value: string }[]>([]);
const isLoading = ref(false);
const showJsonEditor = ref(false);
const rawJson = ref('');

// 已注册的触发器类型
const triggerTypes = [
  { id: 'classisland.app.startup', name: '应用启动', icon: '🚀' },
  { id: 'classisland.app.stopping', name: '应用关闭', icon: '🛑' },
  { id: 'classisland.cron', name: '定时触发', icon: '⏰' },
  { id: 'classisland.timeStateChanged', name: '时间状态变化', icon: '🔔' },
  { id: 'classisland.onClass', name: '上课时', icon: '📖' },
  { id: 'classisland.onBreakingTime', name: '课间休息', icon: '☕' },
  { id: 'classisland.onAfterSchool', name: '放学后', icon: '🏠' },
  { id: 'classisland.preTimePoint', name: '时间节点前', icon: '⏱️' },
  { id: 'classisland.ruleSet.rulesetChanged', name: '规则集变化', icon: '📐' },
  { id: 'classisland.signal', name: '信号触发', icon: '📡' },
  { id: 'classisland.uri', name: 'URI 触发', icon: '🔗' },
  { id: 'classisland.trayMenu', name: '托盘菜单', icon: '📋' },
];

// 已注册的动作类型
const actionTypes = [
  { id: 'classisland.weather.notification', name: '天气通知', icon: '🌤️' },
  { id: 'classisland.sleep', name: '休眠', icon: '💤' },
  { id: 'classisland.run', name: '运行程序', icon: '▶️' },
  { id: 'classisland.notification', name: '发送通知', icon: '🔔' },
  { id: 'classisland.modifyAppSettings', name: '修改设置', icon: '⚙️' },
  { id: 'classisland.app.restart', name: '重启应用', icon: '🔄' },
  { id: 'classisland.app.quit', name: '退出应用', icon: '🚪' },
];

interface TriggerSettings {
  Id: string;
  Settings?: any;
}

interface ActionItem {
  Id: string;
  Settings?: any;
}

interface ActionSet {
  Name: string;
  Actions: ActionItem[];
  IsEnabled: boolean;
  IsRevertEnabled: boolean;
}

interface Ruleset {
  Mode: number;
  IsReversed: boolean;
  Groups: any[];
}

interface Workflow {
  Triggers: TriggerSettings[];
  IsConditionEnabled: boolean;
  Ruleset?: Ruleset;
  ActionSet: ActionSet;
}

const workflows = ref<Workflow[]>([]);

function getTriggerInfo(id: string) {
  return triggerTypes.find(t => t.id === id) || { id, name: id, icon: '❓' };
}

function getActionInfo(id: string) {
  return actionTypes.find(a => a.id === id) || { id, name: id, icon: '❓' };
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
    const config = await getClientAutomation(selectedClient.value);
    const json = config?.workflowsJson || '[]';
    try {
      const parsed = JSON.parse(json);
      workflows.value = Array.isArray(parsed) ? parsed : [];
    } catch {
      workflows.value = [];
    }
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
    const json = JSON.stringify(workflows.value, null, 2);
    await updateClientAutomation(selectedClient.value, { workflowsJson: json });
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
    message.success('已请求客户端上报配置，请稍后刷新');
  } catch (e: any) {
    message.error('请求失败: ' + (e.message || e));
  }
}

function addWorkflow() {
  workflows.value.push({
    Triggers: [],
    IsConditionEnabled: false,
    ActionSet: {
      Name: '新行动组',
      Actions: [],
      IsEnabled: true,
      IsRevertEnabled: false,
    },
  });
}

function removeWorkflow(index: number) {
  workflows.value.splice(index, 1);
}

function addTrigger(workflowIndex: number, triggerId: string) {
  workflows.value[workflowIndex].Triggers.push({ Id: triggerId, Settings: {} });
}

function removeTrigger(workflowIndex: number, triggerIndex: number) {
  workflows.value[workflowIndex].Triggers.splice(triggerIndex, 1);
}

function addAction(workflowIndex: number, actionId: string) {
  workflows.value[workflowIndex].ActionSet.Actions.push({ Id: actionId, Settings: {} });
}

function removeAction(workflowIndex: number, actionIndex: number) {
  workflows.value[workflowIndex].ActionSet.Actions.splice(actionIndex, 1);
}

function openJsonEditor() {
  rawJson.value = JSON.stringify(workflows.value, null, 2);
  showJsonEditor.value = true;
}

function applyJsonEdit() {
  try {
    const parsed = JSON.parse(rawJson.value);
    workflows.value = Array.isArray(parsed) ? parsed : [];
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
    <n-card title="自动化管理">
      <n-form label-placement="left" label-width="auto" style="max-width: 700px">
        <n-form-item label="选择客户端">
          <n-select v-model:value="selectedClient" :options="clientOptions" filterable placeholder="选择客户端实例" @update:value="loadConfig" />
        </n-form-item>
      </n-form>

      <div v-if="selectedClient">
        <n-space class="mb-4">
          <n-button @click="requestUpload" size="small">请求客户端上报</n-button>
          <n-button @click="loadConfig" size="small">刷新</n-button>
          <n-button @click="addWorkflow" size="small" type="primary">添加工作流</n-button>
          <n-button @click="openJsonEditor" size="small">编辑 JSON</n-button>
          <n-button @click="saveConfig" size="small" type="success" :loading="isLoading">保存并推送</n-button>
        </n-space>
      </div>
    </n-card>

    <!-- 工作流列表 -->
    <n-card v-if="selectedClient && workflows.length > 0" title="工作流配置">
      <div v-for="(workflow, wfIndex) in workflows" :key="wfIndex" class="workflow-card">
        <div class="workflow-header">
          <n-space align="center">
            <n-tag type="info" size="small">工作流 {{ wfIndex + 1 }}</n-tag>
            <n-tag size="small">{{ workflow.Triggers.length }} 个触发器</n-tag>
            <n-tag size="small">{{ workflow.ActionSet.Actions.length }} 个动作</n-tag>
            <n-popconfirm @positive-click="removeWorkflow(wfIndex)">
              <template #trigger>
                <n-button size="tiny" type="error">删除工作流</n-button>
              </template>
              确定删除此工作流？
            </n-popconfirm>
          </n-space>
        </div>

        <!-- 触发器 -->
        <div class="section">
          <div class="section-title">触发器</div>
          <div class="items-list">
            <div v-for="(trigger, trigIndex) in workflow.Triggers" :key="trigIndex" class="item-card">
              <span class="item-icon">{{ getTriggerInfo(trigger.Id).icon }}</span>
              <span class="item-name">{{ getTriggerInfo(trigger.Id).name }}</span>
              <n-button size="tiny" type="error" @click="removeTrigger(wfIndex, trigIndex)">×</n-button>
            </div>
            <n-select
              size="tiny"
              placeholder="+ 添加触发器"
              :options="triggerTypes.map(t => ({ label: `${t.icon} ${t.name}`, value: t.id }))"
              @update:value="(val: string) => addTrigger(wfIndex, val)"
              style="width: 180px"
            />
          </div>
        </div>

        <!-- 条件 -->
        <div class="section">
          <div class="section-title">
            条件
            <n-switch v-model:value="workflow.IsConditionEnabled" size="small" />
          </div>
          <div v-if="workflow.IsConditionEnabled" class="condition-info">
            <n-tag type="warning" size="small">规则集已启用</n-tag>
          </div>
        </div>

        <!-- 动作 -->
        <div class="section">
          <div class="section-title">动作组: {{ workflow.ActionSet.Name }}</div>
          <div class="items-list">
            <div v-for="(action, actIndex) in workflow.ActionSet.Actions" :key="actIndex" class="item-card">
              <span class="item-icon">{{ getActionInfo(action.Id).icon }}</span>
              <span class="item-name">{{ getActionInfo(action.Id).name }}</span>
              <n-button size="tiny" type="error" @click="removeAction(wfIndex, actIndex)">×</n-button>
            </div>
            <n-select
              size="tiny"
              placeholder="+ 添加动作"
              :options="actionTypes.map(a => ({ label: `${a.icon} ${a.name}`, value: a.id }))"
              @update:value="(val: string) => addAction(wfIndex, val)"
              style="width: 180px"
            />
          </div>
        </div>
      </div>
    </n-card>

    <!-- JSON 编辑器弹窗 -->
    <n-modal v-model:show="showJsonEditor" title="编辑 JSON" preset="dialog" style="width: 800px">
      <n-input v-model:value="rawJson" type="textarea" :rows="20" placeholder="自动化工作流 JSON" />
      <template #action>
        <n-button @click="applyJsonEdit" type="primary">应用</n-button>
      </template>
    </n-modal>
  </div>
</template>

<style scoped>
.workflow-card {
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 12px;
  margin-bottom: 12px;
  background: #fafafa;
}

.workflow-header {
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid #e0e0e0;
}

.section {
  margin-bottom: 12px;
}

.section-title {
  font-size: 13px;
  font-weight: 600;
  color: #555;
  margin-bottom: 8px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.items-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.item-card {
  display: flex;
  align-items: center;
  gap: 6px;
  border: 1px solid #d0d0d0;
  border-radius: 6px;
  padding: 4px 10px;
  background: white;
  font-size: 13px;
}

.item-icon {
  font-size: 16px;
}

.item-name {
  font-weight: 500;
}

.condition-info {
  padding: 8px;
  background: #fff8e1;
  border-radius: 4px;
  border: 1px solid #ffe082;
}
</style>
