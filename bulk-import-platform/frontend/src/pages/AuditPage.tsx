import { useEffect, useState } from "react";
import { Box, Typography, CircularProgress, Alert, Paper, Stack } from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { AuditApi } from "../api/client";
import type { AuditLogDto } from "../types";

export default function AuditPage() {
  const [rows, setRows] = useState<AuditLogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    AuditApi.list()
      .then(setRows)
      .catch(() => setError("Could not load audit logs."))
      .finally(() => setLoading(false));
  }, []);

  const columns: GridColDef[] = [
    { field: "id", headerName: "#", width: 70 },
    { field: "importJobId", headerName: "Job #", width: 90 },
    { field: "action", headerName: "Action", width: 200 },
    { field: "performedBy", headerName: "User", width: 140 },
    { field: "timestamp", headerName: "Timestamp", width: 190, valueFormatter: (v: string) => new Date(v).toLocaleString() },
    { field: "details", headerName: "Details", flex: 1 },
  ];

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Audit Log</Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>
        Every upload, mapping change, validation run, and import commit is recorded here for traceability.
      </Typography>
      {error && <Alert severity="warning" sx={{ mb: 2 }}>{error}</Alert>}
      {loading ? (
        <Stack alignItems="center" justifyContent="center" sx={{ height: "40vh" }}>
          <CircularProgress />
        </Stack>
      ) : (
        <Paper variant="outlined" sx={{ height: 560 }}>
          <DataGrid rows={rows} columns={columns} getRowId={(row) => row.id} density="compact" sx={{ border: "none" }} />
        </Paper>
      )}
    </Box>
  );
}
