import { useEffect, useState } from "react";
import { Box, Card, CardContent, Typography, Grid, Chip, Alert, CircularProgress, Stack, Paper } from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import WorkHistoryRoundedIcon from "@mui/icons-material/WorkHistoryRounded";
import DirectionsCarFilledRoundedIcon from "@mui/icons-material/DirectionsCarFilledRounded";
import PeopleAltRoundedIcon from "@mui/icons-material/PeopleAltRounded";
import HourglassBottomRoundedIcon from "@mui/icons-material/HourglassBottomRounded";
import EventAvailableRoundedIcon from "@mui/icons-material/EventAvailableRounded";
import { DashboardApi } from "../api/client";
import type { DashboardSummaryDto } from "../types";
import { statusColor } from "../components/statusColor";

export default function DashboardPage() {
  const [data, setData] = useState<DashboardSummaryDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    DashboardApi.summary()
      .then(setData)
      .catch(() => setError("Could not reach the API. Is the backend running at the configured VITE_API_BASE_URL?"))
      .finally(() => setLoading(false));
  }, []);

  const columns: GridColDef[] = [
    { field: "importJobId", headerName: "Job #", width: 80 },
    { field: "fileName", headerName: "File", flex: 1 },
    { field: "importType", headerName: "Type", width: 110 },
    {
      field: "status",
      headerName: "Status",
      width: 170,
      renderCell: (params) => <Chip size="small" label={params.value} color={statusColor(params.value)} />,
    },
    { field: "totalRecords", headerName: "Total", width: 90 },
    { field: "importedRecords", headerName: "Imported", width: 100 },
    { field: "warningRecords", headerName: "Warnings", width: 100 },
    { field: "failedRecords", headerName: "Failed", width: 90 },
    { field: "uploadedAt", headerName: "Uploaded", width: 190, valueFormatter: (v: string) => new Date(v).toLocaleString() },
  ];

  if (loading) {
    return (
      <Stack alignItems="center" justifyContent="center" sx={{ height: "60vh" }}>
        <CircularProgress />
      </Stack>
    );
  }
  if (error) return <Alert severity="warning">{error}</Alert>;
  if (!data) return null;

  const cards = [
    { label: "Total Import Jobs", value: data.totalImportJobs, icon: WorkHistoryRoundedIcon, color: "#4338ca" },
    { label: "Vehicles Imported", value: data.totalVehiclesImported, icon: DirectionsCarFilledRoundedIcon, color: "#0284c7" },
    { label: "Customers Imported", value: data.totalCustomersImported, icon: PeopleAltRoundedIcon, color: "#0d9488" },
    { label: "Jobs In Progress", value: data.jobsInProgress, icon: HourglassBottomRoundedIcon, color: "#d97706" },
    { label: "Completed Today", value: data.jobsCompletedToday, icon: EventAvailableRoundedIcon, color: "#16a34a" },
  ];

  return (
    <Box>
      <Typography variant="h5" gutterBottom>Dashboard</Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>
        A live snapshot of every bulk import job across all clients.
      </Typography>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        {cards.map((c) => (
          <Grid key={c.label} size={{ xs: 12, sm: 6, md: 2.4 }}>
            <Card>
              <CardContent>
                <Stack direction="row" spacing={1.5} alignItems="center">
                  <Box
                    sx={{
                      width: 42,
                      height: 42,
                      borderRadius: 2,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      bgcolor: `${c.color}1a`,
                      color: c.color,
                      flexShrink: 0,
                    }}
                  >
                    <c.icon fontSize="small" />
                  </Box>
                  <Box sx={{ minWidth: 0 }}>
                    <Typography variant="h5" sx={{ lineHeight: 1.1 }}>{c.value}</Typography>
                    <Typography variant="caption" color="text.secondary" noWrap>{c.label}</Typography>
                  </Box>
                </Stack>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Typography variant="subtitle1" gutterBottom>Recent Import Jobs</Typography>
      <Paper variant="outlined" sx={{ height: 440 }}>
        <DataGrid
          rows={data.recentJobs}
          columns={columns}
          getRowId={(row) => row.importJobId}
          density="compact"
          disableRowSelectionOnClick
          sx={{ border: "none" }}
        />
      </Paper>
    </Box>
  );
}
