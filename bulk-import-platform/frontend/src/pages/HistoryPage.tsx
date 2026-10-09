import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  ToggleButtonGroup,
  ToggleButton,
  Chip,
  CircularProgress,
  Alert,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  Paper,
  Stack,
} from "@mui/material";
import RocketLaunchRoundedIcon from "@mui/icons-material/RocketLaunchRounded";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { ImportsApi } from "../api/client";
import type { ImportType, UploadHistoryItemDto } from "../types";
import { statusColor } from "../components/statusColor";
import RpaStep from "../components/wizard/RpaStep";

// Jobs at or past this point have local Vehicle/Customer rows committed - the "Push to
// irentosoft" step can always be (re)opened for them, whether it was never started, partially
// failed, or fully finished (RpaStep itself offers "Run Again" once a summary exists).
const RPA_RESUMABLE_STATUSES = new Set([
  "Completed",
  "CompletedWithErrors",
  "RpaInProgress",
  "RpaCompleted",
  "RpaCompletedWithErrors",
]);

export default function HistoryPage() {
  const [type, setType] = useState<ImportType>("vehicle");
  const [rows, setRows] = useState<UploadHistoryItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [rpaTarget, setRpaTarget] = useState<UploadHistoryItemDto | null>(null);

  useEffect(() => {
    setLoading(true);
    ImportsApi.history(type)
      .then(setRows)
      .catch(() => setError("Could not load upload history."))
      .finally(() => setLoading(false));
  }, [type]);

  const columns: GridColDef[] = [
    { field: "importJobId", headerName: "Job #", width: 80 },
    { field: "fileName", headerName: "File Name", flex: 1 },
    { field: "clientAccountName", headerName: "Client", width: 160 },
    { field: "uploadedBy", headerName: "User", width: 140 },
    { field: "uploadedAt", headerName: "Upload Date", width: 190, valueFormatter: (v: string) => new Date(v).toLocaleString() },
    {
      field: "status",
      headerName: "Processing Status",
      width: 180,
      renderCell: (params) => <Chip size="small" label={params.value} color={statusColor(params.value)} />,
    },
    { field: "totalRecords", headerName: "Total", width: 90 },
    { field: "importedRecords", headerName: "Imported", width: 100 },
    { field: "warningRecords", headerName: "Warnings", width: 100 },
    { field: "failedRecords", headerName: "Failed", width: 90 },
    {
      field: "actions",
      headerName: "",
      width: 160,
      sortable: false,
      renderCell: (params) =>
        RPA_RESUMABLE_STATUSES.has(params.row.status) ? (
          <Button
            size="small"
            startIcon={<RocketLaunchRoundedIcon fontSize="small" />}
            onClick={() => setRpaTarget(params.row as UploadHistoryItemDto)}
          >
            Push to irentosoft
          </Button>
        ) : null,
    },
  ];

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Upload History</Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Every file uploaded so far. Resume the irentosoft push for any past job without re-uploading.
      </Typography>
      <ToggleButtonGroup
        value={type}
        exclusive
        onChange={(_, v) => v && setType(v)}
        sx={{ mb: 2 }}
        size="small"
      >
        <ToggleButton value="vehicle">Vehicle</ToggleButton>
        <ToggleButton value="customer">Customer</ToggleButton>
        <ToggleButton value="reservation">Reservation</ToggleButton>
      </ToggleButtonGroup>

      {error && <Alert severity="warning" sx={{ mb: 2 }}>{error}</Alert>}
      {loading ? (
        <Stack alignItems="center" justifyContent="center" sx={{ height: "40vh" }}>
          <CircularProgress />
        </Stack>
      ) : (
        <Paper variant="outlined" sx={{ height: 560 }}>
          <DataGrid
            rows={rows}
            columns={columns}
            getRowId={(row) => row.importJobId}
            density="compact"
            sx={{ border: "none" }}
          />
        </Paper>
      )}

      <Dialog open={!!rpaTarget} onClose={() => setRpaTarget(null)} fullWidth maxWidth="lg">
        <DialogTitle>Job #{rpaTarget?.importJobId} - {rpaTarget?.fileName}</DialogTitle>
        <DialogContent>
          {rpaTarget && (
            <RpaStep
              type={type}
              importJobId={rpaTarget.importJobId}
              clientAccountName={rpaTarget.clientAccountName ?? "Unknown client"}
              onStartNew={() => setRpaTarget(null)}
            />
          )}
        </DialogContent>
      </Dialog>
    </Box>
  );
}
