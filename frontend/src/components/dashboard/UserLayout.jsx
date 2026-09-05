import { Outlet } from 'react-router-dom';
import UserTopNavbar from './UserTopNavbar';

export default function UserLayout() {
  return (
    <div className="min-h-screen bg-[var(--color-surface-0)] flex flex-col">
      <UserTopNavbar />
      <main className="flex-1 max-w-[1280px] w-full mx-auto px-4 sm:px-6 py-6 lg:py-8">
        <Outlet />
      </main>
    </div>
  );
}
