import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { ThemeProvider, createTheme, CssBaseline } from "@mui/material";
import { SnackbarProvider } from "notistack";
import App from "./App.tsx";

const theme = createTheme({
  palette: {
    mode: "light",
    primary: { main: "#4338ca", light: "#6366f1", dark: "#312e81", contrastText: "#fff" },
    secondary: { main: "#0d9488" },
    background: { default: "#f4f5fb", paper: "#ffffff" },
    text: { primary: "#1e1b2e", secondary: "#635e78" },
    divider: "#e6e3f1",
    success: { main: "#16a34a" },
    warning: { main: "#d97706" },
    error: { main: "#dc2626" },
    info: { main: "#0284c7" },
  },
  shape: { borderRadius: 12 },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica Neue", Arial, sans-serif',
    h4: { fontWeight: 700, letterSpacing: -0.3 },
    h5: { fontWeight: 700 },
    h6: { fontWeight: 700 },
    subtitle1: { fontWeight: 600 },
    button: { textTransform: "none", fontWeight: 600 },
  },
  shadows: [
    "none",
    "0 1px 2px 0 rgb(30 27 46 / 0.06)",
    "0 1px 3px 0 rgb(30 27 46 / 0.08)",
    "0 2px 6px -1px rgb(30 27 46 / 0.08)",
    "0 4px 10px -2px rgb(30 27 46 / 0.10)",
    "0 6px 14px -3px rgb(30 27 46 / 0.10)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
    "0 8px 18px -4px rgb(30 27 46 / 0.12)",
  ],
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: {
          scrollbarColor: "#c9c4dc #f4f5fb",
          "&::-webkit-scrollbar": { width: 10, height: 10 },
          "&::-webkit-scrollbar-thumb": { backgroundColor: "#c9c4dc", borderRadius: 8 },
          "&::-webkit-scrollbar-track": { backgroundColor: "transparent" },
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: { borderRadius: 10, paddingInline: 18 },
        contained: { boxShadow: "none", "&:hover": { boxShadow: "none" } },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: "none" },
        outlined: { borderColor: "#e6e3f1" },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          border: "1px solid #e6e3f1",
          boxShadow: "0 1px 2px 0 rgb(30 27 46 / 0.04)",
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: { fontWeight: 600, borderRadius: 8 },
      },
    },
    MuiAppBar: {
      styleOverrides: {
        root: { boxShadow: "none", borderBottom: "1px solid #e6e3f1" },
      },
    },
    MuiTextField: {
      defaultProps: { size: "small" },
    },
    MuiDataGrid: {
      styleOverrides: {
        root: { border: "none" },
      },
    },
  },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <SnackbarProvider maxSnack={3} anchorOrigin={{ vertical: "bottom", horizontal: "right" }}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </SnackbarProvider>
    </ThemeProvider>
  </StrictMode>
);
