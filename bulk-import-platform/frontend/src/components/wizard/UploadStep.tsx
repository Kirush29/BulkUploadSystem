import { useEffect, useRef, useState } from "react";
import { Box, Button, Typography, CircularProgress, Alert, TextField, MenuItem, Stack } from "@mui/material";
import UploadFileIcon from "@mui/icons-material/UploadFileRounded";
import DownloadIcon from "@mui/icons-material/DownloadRounded";
import DescriptionIcon from "@mui/icons-material/DescriptionRounded";
import { useSnackbar } from "notistack";
import type { ImportType, UploadResultDto, ClientAccountDto } from "../../types";
import { ImportsApi, ClientAccountsApi } from "../../api/client";

export default function UploadStep({
  type,
  onUploaded,
}: {
  type: ImportType;
  onUploaded: (res: UploadResultDto) => void;
}) {
  const { enqueueSnackbar } = useSnackbar();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [accounts, setAccounts] = useState<ClientAccountDto[]>([]);
  const [clientAccountId, setClientAccountId] = useState<number | "">("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragOver, setDragOver] = useState(false);

  useEffect(() => {
    ClientAccountsApi.list()
      .then((all) => setAccounts(all.filter((a) => a.isActive)))
      .catch(() => setError("Could not load client accounts."));
  }, []);

  const handleFile = async (file: File) => {
    if (!clientAccountId) return;
    setBusy(true);
    setError(null);
    try {
      const result = await ImportsApi.upload(type, clientAccountId, file);
      enqueueSnackbar(`Parsed ${result.totalRows} rows from ${result.fileName}`, { variant: "success" });
      onUploaded(result);
    } catch (e) {
      const message =
        e && typeof e === "object" && "response" in e
          ? (e as { response?: { data?: { message?: string } } }).response?.data?.message
          : undefined;
      setError(message || "Upload failed. Please check the file and try again.");
    } finally {
      setBusy(false);
    }
  };

  const canDrop = !!clientAccountId && !busy;

  return (
    <Box>
      <Typography variant="h6" gutterBottom>Step 1: Upload File</Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>
        Pick which client this data belongs to, then upload a .xlsx, .xlsm, or .csv file containing {type} records.
        The first row must contain column headers. Not sure of the expected columns? Download the template below.
        Duplicate checks (e.g. an existing Vehicle Number) are checked only against this client's own data -
        other clients may have the same values.
      </Typography>

      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {accounts.length === 0 && !error && (
        <Alert severity="info" sx={{ mb: 2 }}>
          No active client accounts yet - add one under "Client Accounts" before uploading.
        </Alert>
      )}

      <TextField
        select
        label="Client Account"
        value={clientAccountId}
        onChange={(e) => setClientAccountId(e.target.value === "" ? "" : Number(e.target.value))}
        sx={{ minWidth: 260, mb: 3, display: "block" }}
        disabled={busy}
      >
        {accounts.map((a) => (
          <MenuItem key={a.id} value={a.id}>{a.name}</MenuItem>
        ))}
      </TextField>

      <Box
        onDragOver={(e) => {
          e.preventDefault();
          if (canDrop) setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragOver(false);
          const file = e.dataTransfer.files?.[0];
          if (file && canDrop) handleFile(file);
        }}
        onClick={() => canDrop && fileInputRef.current?.click()}
        sx={{
          border: "2px dashed",
          borderColor: dragOver ? "primary.main" : "divider",
          borderRadius: 3,
          bgcolor: dragOver ? "action.hover" : "background.default",
          p: 5,
          textAlign: "center",
          cursor: canDrop ? "pointer" : "not-allowed",
          opacity: canDrop ? 1 : 0.6,
          transition: "border-color .15s ease, background-color .15s ease",
        }}
      >
        <input
          ref={fileInputRef}
          type="file"
          hidden
          accept=".xlsx,.xlsm,.csv"
          onChange={(e) => {
            const file = e.target.files?.[0];
            if (file) handleFile(file);
            e.target.value = "";
          }}
        />

        {busy ? (
          <Stack alignItems="center" spacing={1.5}>
            <CircularProgress size={32} />
            <Typography color="text.secondary">Uploading and parsing your file…</Typography>
          </Stack>
        ) : (
          <Stack alignItems="center" spacing={1.5}>
            <Box
              sx={{
                width: 56,
                height: 56,
                borderRadius: "50%",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                bgcolor: "primary.main",
                color: "primary.contrastText",
              }}
            >
              <UploadFileIcon />
            </Box>
            <Typography variant="subtitle1">
              Drag & drop your Excel or CSV file here
            </Typography>
            <Typography variant="body2" color="text.secondary">
              or click to browse · .xlsx / .xlsm / .csv
            </Typography>
            <Button variant="contained" disabled={!canDrop} sx={{ mt: 1 }}>
              Choose File
            </Button>
            {!clientAccountId && (
              <Typography variant="caption" color="text.secondary">
                Select a client account above to enable upload.
              </Typography>
            )}
          </Stack>
        )}
      </Box>

      <Button
        variant="text"
        startIcon={<DownloadIcon />}
        href={ImportsApi.templateUrl(type)}
        target="_blank"
        sx={{ mt: 2 }}
      >
        Download {type === "vehicle" ? "Vehicle" : "Customer"} Template
      </Button>

      <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 1, color: "text.secondary" }}>
        <DescriptionIcon fontSize="small" />
        <Typography variant="caption">
          Column headers don't need to match exactly - you'll map them to system fields in the next step.
        </Typography>
      </Stack>
    </Box>
  );
}
