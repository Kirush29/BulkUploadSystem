import { useEffect, useState } from "react";
import {
  Box,
  Typography,
  Button,
  Chip,
  CircularProgress,
  Alert,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
  Stack,
  Paper,
} from "@mui/material";
import AddRoundedIcon from "@mui/icons-material/AddRounded";
import { DataGrid, type GridColDef } from "@mui/x-data-grid";
import { useSnackbar } from "notistack";
import { ClientAccountsApi } from "../api/client";
import type { ClientAccountDto } from "../types";

export default function ClientAccountsPage() {
  const { enqueueSnackbar } = useSnackbar();
  const [rows, setRows] = useState<ClientAccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [createName, setCreateName] = useState("");
  const [createUsername, setCreateUsername] = useState("");
  const [createPassword, setCreatePassword] = useState("");

  const [resetTarget, setResetTarget] = useState<ClientAccountDto | null>(null);
  const [resetPassword, setResetPassword] = useState("");

  const [busy, setBusy] = useState(false);

  const load = () => {
    setLoading(true);
    ClientAccountsApi.list()
      .then(setRows)
      .catch(() => setError("Could not load client accounts."))
      .finally(() => setLoading(false));
  };

  useEffect(load, []);

  const handleCreate = async () => {
    setBusy(true);
    try {
      await ClientAccountsApi.create({
        name: createName,
        irentosoftUsername: createUsername,
        password: createPassword,
      });
      enqueueSnackbar("Client account created", { variant: "success" });
      setCreateOpen(false);
      setCreateName("");
      setCreateUsername("");
      setCreatePassword("");
      load();
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const handleToggleActive = async (account: ClientAccountDto) => {
    try {
      await ClientAccountsApi.update(account.id, {
        name: account.name,
        irentosoftUsername: account.irentosoftUsername,
        isActive: !account.isActive,
      });
      load();
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    }
  };

  const handleResetPassword = async () => {
    if (!resetTarget) return;
    setBusy(true);
    try {
      await ClientAccountsApi.resetPassword(resetTarget.id, { password: resetPassword });
      enqueueSnackbar("Password updated", { variant: "success" });
      setResetTarget(null);
      setResetPassword("");
    } catch (e) {
      enqueueSnackbar(extractError(e), { variant: "error" });
    } finally {
      setBusy(false);
    }
  };

  const columns: GridColDef[] = [
    { field: "name", headerName: "Client", flex: 1 },
    { field: "irentosoftUsername", headerName: "irentosoft Login", flex: 1 },
    {
      field: "isActive",
      headerName: "Status",
      width: 120,
      renderCell: (params) => (
        <Chip
          size="small"
          label={params.value ? "Active" : "Inactive"}
          color={params.value ? "success" : "default"}
          onClick={() => handleToggleActive(params.row as ClientAccountDto)}
        />
      ),
    },
    { field: "createdAt", headerName: "Created", width: 190, valueFormatter: (v: string) => new Date(v).toLocaleString() },
    {
      field: "actions",
      headerName: "",
      width: 160,
      sortable: false,
      renderCell: (params) => (
        <Button size="small" onClick={() => setResetTarget(params.row as ClientAccountDto)}>
          Reset Password
        </Button>
      ),
    },
  ];

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1 }}>
        <Typography variant="h5">Client Accounts</Typography>
        <Button variant="contained" startIcon={<AddRoundedIcon />} onClick={() => setCreateOpen(true)}>
          New Client Account
        </Button>
      </Box>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Each client's own irentosoft login, used by the "Push to irentosoft" wizard step to enter
        their Vehicle/Customer records under the correct account.
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

      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>New Client Account</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Client Name" value={createName} onChange={(e) => setCreateName(e.target.value)} fullWidth autoFocus />
            <TextField label="irentosoft Username" value={createUsername} onChange={(e) => setCreateUsername(e.target.value)} fullWidth />
            <TextField label="irentosoft Password" type="password" value={createPassword} onChange={(e) => setCreatePassword(e.target.value)} fullWidth />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>Cancel</Button>
          <Button variant="contained" disabled={busy || !createName || !createUsername || !createPassword} onClick={handleCreate}>
            Create
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={!!resetTarget} onClose={() => setResetTarget(null)} fullWidth maxWidth="sm">
        <DialogTitle>Reset Password for {resetTarget?.name}</DialogTitle>
        <DialogContent>
          <TextField
            label="New irentosoft Password"
            type="password"
            value={resetPassword}
            onChange={(e) => setResetPassword(e.target.value)}
            fullWidth
            autoFocus
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetTarget(null)}>Cancel</Button>
          <Button variant="contained" disabled={busy || !resetPassword} onClick={handleResetPassword}>
            Update Password
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
