import { Routes, Route } from "react-router-dom";
import Layout from "./components/Layout";
import DashboardPage from "./pages/DashboardPage";
import ImportWizardPage from "./pages/ImportWizardPage";
import HistoryPage from "./pages/HistoryPage";
import AuditPage from "./pages/AuditPage";
import ClientAccountsPage from "./pages/ClientAccountsPage";

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/vehicle" element={<ImportWizardPage type="vehicle" />} />
        <Route path="/customer" element={<ImportWizardPage type="customer" />} />
        <Route path="/history" element={<HistoryPage />} />
        <Route path="/audit" element={<AuditPage />} />
        <Route path="/client-accounts" element={<ClientAccountsPage />} />
      </Route>
    </Routes>
  );
}
