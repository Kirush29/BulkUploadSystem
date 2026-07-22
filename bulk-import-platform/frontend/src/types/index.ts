export type ImportType = "vehicle" | "customer";

export type ImportJobStatus =
  | "Uploaded"
  | "Mapped"
  | "Validated"
  | "Importing"
  | "Completed"
  | "CompletedWithErrors"
  | "Failed"
  | "RpaInProgress"
  | "RpaCompleted"
  | "RpaCompletedWithErrors";

export type RecordStatus = "Valid" | "Warning" | "Error" | "Imported" | "Skipped";

export type ValidationSeverity = "Warning" | "Error";

export interface FieldDefinitionDto {
  systemField: string;
  isMandatory: boolean;
  dataType: string;
}

export interface UploadResultDto {
  importJobId: number;
  clientAccountId: number;
  clientAccountName: string;
  fileName: string;
  detectedHeaders: string[];
  sampleRows: Record<string, string | null>[];
  totalRows: number;
  suggestedMapping: Record<string, string>;
  systemFields: FieldDefinitionDto[];
}

export interface SaveMappingRequestDto {
  profileName?: string;
  columnMapping: Record<string, string>;
  defaultValues: Record<string, string>;
  saveAsReusableProfile: boolean;
}

export interface ValidationResultDto {
  severity: ValidationSeverity;
  fieldName: string;
  ruleCode: string;
  message: string;
}

export interface ImportRecordDto {
  id: number;
  rowNumber: number;
  mappedData: Record<string, string | null>;
  defaultsApplied: string[];
  status: RecordStatus;
  validationResults: ValidationResultDto[];
}

export interface ValidationSummaryDto {
  importJobId: number;
  status: ImportJobStatus;
  totalRecords: number;
  validRecords: number;
  warningRecords: number;
  errorRecords: number;
  records: ImportRecordDto[];
}

export interface ImportSummaryReportDto {
  importJobId: number;
  status: ImportJobStatus;
  totalRecords: number;
  importedRecords: number;
  failedRecords: number;
  warningRecords: number;
  completedAt?: string;
}

export interface UploadHistoryItemDto {
  importJobId: number;
  uploadedAt: string;
  uploadedBy: string;
  fileName: string;
  clientAccountName?: string;
  importType: "Vehicle" | "Customer";
  status: ImportJobStatus;
  totalRecords: number;
  importedRecords: number;
  failedRecords: number;
  warningRecords: number;
}

export interface DashboardSummaryDto {
  totalImportJobs: number;
  totalVehiclesImported: number;
  totalCustomersImported: number;
  jobsInProgress: number;
  jobsCompletedToday: number;
  recentJobs: UploadHistoryItemDto[];
}

export interface AuditLogDto {
  id: number;
  importJobId?: number;
  action: string;
  performedBy: string;
  timestamp: string;
  details?: string;
}

// ---------- Client Accounts (irentosoft login per client) ----------

export interface ClientAccountDto {
  id: number;
  name: string;
  irentosoftUsername: string;
  isActive: boolean;
  createdAt: string;
  createdBy?: string;
}

export interface CreateClientAccountRequestDto {
  name: string;
  irentosoftUsername: string;
  password: string;
}

export interface UpdateClientAccountRequestDto {
  name: string;
  irentosoftUsername: string;
  isActive: boolean;
}

export interface ResetClientAccountPasswordRequestDto {
  password: string;
}

// ---------- RPA push-to-irentosoft ----------

export type RpaStatus = "NotAttempted" | "InProgress" | "Succeeded" | "Failed" | "Skipped";

export interface RpaRunOptionsDto {
  headed?: boolean;
  captureScreenshotsOnFailure?: boolean;
}

export interface RpaRunRequestDto {
  // No clientAccountId here - the job already has one, assigned at upload time.
  owningLocation?: string;
  currentLocation?: string;
  options?: RpaRunOptionsDto;
}

export interface RpaRetryRequestDto {
  importRecordIds: number[];
  options?: RpaRunOptionsDto;
}

export interface UpdateRecordAndRetryRequestDto {
  fieldUpdates: Record<string, string | null>;
  options?: RpaRunOptionsDto;
}

export interface RpaRecordResultDto {
  importRecordId: number;
  rowNumber: number;
  rpaStatus: RpaStatus;
  rpaMessage?: string;
  attemptCount: number;
  lastScreenshotPath?: string;
  mappedData: Record<string, string | null>;
}

export interface RpaRunSummaryDto {
  importJobId: number;
  status: ImportJobStatus;
  totalEligible: number;
  succeeded: number;
  failed: number;
  skipped: number;
  records: RpaRecordResultDto[];
}
