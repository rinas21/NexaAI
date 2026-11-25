import { useEffect, useState, type ReactNode } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  BarChart3,
  Bot,
  FileText,
  LayoutDashboard,
  LogOut,
  Menu,
  MessageSquare,
  Settings,
  Sparkles,
  UserRound,
  X,
} from 'lucide-react';
import { useAuth } from '@hooks/useAuth';
import { useAuthStore } from '@store/authStore';
import { cn } from '@utils/cn';
import { getInitials } from '@utils/format';
import { Dropdown } from '@components/ui/Dropdown';
import { ThemeToggle } from '@components/ui/ThemeToggle';

interface NavItem {
  to: string;
  label: string;
  description: string;
  icon: ReactNode;
}

const navigation: NavItem[] = [
  { to: '/', label: 'Overview', description: 'Dashboard', icon: <LayoutDashboard className="h-[18px] w-[18px]" /> },
  { to: '/chat', label: 'Chat', description: 'Ask your data', icon: <MessageSquare className="h-[18px] w-[18px]" /> },
  { to: '/documents', label: 'Documents', description: 'Knowledge base', icon: <FileText className="h-[18px] w-[18px]" /> },
  { to: '/agents', label: 'Agents', description: 'Automations', icon: <Bot className="h-[18px] w-[18px]" /> },
  { to: '/evaluation', label: 'Evaluation', description: 'Quality metrics', icon: <BarChart3 className="h-[18px] w-[18px]" /> },
  { to: '/settings', label: 'Settings', description: 'Preferences', icon: <Settings className="h-[18px] w-[18px]" /> },
];

function SidebarContent({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <div className="flex h-full flex-col">
      <div className="flex h-16 items-center gap-3 border-b border-neutral-200 px-5 dark:border-neutral-800">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 text-white shadow-soft">
          <Sparkles className="h-5 w-5" />
        </span>
        <div className="leading-tight">
          <p className="text-base font-bold tracking-tight text-neutral-900 dark:text-white">NexaAI</p>
          <p className="text-[10px] uppercase tracking-[0.16em] text-neutral-400">Enterprise Assistant</p>
        </div>
      </div>

      <nav aria-label="Main navigation" className="flex-1 space-y-1 overflow-y-auto px-3 py-5">
        <p className="mb-3 px-3 text-[10px] font-semibold uppercase tracking-[0.14em] text-neutral-400">
          Workspace
        </p>
        {navigation.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === '/'}
            onClick={onNavigate}
            className={({ isActive }) =>
              cn(
                'group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-all',
                isActive
                  ? 'bg-primary-50 text-primary-700 ring-1 ring-primary-100 dark:bg-primary-900/25 dark:text-primary-300 dark:ring-primary-900/40'
                  : 'text-neutral-600 hover:bg-neutral-100 hover:text-neutral-900 dark:text-neutral-400 dark:hover:bg-neutral-800 dark:hover:text-neutral-100',
              )
            }
          >
            {({ isActive }) => (
              <>
                <span className={cn('transition-colors', isActive ? 'text-primary-600 dark:text-primary-400' : 'text-neutral-400 group-hover:text-neutral-600 dark:group-hover:text-neutral-300')}>
                  {item.icon}
                </span>
                <span className="flex-1">{item.label}</span>
                <span className="text-[10px] font-normal text-neutral-400 opacity-0 transition-opacity group-hover:opacity-100">
                  {item.description}
                </span>
              </>
            )}
          </NavLink>
        ))}
      </nav>

      <div className="m-3 rounded-xl bg-gradient-to-br from-primary-50 to-indigo-50 p-4 dark:from-primary-900/20 dark:to-indigo-900/20">
        <p className="text-xs font-semibold text-neutral-800 dark:text-neutral-100">Need a hand?</p>
        <p className="mt-1 text-xs leading-relaxed text-neutral-500 dark:text-neutral-400">
          Upload documents, then ask questions grounded in your own knowledge base.
        </p>
        <NavLink to="/chat" onClick={onNavigate} className="mt-3 inline-block text-xs font-semibold text-primary-700 hover:underline dark:text-primary-300">
          Start a conversation →
        </NavLink>
      </div>
    </div>
  );
}

export function Layout() {
  const [mobileOpen, setMobileOpen] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();
  const { signOut } = useAuth();
  const user = useAuthStore((state) => state.user);

  useEffect(() => {
    setMobileOpen(false);
  }, [location.pathname]);

  const activeNav = navigation.find((item) =>
    item.to === '/' ? location.pathname === '/' : location.pathname.startsWith(item.to),
  );

  const handleLogout = () => {
    void signOut().then(() => navigate('/login'));
  };

  const displayName = user?.fullName ?? user?.name ?? user?.email ?? 'Account';

  return (
    <div className="min-h-screen bg-neutral-50 dark:bg-neutral-950">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-[260px] border-r border-neutral-200 bg-white dark:border-neutral-800 dark:bg-neutral-900 lg:block">
        <SidebarContent />
      </aside>

      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div
            className="absolute inset-0 bg-neutral-950/50 backdrop-blur-sm"
            onClick={() => setMobileOpen(false)}
            aria-hidden="true"
          />
          <aside className="relative h-full w-[280px] bg-white shadow-soft animate-slide-up dark:bg-neutral-900" aria-label="Mobile navigation">
            <button
              type="button"
              onClick={() => setMobileOpen(false)}
              aria-label="Close navigation"
              className="absolute right-3 top-4 btn-icon btn-ghost text-neutral-500"
            >
              <X className="h-5 w-5" />
            </button>
            <SidebarContent onNavigate={() => setMobileOpen(false)} />
          </aside>
        </div>
      )}

      <div className="lg:pl-[260px]">
        <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-neutral-200 bg-white/90 px-4 backdrop-blur-lg dark:border-neutral-800 dark:bg-neutral-900/90 sm:px-6">
          <button
            type="button"
            onClick={() => setMobileOpen(true)}
            aria-label="Open navigation"
            className="btn-icon btn-ghost text-neutral-500 lg:hidden"
          >
            <Menu className="h-5 w-5" />
          </button>
          <div className="min-w-0 flex-1">
            <h1 className="truncate text-base font-semibold text-neutral-900 dark:text-neutral-50">
              {activeNav?.label ?? 'NexaAI'}
            </h1>
            <p className="hidden text-xs text-neutral-500 dark:text-neutral-400 sm:block">
              {activeNav?.description ?? 'Enterprise AI Assistant'}
            </p>
          </div>

          <div className="flex items-center gap-1.5">
            <span className="mr-2 hidden items-center gap-2 rounded-full bg-green-50 px-3 py-1.5 text-xs font-medium text-green-700 dark:bg-green-900/20 dark:text-green-300 sm:inline-flex">
              <span className="h-2 w-2 animate-pulse rounded-full bg-green-500" />
              System online
            </span>
            <ThemeToggle />
            <div className="mx-1 h-6 w-px bg-neutral-200 dark:bg-neutral-700" />
            <Dropdown
              ariaLabel="User menu"
              trigger={
                <span className="flex items-center gap-2.5 rounded-lg px-1.5 py-1 hover:bg-neutral-100 dark:hover:bg-neutral-800">
                  <span className="flex h-8 w-8 items-center justify-center rounded-full bg-gradient-to-br from-primary-500 to-indigo-600 text-xs font-semibold text-white">
                    {getInitials(user?.fullName ?? user?.name, user?.email)}
                  </span>
                  <span className="hidden max-w-[140px] truncate text-sm font-medium text-neutral-700 dark:text-neutral-200 md:block">
                    {displayName}
                  </span>
                </span>
              }
              items={[
                {
                  label: 'Profile settings',
                  icon: <UserRound className="h-4 w-4" />,
                  onClick: () => navigate('/settings'),
                },
                {
                  label: 'Sign out',
                  icon: <LogOut className="h-4 w-4" />,
                  danger: true,
                  onClick: handleLogout,
                },
              ]}
            />
          </div>
        </header>

        <main className="mx-auto w-full max-w-[1600px] p-4 sm:p-6 lg:p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
