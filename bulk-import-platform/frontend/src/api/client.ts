import axios from "axios";
import type {
  UploadResultDto,
  SaveMappingRequestDto,
  ValidationSummaryDto,
  ImportSummaryReportDto,
  UploadHistoryItemDto,
  DashboardSummaryDto,
  AuditLogDto,
  ImportType,
  ClientAccountDto,
  CreateClientAccountRequestDto,
  UpdateClientAccountRequestDto,
  ResetClientAccountPasswordRequestDto,
  RpaRunRequestDto,
  RpaRetryRequestDto,
  RpaRunSummaryDto,
  UpdateRecordAndRetryRequestDto,
} from "../types";

// Points at the ASP.NET Core Web API (see backend/BulkImportPlatform.Api).
// Override with VITE_API_BASE_URL in a .env file if the API runs elsewhere.
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:5199";

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: { "X-User": "demo-user" },
});

export const ImportsApi = {
  upload: async (type: ImportType, clientAccountId: number, file: File): Promise<UploadResultDto> => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("clientAccountId", String(clientAccountId));
    const { data } = await api.post(`/api/imports/${type}/upload`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    return data;
  },

  validate: async (
    type: ImportType,
    importJobId: number,
    request: SaveMappingRequestDto
  ): Promise<ValidationSummaryDto> => {
    const { data } = await api.post(`/api/imports/${type}/${importJobId}/validate`, request);
    return data;
  },

  getPreview: async (type: ImportType, importJobId: number): Promise<ValidationSummaryDto> => {
    const { data } = await api.get(`/api/imports/${type}/${importJobId}`);
    return data;
  },

  commit: async (type: ImportType, importJobId: number): Promise<ImportSummaryReportDto> => {
    const { data } = await api.post(`/api/imports/${type}/${importJobId}/commit`);
    return data;
  },

  history: async (type: ImportType, take = 50): Promise<UploadHistoryItemDto[]> => {
    const { data } = await api.get(`/api/imports/${type}/history`, { params: { take } });
    return data;
  },

  jobAudit: async (type: ImportType, importJobId: number): Promise<AuditLogDto[]> => {
    const { data } = await api.get(`/api/imports/${type}/${importJobId}/audit`);
    return data;
  },

  templateUrl: (type: ImportType) => `${API_BASE_URL}/api/templates/${type}`,
};

export const DashboardApi = {
  summary: async (): Promise<DashboardSummaryDto> => {
    const { data } = await api.get(`/api/dashboard/summary`);
    return data;
  },
};

export const AuditApi = {
  list: async (importJobId?: number, take = 200): Promise<AuditLogDto[]> => {
    const { data } = await api.get(`/api/audit-logs`, { params: { importJobId, take } });
    return data;
  },
};

export const ClientAccountsApi = {
  list: async (): Promise<ClientAccountDto[]> => {
    const { data } = await api.get(`/api/client-accounts`);
    return data;
  },

  create: async (request: CreateClientAccountRequestDto): Promise<ClientAccountDto> => {
    const { data } = await api.post(`/api/client-accounts`, request);
    return data;
  },

  update: async (id: number, request: UpdateClientAccountRequestDto): Promise<ClientAccountDto> => {
    const { data } = await api.put(`/api/client-accounts/${id}`, request);
    return data;
  },

  resetPassword: async (id: number, request: ResetClientAccountPasswordRequestDto): Promise<void> => {
    await api.post(`/api/client-accounts/${id}/reset-password`, request);
  },
};

export const RpaApi = {
  run: async (importJobId: number, request: RpaRunRequestDto): Promise<RpaRunSummaryDto> => {
    const { data } = await api.post(`/api/rpa/${importJobId}/run`, request);
    return data;
  },

  retry: async (importJobId: number, request: RpaRetryRequestDto): Promise<RpaRunSummaryDto> => {
    const { data } = await api.post(`/api/rpa/${importJobId}/retry`, request);
    return data;
  },

  status: async (importJobId: number): Promise<RpaRunSummaryDto> => {
    const { data } = await api.get(`/api/rpa/${importJobId}/status`);
    return data;
  },

  screenshotUrl: (fileName: string) => `${API_BASE_URL}/api/rpa/screenshots/${fileName}`,

  updateRecordAndRetry: async (
    importJobId: number,
    importRecordId: number,
    request: UpdateRecordAndRetryRequestDto
  ): Promise<RpaRunSummaryDto> => {
    const { data } = await api.put(`/api/rpa/${importJobId}/records/${importRecordId}`, request);
    return data;
  },
};
