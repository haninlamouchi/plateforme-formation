import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import Navbar from './Navbar';
import UserLayout from './UserLayout';
import ChatbotWidget from '../ChatbotWidget';
import { useAuth } from '../../context/AuthContext';

export default function DashboardLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { user } = useAuth();

  if (user?.role !== 'ADMINISTRATEUR') {
    return (
      <>
        <UserLayout />
        <ChatbotWidget />
      </>
    );
  }

  return (
    <div className="flex min-h-screen bg-[var(--color-surface-0)]">
      <Sidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      <div className="flex-1 flex flex-col lg:ml-[var(--sidebar-width)] min-h-screen relative z-[1]">
        <Navbar onMenuClick={() => setSidebarOpen(true)} />
        <main className="flex-1 p-5 lg:p-8">
          <Outlet />
        </main>
      </div>
      <ChatbotWidget />
    </div>
  );
}
