import { useState } from "react";
import {
  Box,
  Typography,
  Stepper,
  Step,
  StepLabel,
  StepConnector,
  stepConnectorClasses,
  Button,
  Paper,
  Alert,
  Stack,
} from "@mui/material";
import { styled } from "@mui/material/styles";
import DirectionsCarFilledRoundedIcon from "@mui/icons-material/DirectionsCarFilledRounded";
import PeopleAltRoundedIcon from "@mui/icons-material/PeopleAltRounded";
import EventAvailableRoundedIcon from "@mui/icons-material/EventAvailableRounded";
import UploadFileRoundedIcon from "@mui/icons-material/UploadFileRounded";
import RuleFolderRoundedIcon from "@mui/icons-material/RuleFolderRounded";
import FactCheckRoundedIcon from "@mui/icons-material/FactCheckRounded";
import TaskAltRoundedIcon from "@mui/icons-material/TaskAltRounded";
import RocketLaunchRoundedIcon from "@mui/icons-material/RocketLaunchRounded";
import { useSnackbar } from "notistack";
import type { ImportType, UploadResultDto, ValidationSummaryDto, ImportSummaryReportDto } from "../types";
import { ImportsApi } from "../api/client";
import UploadStep from "../components/wizard/UploadStep";
import MappingStep from "../components/wizard/MappingStep";
import PreviewStep from "../components/wizard/PreviewStep";
import ResultStep from "../components/wizard/ResultStep";
import RpaStep from "../components/wizard/RpaStep";

const steps = [
  { label: "Upload", icon: UploadFileRoundedIcon },
  { label: "Field Mapping", icon: RuleFolderRoundedIcon },
  { label: "Validate & Preview", icon: FactCheckRoundedIcon },
  { label: "Import Result", icon: TaskAltRoundedIcon },
  { label: "Push to irentosoft", icon: RocketLaunchRoundedIcon },
];

const Connector = styled(StepConnector)(({ theme }) => ({
  [`&.${stepConnectorClasses.alternativeLabel}`]: { top: 18 },
  [`& .${stepConnectorClasses.line}`]: {
    borderTopWidth: 2,
    borderColor: theme.palette.divider,
  },
  [`&.${stepConnectorClasses.active} .${stepConnectorClasses.line}`]: {
    borderColor: theme.palette.primary.main,
  },
  [`&.${stepConnectorClasses.completed} .${stepConnectorClasses.line}`]: {
    borderColor: theme.palette.primary.main,
  },
}));

function StepIcon({ icon: Icon, active, completed }: { icon: typeof UploadFileRoundedIcon; active: boolean; completed: boolean }) {
  return (
    <Box
      sx={{
        width: 36,
        height: 36,
        borderRadius: "50%",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        bgcolor: active || completed ? "primary.main" : "action.selected",
        color: active || completed ? "primary.contrastText" : "text.secondary",
        transition: "background-color .2s ease",
      }}
    >
      <Icon fontSize="small" />
    </Box>
  );
}

export default function ImportWizardPage({ type }: { type: ImportType }) {
  const { enqueueSnackbar } = useSnackbar();
  const [activeStep, setActiveStep] = useState(0);
  const [uploadResult, setUploadResult] = useState<UploadResultDto | null>(null);
  const [columnMapping, setColumnMapping] = useState<Record<string, string>>({});
  const [defaultValues, setDefaultValues] = useState<Record<string, string>>({});
  const [validation, setValidation] = useState<ValidationSummaryDto | null>(null);
  const [result, setResult] = useState<ImportSummaryReportDto | null>(null);
  const [busy, setBusy] = useState(false);

  const reset = () => {
    setActiveStep(0);
    setUploadResult(null);
    setColumnMapping({});
    setDefaultValues({});
    setValidation(null);
    setResult(null);
  };

  const handleUploaded = (res: UploadResultDto) => {
    setUploadResult(res);
    setColumnMapping(res.suggestedMapping);
    setActiveStep(1);
  };

  const handleValidate = async () => {
    if (!uploadResult) return;
    setBusy(true);
    try {
      const summary = await ImportsApi.validate(type, uploadResult.importJobId, {
        columnMapping,
        defaultValues,
        saveAsReusableProfile: false,
      });
      setValidation(summary);
      setActiveStep(2);
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const handleCommit = async () => {
    if (!uploadResult) return;
    setBusy(true);
    try {
      const summary = await ImportsApi.commit(type, uploadResult.importJobId);
      setResult(summary);
      setActiveStep(3);
      enqueueSnackbar("Import completed", { variant: "success" });
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const label = type.charAt(0).toUpperCase() + type.slice(1);
  const Icon =
    type === "vehicle" ? DirectionsCarFilledRoundedIcon : type === "reservation" ? EventAvailableRoundedIcon : PeopleAltRoundedIcon;

  return (
    <Box>
      <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 3 }}>
        <Box
          sx={{
            width: 44,
            height: 44,
            borderRadius: 2.5,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            background: "linear-gradient(135deg, #4338ca, #6366f1)",
            color: "#fff",
          }}
        >
          <Icon />
        </Box>
        <Box>
          <Typography variant="h5">{label} Bulk Import</Typography>
          <Typography variant="body2" color="text.secondary">
            Upload, map, validate, and push {type} records to irentosoft.
          </Typography>
        </Box>
      </Stack>

      <Paper variant="outlined" sx={{ p: { xs: 2, sm: 3 }, mb: 3 }}>
        <Stepper activeStep={activeStep} connector={<Connector />} sx={{ mb: 4 }}>
          {steps.map((s, i) => (
            <Step key={s.label}>
              <StepLabel
                StepIconComponent={() => (
                  <StepIcon icon={s.icon} active={i === activeStep} completed={i < activeStep} />
                )}
              >
                <Typography variant="body2" sx={{ fontWeight: i === activeStep ? 700 : 500 }}>
                  {s.label}
                </Typography>
              </StepLabel>
            </Step>
          ))}
        </Stepper>

        {activeStep === 0 && <UploadStep type={type} onUploaded={handleUploaded} />}

        {activeStep === 1 && uploadResult && (
          <MappingStep
            uploadResult={uploadResult}
            columnMapping={columnMapping}
            setColumnMapping={setColumnMapping}
            defaultValues={defaultValues}
            setDefaultValues={setDefaultValues}
            onBack={() => setActiveStep(0)}
            onNext={handleValidate}
            busy={busy}
          />
        )}

        {activeStep === 2 && validation && (
          <PreviewStep
            validation={validation}
            onBack={() => setActiveStep(1)}
            onCommit={handleCommit}
            busy={busy}
          />
        )}

        {activeStep === 3 && result && (
          <ResultStep result={result} onStartNew={reset} onPushToIrentosoft={() => setActiveStep(4)} />
        )}

        {activeStep === 4 && uploadResult && (
          <RpaStep
            type={type}
            importJobId={uploadResult.importJobId}
            clientAccountName={uploadResult.clientAccountName}
            onStartNew={reset}
          />
        )}
      </Paper>

      {activeStep === 2 && validation && validation.errorRecords > 0 && (
        <Alert severity="warning" sx={{ mt: 2 }}>
          {validation.errorRecords} record(s) have critical errors and will be excluded from the import.
          Valid and warning records ({validation.validRecords + validation.warningRecords}) can still proceed.
        </Alert>
      )}

      {activeStep > 0 && activeStep < 3 && (
        <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
          <Button color="inherit" onClick={reset}>Cancel & Start Over</Button>
        </Box>
      )}
    </Box>
  );
}

function extractError(e: unknown): string {
  if (e && typeof e === "object" && "response" in e) {
    const resp = (e as { response?: { data?: { message?: string } } }).response;
    if (resp?.data?.message) return resp.data.message;
  }
  return "Something went wrong. Please try again.";
}
