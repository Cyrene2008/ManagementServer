import { Alova } from '@/utils/http/alova/index';

export interface ExecuteCommandRequest {
  clientCuid: string;
  command: string;
  shell: number;
  timeoutSeconds: number;
}

export interface RemoteCommand {
  id: number;
  clientCuid: string;
  command: string;
  shell: number;
  status: number;
  exitCode: number | null;
  stdout: string | null;
  stderr: string | null;
  timeoutSeconds: number;
  completedTime: string | null;
  createdTime: string;
  updatedTime: string;
}

export interface PaginatedList<T> {
  pageIndex: number;
  totalPages: number;
  pageSize: number;
  itemCount: number;
  items: T[];
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export function executeCommand(data: ExecuteCommandRequest) {
  return Alova.Post<RemoteCommand>('/api/v1/commands/execute', data);
}

export function getCommand(id: number) {
  return Alova.Get<RemoteCommand>(`/api/v1/commands/${id}`);
}

export function listCommands(params: { pageIndex?: number; pageSize?: number; clientCuid?: string; status?: number }) {
  return Alova.Get<PaginatedList<RemoteCommand>>('/api/v1/commands', { params });
}
