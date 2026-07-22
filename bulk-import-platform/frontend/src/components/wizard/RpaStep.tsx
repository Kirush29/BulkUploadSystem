import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Button,
  Chip,
  CircularProgress,
  Card,
  CardContent,
  Grid,
  Stack,
  TextField,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
} from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { useSnackbar } from "notistack";
import { RpaApi } from "../../api/client";
import type { ImportType, RpaRecordResultDto, RpaRunSummaryDto } from "../../types";
import { statusColor } from "../statusColor";

export default function RpaStep({
  type,
  importJobId,
  clientAccountName,
  onStartNew,
}: {
  type: ImportType;
  importJobId: number;
  clientAccountName: string;
  onStartNew: () => void;
}) {
  const { enqueueSnackbar } = useSnackbar();
  const [owningLocation, setOwningLocation] = useState("");
  const [currentLocation, setCurrentLocation] = useState("");
  const [summary, setSummary] = useState<RpaRunSummaryDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [editTarget, setEditTarget] = useState<RpaRecordResultDto | null>(null);
  const [editValues, setEditValues] = useState<Record<string, string | null>>({});

  useEffect(() => {
    RpaApi.status(importJobId).then(setSummary).catch(() => {});
  }, [importJobId]);

  const handleRun = async () => {
    setBusy(true);
    try {
      const result = await RpaApi.run(importJobId, {
        owningLocation: type === "vehicle" ? owningLocation : undefined,
        currentLocation: type === "vehicle" ? currentLocation : undefined,
      });
      setSummary(result);
      enqueueSnackbar(
        result.failed > 0 ? `Push finished with ${result.failed} failure(s)` : "Push to irentosoft completed",
        { variant: result.failed > 0 ? "warning" : "success" }
      );
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const handleRetry = async (importRecordId: number) => {
    setBusy(true);
    try {
      const result = await RpaApi.retry(importJobId, { importRecordIds: [importRecordId] });
      setSummary(result);
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const openEdit = (record: RpaRecordResultDto) => {
    setEditTarget(record);
    setEditValues(record.mappedData);
  };

  const handleSaveAndRetry = async () => {
    if (!editTarget) return;
    setBusy(true);
    try {
      const result = await RpaApi.updateRecordAndRetry(importJobId, editTarget.importRecordId, {
        fieldUpdates: editValues,
      });
      setSummary(result);
      setEditTarget(null);
      const updated = result.records.find((r) => r.importRecordId === editTarget.importRecordId);
      enqueueSnackbar(
        updated?.rpaStatus === "Succeeded" ? "Row corrected and pushed successfully" : "Still failing - see Details",
        { variant: updated?.rpaStatus === "Succeeded" ? "success" : "warning" }
      );
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const columns: GridColDef[] = [
    { field: "rowNumber", headerName: "Row", width: 80 },
    {
      field: "rpaStatus",
      headerName: "Status",
      width: 140,
      renderCell: (params) => <Chip size="small" label={params.value} color={statusColor(params.value)} />,
    },
    { field: "rpaMessage", headerName: "Details", flex: 1, minWidth: 300 },
    { field: "attemptCount", headerName: "Attempts", width: 100 },
    {
      field: "lastScreenshotPath",
      headerName: "",
      width: 120,
      sortable: false,
      renderCell: (params) =>
        params.value ? (
          <Button size="small" href={RpaApi.screenshotUrl(params.value as string)} target="_blank" rel="noopener">
            Screenshot
          </Button>
        ) : null,
    },
    {
      field: "actions",
      headerName: "",
      width: 200,
      sortable: false,
      renderCell: (params) =>
        params.row.rpaStatus === "Failed" ? (
          <Stack direction="row" spacing={1}>
            <Button size="small" disabled={busy} onClick={() => handleRetry(params.row.importRecordId)}>
              Retry
            </Button>
            <Button size="small" disabled={busy} onClick={() => openEdit(params.row as RpaRecordResultDto)}>
              Edit &amp; Retry
            </Button>
          </Stack>
        ) : null,
    },
  ];

  const summaryCards = summary
    ? [
        { label: "Eligible", value: summary.totalEligible },
        { label: "Succeeded", value: summary.succeeded },
        { label: "Failed", value: summary.failed },
      ]
    : [];

  const alreadyRun = !!summary && (summary.succeeded > 0 || summary.failed > 0);

  return (
    <Box>
      <Typography variant="h6" gutterBottom>Step 5: Push to irentosoft</Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        irentosoft has no bulk-upload feature of its own - this drives its web forms directly,
        logged in as <strong>{clientAccountName}</strong> (chosen at upload), to enter every
        record committed in the previous step.
      </Typography>

      <Stack direction="row" spacing={2} sx={{ mb: 2, flexWrap: "wrap" }}>
        {type === "vehicle" && (
          <>
            <TextField
              label="Owning Location"
              value={owningLocation}
              onChange={(e) => setOwningLocation(e.target.value)}
              disabled={busy}
            />
            <TextField
              label="Current Location"
              value={currentLocation}
              onChange={(e) => setCurrentLocation(e.target.value)}
              disabled={busy}
            />
          </>
        )}

        <Button
          variant="contained"
          onClick={handleRun}
          disabled={busy}
          startIcon={busy ? <CircularProgress size={18} color="inherit" /> : undefined}
        >
          {alreadyRun ? "Run Again" : "Push to irentosoft"}
        </Button>
      </Stack>

      {summary && (
        <>
          <Grid container spacing={2} sx={{ mb: 3 }}>
            {summaryCards.map((c) => (
              <Grid key={c.label} size={{ xs: 6, sm: 3 }}>
                <Card variant="outlined">
                  <CardContent>
                    <Typography variant="body2" color="text.secondary">{c.label}</Typography>
                    <Typography variant="h5">{c.value}</Typography>
                  </CardContent>
                </Card>
              </Grid>
            ))}
          </Grid>

          <Box sx={{ height: 420, mb: 3 }}>
            <DataGrid
              rows={summary.records}
              columns={columns}
              getRowId={(row) => row.importRecordId}
              density="compact"
              disableRowSelectionOnClick
              getRowHeight={() => "auto"}
            />
          </Box>
        </>
      )}

      <Button variant="outlined" onClick={onStartNew}>Start New Import</Button>

      <Dialog open={!!editTarget} onClose={() => setEditTarget(null)} fullWidth maxWidth="sm">
        <DialogTitle>Correct Row {editTarget?.rowNumber} and Retry</DialogTitle>
        <DialogContent>
          {editTarget?.rpaMessage && (
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              Last failure: {editTarget.rpaMessage}
            </Typography>
          )}
          <Stack spacing={2} sx={{ mt: 1 }}>
            {Object.entries(editValues).map(([field, value]) => (
              <TextField
                key={field}
                label={field}
                value={value ?? ""}
                onChange={(e) => setEditValues((prev) => ({ ...prev, [field]: e.target.value }))}
                fullWidth
                disabled={busy}
              />
            ))}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditTarget(null)} disabled={busy}>Cancel</Button>
          <Button
            variant="contained"
            onClick={handleSaveAndRetry}
            disabled={busy}
            startIcon={busy ? <CircularProgress size={18} color="inherit" /> : undefined}
          >
            Save &amp; Retry
          </Button>
        </DialogActions>
      </Dialog>
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
