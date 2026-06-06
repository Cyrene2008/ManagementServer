import {RouteRecordRaw} from "vue-router";
import {Layout} from "@/router/constant";
import {RocketOutlined, NotificationOutlined, ReloadOutlined, CodeOutlined} from "@vicons/antd";
import { renderIcon } from '@/utils/index';

const routeName = "commands";

const routes: Array<RouteRecordRaw> = [
  {
    path: '/commands',
    name: routeName,
    component: Layout,
    meta: {
      title: '命令',
      roles: ["CommandsUser"],
      sort: 98,
      icon: renderIcon(RocketOutlined)
    },
    children: [
      {
        path: 'broadcast',
        name: `${routeName}_broadcast`,
        component: () => import("@/views/commands/broadcast/index.vue"),
        meta: {
          title: '广播提醒',
          icon: renderIcon(NotificationOutlined)
        },
      },
      {
        path: 'reboot',
        name: `${routeName}_reboot`,
        component: () => import("@/views/commands/reboot/index.vue"),
        meta: {
          title: '重启实例',
          icon: renderIcon(ReloadOutlined)
        },
      },
      {
        path: 'remote',
        name: `${routeName}_remote`,
        component: () => import("@/views/commands/remote/index.vue"),
        meta: {
          title: '远程命令',
          icon: renderIcon(CodeOutlined)
        },
      }
    ]
  }
];

export default routes;
