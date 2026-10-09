import { useEffect, useMemo, useState } from "react";
import {
  Box,
  Checkbox,
  FormControlLabel,
  FormGroup,
  MenuItem,
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
  DialogContentText,
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
  // Asked once per push (vehicle imports only) and reused for Retry / Edit & Retry, so a
  // retried row gets the same make/model handling as the run it came from.
  const [askNewAccount, setAskNewAccount] = useState(false);
  const [createMissingMakesAndModels, setCreateMissingMakesAndModels] = useState(false);
  // Reservation pushes only: which source Status values to push, and API calls in flight at once.
  const [reservationStatuses, setReservationStatuses] = useState<string[] | null>(null);
  const [maxParallelRequests, setMaxParallelRequests] = useState(4);
  const isReservation = type === "reservation";
  const rpaOptions = isReservation
    ? { reservationStatuses: reservationStatuses ?? [], maxParallelRequests }
    : { createMissingMakesAndModels };

  useEffect(() => {
    RpaApi.status(importJobId).then(setSummary).catch(() => {});
  }, [importJobId]);

  // A large push can run for minutes - poll so the grid fills in as rows complete.
  useEffect(() => {
    if (!busy) return;
    const timer = setInterval(() => {
      RpaApi.status(importJobId).then(setSummary).catch(() => {});
    }, 3000);
    return () => clearInterval(timer);
  }, [busy, importJobId]);

  // Status values in the file, with how many rows still need pushing for each.
  const statusCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const r of summary?.records ?? []) {
      if (r.rpaStatus === "Succeeded" || r.rpaStatus === "Skipped") continue;
      const status = (r.mappedData["Status"] ?? "").trim();
      counts.set(status, (counts.get(status) ?? 0) + 1);
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1]);
  }, [summary]);

  // Default to open bookings only: historical rows (done/cancel/...) are rarely wanted as new reservations.
  useEffect(() => {
    if (!isReservation || reservationStatuses !== null || statusCounts.length === 0) return;
    const reserved = statusCounts.map(([s]) => s).filter((s) => s.toLowerCase() === "reserved");
    setReservationStatuses(reserved.length > 0 ? reserved : statusCounts.map(([s]) => s));
  }, [isReservation, reservationStatuses, statusCounts]);

  const toggleStatus = (status: string) =>
    setReservationStatuses((prev) =>
      (prev ?? []).includes(status) ? (prev ?? []).filter((s) => s !== status) : [...(prev ?? []), status]
    );

  const selectedCount = statusCounts
    .filter(([s]) => (reservationStatuses ?? []).includes(s))
    .reduce((sum, [, n]) => sum + n, 0);

  const handlePushClick = () => {
    if (type === "vehicle") setAskNewAccount(true);
    else handleRun(false);
  };

  const handleRun = async (createMissing: boolean) => {
    setAskNewAccount(false);
    setCreateMissingMakesAndModels(createMissing);
    setBusy(true);
    try {
      const result = await RpaApi.run(importJobId, {
        owningLocation: type === "vehicle" ? owningLocation : undefined,
        currentLocation: type === "vehicle" ? currentLocation : undefined,
        options: isReservation ? rpaOptions : { createMissingMakesAndModels: createMissing },
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
      const result = await RpaApi.retry(importJobId, { importRecordIds: [importRecordId], options: rpaOptions });
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
        options: rpaOptions,
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
      {isReservation ? (
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          Logs in to irentosoft as <strong>{clientAccountName}</strong> (chosen at upload), then creates
          each reservation through irentosoft's own booking API - the same call its Add Reservation
          screen makes - so rows take well under a second each instead of filling in forms. Customers
          are matched by phone; anyone not found is created. Bookings are created with status New and
          no emails are sent.
        </Typography>
      ) : (
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          irentosoft has no bulk-upload feature of its own - this drives its web forms directly,
          logged in as <strong>{clientAccountName}</strong> (chosen at upload), to enter every
          record committed in the previous step.
        </Typography>
      )}

      {isReservation && statusCounts.length > 0 && (
        <Box sx={{ mb: 2 }}>
          <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
            Push rows with status ({selectedCount} row(s) selected)
          </Typography>
          <FormGroup row>
            {statusCounts.map(([status, count]) => (
              <FormControlLabel
                key={status}
                control={
                  <Checkbox
                    size="small"
                    checked={(reservationStatuses ?? []).includes(status)}
                    onChange={() => toggleStatus(status)}
                    disabled={busy}
                  />
                }
                label={`${status || "(blank)"} (${count})`}
              />
            ))}
          </FormGroup>
        </Box>
      )}

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

        {isReservation && (
          <TextField
            select
            label="Parallel requests"
            value={maxParallelRequests}
            onChange={(e) => setMaxParallelRequests(Number(e.target.value))}
            disabled={busy}
            sx={{ minWidth: 160 }}
            helperText="Higher is faster; lower is gentler on irentosoft"
          >
            {[1, 2, 4, 6, 8].map((n) => (
              <MenuItem key={n} value={n}>{n}</MenuItem>
            ))}
          </TextField>
        )}

        <Button
          variant="contained"
          onClick={handlePushClick}
          disabled={busy || (isReservation && selectedCount === 0)}
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

      <Dialog open={askNewAccount} onClose={() => setAskNewAccount(false)} fullWidth maxWidth="sm">
        <DialogTitle>Is this a new irentosoft account?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            For a new account, any vehicle <strong>Make</strong> or <strong>Model</strong> in this
            file that <strong>{clientAccountName}</strong> doesn't have yet is added first, under
            Settings &rarr; Asset &rarr; Asset Make / Asset Model. Makes and models that already
            exist are never added again.
          </DialogContentText>
          <DialogContentText sx={{ mt: 2 }}>
            For an existing account, vehicles whose make or model isn't set up will fail and can
            be corrected with Edit &amp; Retry.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAskNewAccount(false)}>Cancel</Button>
          <Button onClick={() => handleRun(false)}>No, existing account</Button>
          <Button variant="contained" onClick={() => handleRun(true)}>
            Yes, add missing makes &amp; models
          </Button>
        </DialogActions>
      </Dialog>

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
