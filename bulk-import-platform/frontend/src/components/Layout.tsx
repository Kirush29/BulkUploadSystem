import { useState } from "react";
import {
  AppBar,
  Box,
  Toolbar,
  Typography,
  Drawer,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  IconButton,
  Avatar,
  Stack,
} from "@mui/material";
import CloudUploadIcon from "@mui/icons-material/CloudUploadRounded";
import DashboardIcon from "@mui/icons-material/DashboardRounded";
import DirectionsCarIcon from "@mui/icons-material/DirectionsCarFilledRounded";
import PeopleIcon from "@mui/icons-material/PeopleAltRounded";
import HistoryIcon from "@mui/icons-material/HistoryRounded";
import FactCheckIcon from "@mui/icons-material/FactCheckRounded";
import MenuIcon from "@mui/icons-material/MenuRounded";
import BadgeIcon from "@mui/icons-material/BadgeRounded";
import EventIcon from "@mui/icons-material/EventAvailableRounded";
import { NavLink, Outlet, useLocation } from "react-router-dom";

const drawerWidth = 256;

const navItems = [
  { label: "Dashboard", path: "/", icon: <DashboardIcon /> },
  { label: "Vehicle Import", path: "/vehicle", icon: <DirectionsCarIcon /> },
  { label: "Customer Import", path: "/customer", icon: <PeopleIcon /> },
  { label: "Reservation Import", path: "/reservation", icon: <EventIcon /> },
  { label: "Upload History", path: "/history", icon: <HistoryIcon /> },
  { label: "Audit Log", path: "/audit", icon: <FactCheckIcon /> },
  { label: "Client Accounts", path: "/client-accounts", icon: <BadgeIcon /> },
];

export default function Layout() {
  const [open, setOpen] = useState(true);
  const location = useLocation();
  const current = navItems.find((n) => n.path === location.pathname);

  return (
    <Box sx={{ display: "flex" }}>
      <AppBar
        position="fixed"
        color="inherit"
        sx={{ zIndex: (theme) => theme.zIndex.drawer + 1, bgcolor: "background.paper" }}
      >
        <Toolbar sx={{ gap: 1.5 }}>
          <IconButton edge="start" onClick={() => setOpen(!open)}>
            <MenuIcon />
          </IconButton>
          <Typography variant="subtitle1" noWrap component="div" sx={{ color: "text.primary" }}>
            {current?.label ?? "Bulk Import Platform"}
          </Typography>
          <Box sx={{ flexGrow: 1 }} />
          <Avatar sx={{ width: 34, height: 34, bgcolor: "primary.main", fontSize: 14, fontWeight: 700 }}>
            DU
          </Avatar>
        </Toolbar>
      </AppBar>

      <Drawer
        variant="persistent"
        open={open}
        sx={{
          width: open ? drawerWidth : 0,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: {
            width: drawerWidth,
            boxSizing: "border-box",
            borderRight: "1px solid",
            borderColor: "divider",
            bgcolor: "background.paper",
          },
        }}
      >
        <Toolbar sx={{ px: 2.5 }}>
          <Stack direction="row" spacing={1.25} alignItems="center">
            <Box
              sx={{
                width: 34,
                height: 34,
                borderRadius: "10px",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                background: "linear-gradient(135deg, #4338ca, #6366f1)",
                color: "#fff",
              }}
            >
              <CloudUploadIcon fontSize="small" />
            </Box>
            <Typography variant="subtitle1" sx={{ lineHeight: 1.1 }}>
              Bulk Import
            </Typography>
          </Stack>
        </Toolbar>

        <Box sx={{ overflow: "auto", px: 1.5, py: 1 }}>
          <List sx={{ display: "flex", flexDirection: "column", gap: 0.5 }}>
            {navItems.map((item) => {
              const selected = location.pathname === item.path;
              return (
                <ListItemButton
                  key={item.path}
                  component={NavLink}
                  to={item.path}
                  selected={selected}
                  sx={{
                    borderRadius: 2,
                    py: 1,
                    color: selected ? "primary.main" : "text.secondary",
                    "&.Mui-selected": {
                      bgcolor: "primary.main",
                      color: "primary.contrastText",
                      "&:hover": { bgcolor: "primary.dark" },
                      "& .MuiListItemIcon-root": { color: "inherit" },
                    },
                    "&:hover": { bgcolor: selected ? undefined : "action.hover" },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 38, color: selected ? "inherit" : "text.secondary" }}>
                    {item.icon}
                  </ListItemIcon>
                  <ListItemText
                    primary={item.label}
                    primaryTypographyProps={{ fontSize: 14, fontWeight: selected ? 700 : 500 }}
                  />
                </ListItemButton>
              );
            })}
          </List>
        </Box>
      </Drawer>

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          p: { xs: 2, sm: 3 },
          minHeight: "100vh",
          bgcolor: "background.default",
        }}
      >
        <Toolbar />
        <Box sx={{ maxWidth: 1400, mx: "auto" }}>
          <Outlet />
        </Box>
      </Box>
    </Box>
  );
}
