import { Box, Typography, Grid, Card, CardContent, Button, Alert } from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import type { ImportSummaryReportDto } from "../../types";

export default function ResultStep({
  result,
  onStartNew,
  onPushToIrentosoft,
}: {
  result: ImportSummaryReportDto;
  onStartNew: () => void;
  onPushToIrentosoft: () => void;
}) {
  const cards = [
    { label: "Total Records", value: result.totalRecords },
    { label: "Imported", value: result.importedRecords },
    { label: "Warnings (imported)", value: result.warningRecords },
    { label: "Failed / Rejected", value: result.failedRecords },
  ];

  return (
    <Box>
      <Alert icon={<CheckCircleIcon fontSize="inherit" />} severity={result.failedRecords > 0 ? "warning" : "success"} sx={{ mb: 3 }}>
        Import job #{result.importJobId} finished with status <strong>{result.status}</strong>
        {result.completedAt ? ` at ${new Date(result.completedAt).toLocaleString()}` : ""}.
      </Alert>

      <Grid container spacing={2} sx={{ mb: 3 }}>
        {cards.map((c) => (
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

      <Box sx={{ display: "flex", gap: 2 }}>
        <Button variant="contained" onClick={onPushToIrentosoft} disabled={result.importedRecords === 0}>
          Push to irentosoft
        </Button>
        <Button variant="outlined" onClick={onStartNew}>Start New Import</Button>
      </Box>
    </Box>
  );
}
