import { Component } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { AnimatePresence } from 'framer-motion';
import Login from './pages/Login';
import Register from './pages/Register';
import ForgotPassword from './pages/ForgotPassword';
import ResetPassword from './pages/ResetPassword';
import Dashboard from './pages/Dashboard';
import AdminValidations from './pages/AdminValidations';
import Profile from './pages/Profile';
import UsersManagement from './pages/UsersManagement';
import Categories from './pages/Categories';
import Analytics from './pages/Analytics';
import AuditLog from './pages/AuditLog';
import Documents from './pages/Documents';
import Formations from './pages/Formations';
import FormationDetail from './pages/FormationDetail';
import Chat from './pages/Chat';
import ProtectedRoute from './components/ProtectedRoute';
import DashboardLayout from './components/dashboard/DashboardLayout';
import './styles/theme.css';

function AnimatedRoutes() {
  const location = useLocation();
  return (
    <AnimatePresence mode="wait">
      <Routes location={location} key={location.pathname}>
        <Route path="/" element={<Navigate to="/login" replace />} />
        <Route path="/login" element={<Login />} />
        <Route path="/register" element={<Register />} />
        <Route path="/forgot-password" element={<ForgotPassword />} />
        <Route path="/reset-password" element={<ResetPassword />} />
        <Route
          element={
            <ProtectedRoute>
              <DashboardLayout />
            </ProtectedRoute>
          }
        >
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/users" element={
            <ProtectedRoute allowedRoles={['ADMINISTRATEUR']}>
              <UsersManagement />
            </ProtectedRoute>
          } />
          <Route path="/documents" element={<Documents />} />
          <Route path="/formations" element={<Formations />} />
          <Route path="/formations/:id" element={<FormationDetail />} />
          <Route path="/chat" element={<Chat />} />
          <Route path="/admin/validations" element={
            <ProtectedRoute allowedRoles={['ADMINISTRATEUR']}>
              <AdminValidations />
            </ProtectedRoute>
          } />
          <Route path="/categories" element={
            <ProtectedRoute allowedRoles={['ADMINISTRATEUR']}>
              <Categories />
            </ProtectedRoute>
          } />
          <Route path="/analytics" element={
            <ProtectedRoute allowedRoles={['ADMINISTRATEUR']}>
              <Analytics />
            </ProtectedRoute>
          } />
          <Route path="/audit-log" element={
            <ProtectedRoute allowedRoles={['ADMINISTRATEUR']}>
              <AuditLog />
            </ProtectedRoute>
          } />
        </Route>
      </Routes>
    </AnimatePresence>
  );
}

class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }
  static getDerivedStateFromError() {
    return { hasError: true };
  }
  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
          minHeight: '100vh', gap: '16px', padding: '40px', textAlign: 'center',
          fontFamily: 'system-ui, sans-serif', color: '#374151',
        }}>
          <div style={{ fontSize: '32px' }}>⚠️</div>
          <h2 style={{ margin: 0, fontSize: '20px', fontWeight: '700' }}>Something went wrong</h2>
          <p style={{ margin: 0, fontSize: '14px', color: '#6B7280' }}>
            An unexpected error occurred. Please reload the page.
          </p>
          <button
            onClick={() => window.location.reload()}
            style={{
              padding: '10px 24px', borderRadius: '10px', border: 'none',
              background: '#9B111E', color: '#fff', fontSize: '14px',
              fontWeight: '600', cursor: 'pointer',
            }}>
            Reload page
          </button>
        </div>
      );
    }
    return this.props.children;
  }
}

function App() {
  return (
    <ErrorBoundary>
      <BrowserRouter>
        <AnimatedRoutes />
      </BrowserRouter>
    </ErrorBoundary>
  );
}

export default App;
