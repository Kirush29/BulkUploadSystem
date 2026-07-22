import {
  Box,
  Typography,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Select,
  MenuItem,
  Chip,
  Button,
  Stack,
  TextField,
  CircularProgress,
} from "@mui/material";
import type { UploadResultDto } from "../../types";

export default function MappingStep({
  uploadResult,
  columnMapping,
  setColumnMapping,
  defaultValues,
  setDefaultValues,
  onBack,
  onNext,
  busy,
}: {
  uploadResult: UploadResultDto;
  columnMapping: Record<string, string>;
  setColumnMapping: (m: Record<string, string>) => void;
  defaultValues: Record<string, string>;
  setDefaultValues: (m: Record<string, string>) => void;
  onBack: () => void;
  onNext: () => void;
  busy: boolean;
}) {
  const mandatoryFields = uploadResult.systemFields.filter((f) => f.isMandatory).map((f) => f.systemField);
  const mappedSystemFields = new Set(Object.values(columnMapping).filter(Boolean));
  const missingMandatory = mandatoryFields.filter((f) => !mappedSystemFields.has(f));

  const updateMapping = (column: string, systemField: string) => {
    const next = { ...columnMapping };
    if (systemField === "") {
      delete next[column];
    } else {
      next[column] = systemField;
    }
    setColumnMapping(next);
  };

  const updateDefault = (systemField: string, value: string) => {
    const next = { ...defaultValues };
    if (value === "") {
      delete next[systemField];
    } else {
      next[systemField] = value;
    }
    setDefaultValues(next);
  };

  return (
    <Box>
      <Typography variant="h6" gutterBottom>Step 2: Field Mapping</Typography>
      <Typography color="text.secondary" sx={{ mb: 2 }}>
        Align each column from your spreadsheet with a system field. Columns were pre-mapped where possible;
        adjust or leave a column unmapped to ignore it.
      </Typography>

      <Table size="small" sx={{ mb: 3 }}>
        <TableHead>
          <TableRow>
            <TableCell>Your Column</TableCell>
            <TableCell>Sample Value</TableCell>
            <TableCell>Maps To (System Field)</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {uploadResult.detectedHeaders.map((header) => (
            <TableRow key={header}>
              <TableCell>{header}</TableCell>
              <TableCell sx={{ color: "text.secondary" }}>
                {uploadResult.sampleRows[0]?.[header] ?? "—"}
              </TableCell>
              <TableCell>
                <Select
                  size="small"
                  fullWidth
                  value={columnMapping[header] ?? ""}
                  onChange={(e) => updateMapping(header, e.target.value)}
                  displayEmpty
                >
                  <MenuItem value="">(Not mapped / ignore column)</MenuItem>
                  {uploadResult.systemFields.map((f) => (
                    <MenuItem key={f.systemField} value={f.systemField}>
                      {f.systemField}{f.isMandatory ? " *" : ""}
                    </MenuItem>
                  ))}
                </Select>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      {missingMandatory.length > 0 && (
        <Box sx={{ mb: 2 }}>
          {missingMandatory.map((f) => (
            <Chip key={f} label={`Unmapped mandatory field: ${f}`} color="error" size="small" sx={{ mr: 1, mb: 1 }} />
          ))}
        </Box>
      )}

      <Typography variant="subtitle1" gutterBottom>Default Values (optional)</Typography>
      <Typography color="text.secondary" sx={{ mb: 1 }}>
        Applied automatically when a field is left blank on a row. Leave blank to use the platform's built-in defaults
        (e.g. Year → current year, Tank Size → 0, Customer Status → Active).
      </Typography>
      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, mb: 3 }}>
        {uploadResult.systemFields.filter((f) => !f.isMandatory).map((f) => (
          <TextField
            key={f.systemField}
            label={`${f.systemField} default`}
            size="small"
            value={defaultValues[f.systemField] ?? ""}
            onChange={(e) => updateDefault(f.systemField, e.target.value)}
            sx={{ width: 220 }}
          />
        ))}
      </Box>

      <Stack direction="row" spacing={2}>
        <Button onClick={onBack}>Back</Button>
        <Button
          variant="contained"
          onClick={onNext}
          disabled={busy}
          startIcon={busy ? <CircularProgress size={18} color="inherit" /> : undefined}
        >
          Validate & Preview
        </Button>
      </Stack>
    </Box>
  );
}
