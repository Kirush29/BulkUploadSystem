import { Box, Typography, Chip, Button, CircularProgress, Card, CardContent, Grid, Tooltip, Stack } from "@mui/material";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import type { ValidationSummaryDto } from "../../types";
import { statusColor } from "../statusColor";

export default function PreviewStep({
  validation,
  onBack,
  onCommit,
  busy,
}: {
  validation: ValidationSummaryDto;
  onBack: () => void;
  onCommit: () => void;
  busy: boolean;
}) {
  const columns: GridColDef[] = [
    { field: "rowNumber", headerName: "Row", width: 80 },
    {
      field: "status",
      headerName: "Status",
      width: 120,
      renderCell: (params) => <Chip size="small" label={params.value} color={statusColor(params.value)} />,
    },
    {
      field: "mappedData",
      headerName: "Data",
      flex: 1,
      minWidth: 400,
      renderCell: (params) => (
        <span style={{ fontSize: 12 }}>
          {Object.entries(params.value as Record<string, string | null>)
            .filter(([, v]) => v != null && v !== "")
            .map(([k, v]) => `${k}: ${v}`)
            .join("  •  ")}
        </span>
      ),
    },
    {
      field: "validationResults",
      headerName: "Issues",
      flex: 1,
      minWidth: 300,
      renderCell: (params) => {
        const results = params.value as { severity: string; message: string }[];
        if (!results.length) return <span style={{ color: "#888" }}>No issues</span>;
        return (
          <Box sx={{ display: "flex", flexWrap: "wrap", gap: 0.5 }}>
            {results.map((r, i) => (
              <Tooltip key={i} title={r.message}>
                <Chip
                  size="small"
                  label={r.message}
                  color={r.severity === "Error" ? "error" : "warning"}
                  variant="outlined"
                  sx={{ mb: 0.5 }}
                />
              </Tooltip>
            ))}
          </Box>
        );
      },
    },
  ];

  const summaryCards = [
    { label: "Total Records", value: validation.totalRecords, color: "default" as const },
    { label: "Valid", value: validation.validRecords, color: "success" as const },
    { label: "Warnings", value: validation.warningRecords, color: "warning" as const },
    { label: "Critical Errors", value: validation.errorRecords, color: "error" as const },
  ];

  const importableCount = validation.validRecords + validation.warningRecords;

  return (
    <Box>
      <Typography variant="h6" gutterBottom>Step 3: Validation Results & Preview</Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Review every row before committing. Records with critical errors are excluded automatically;
        records with only warnings can still be imported.
      </Typography>

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

      <Box sx={{ height: 480, mb: 3 }}>
        <DataGrid
          rows={validation.records}
          columns={columns}
          getRowId={(row) => row.id}
          density="compact"
          disableRowSelectionOnClick
          getRowHeight={() => "auto"}
        />
      </Box>

      <Stack direction="row" spacing={2}>
        <Button onClick={onBack}>Back to Mapping</Button>
        <Button
          variant="contained"
          color="primary"
          onClick={onCommit}
          disabled={busy || importableCount === 0}
          startIcon={busy ? <CircularProgress size={18} color="inherit" /> : undefined}
        >
          Import {importableCount} Record{importableCount === 1 ? "" : "s"}
        </Button>
      </Stack>
    </Box>
  );
}
