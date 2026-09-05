import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  Users, ShieldCheck, FileText, BookOpen,
  FolderOpen, Clock, TrendingUp,
} from 'lucide-react';
import HeroBanner from '../components/dashboard/HeroBanner';
import StatCard from '../components/dashboard/StatCard';
import QuickActionCard from '../components/dashboard/QuickActionCard';
import AreaChart from '../components/charts/AreaChart';
import DonutChart from '../components/charts/DonutChart';
import HorizBarChart from '../components/charts/HorizBarChart';
import ChartCard from '../components/charts/ChartCard';
import UserDashboard from './UserDashboard';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import { fetchAdminStats, fetchAdminCharts } from '../services/adminService';

/* ─── Admin dashboard ───────────────────────────────────────────────── */

function AdminDashboard({ t, lang }) {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [statsError, setStatsError] = useState(null);
  const [charts, setCharts] = useState(null);
  const [chartsLoading, setChartsLoading] = useState(true);

  useEffect(() => {
    fetchAdminStats()
      .then(data => { setStats(data); setStatsError(null); })
      .catch(err => {
        console.error('[adminStats] fetch failed:', err);
        setStatsError(err?.response?.status ?? 'network');
      })
      .finally(() => setLoading(false));
    fetchAdminCharts()
      .then(data => setCharts(data))
      .catch(() => {})
      .finally(() => setChartsLoading(false));
  }, []);

  const s = stats ?? {
    activeUsers: 0,
    pendingAccounts: 0,
    totalDocuments: 0,
    totalFormations: 0,
    totalCategories: 0,
    recentUploads: 0,
  };

  const statCards = [
    {
      key: 'users',
      icon: Users,
      value: s.activeUsers,
      label: lang === 'fr' ? 'Utilisateurs actifs' : 'Active users',
      color: '#01B8AA',
      delay: 0.04,
    },
    {
      key: 'pending',
      icon: ShieldCheck,
      value: s.pendingAccounts,
      label: lang === 'fr' ? 'Comptes en attente' : 'Pending accounts',
      color: s.pendingAccounts > 0 ? '#FD625E' : '#01B8AA',
      delay: 0.08,
    },
    {
      key: 'docs',
      icon: FileText,
      value: s.totalDocuments,
      label: lang === 'fr' ? 'Documents' : 'Documents',
      color: '#374649',
      delay: 0.12,
    },
    {
      key: 'formations',
      icon: BookOpen,
      value: s.totalFormations,
      label: lang === 'fr' ? 'Formations' : 'Trainings',
      color: '#A66999',
      delay: 0.16,
    },
  ];

  const quickActions = [
    {
      key: 'validate',
      icon: ShieldCheck,
      title: t('dashboardActionValidate'),
      description: t('dashboardActionValidateDesc'),
      color: '#9B111E',
      to: '/admin/validations',
      badge: s.pendingAccounts > 0 ? s.pendingAccounts : null,
      delay: 0.30,
    },
    {
      key: 'users',
      icon: Users,
      title: lang === 'fr' ? 'Gérer les utilisateurs' : 'Manage users',
      description: lang === 'fr' ? 'Consulter, modifier et gérer tous les comptes.' : 'View, edit and manage all accounts.',
      color: '#1D4ED8',
      to: '/users',
      badge: null,
      delay: 0.34,
    },
    {
      key: 'documents',
      icon: FileText,
      title: t('dashboardActionDocuments'),
      description: t('dashboardActionDocumentsDesc'),
      color: '#6B7280',
      to: '/documents',
      badge: null,
      delay: 0.38,
    },
    {
      key: 'categories',
      icon: FolderOpen,
      title: lang === 'fr' ? 'Catégories' : 'Categories',
      description: lang === 'fr' ? 'Organiser les catégories de documents.' : 'Organise document categories.',
      color: '#7C3AED',
      to: '/categories',
      badge: null,
      delay: 0.42,
    },
  ];

  return (
    <div className="max-w-[1200px] mx-auto space-y-8">
      <HeroBanner />

      {/* Stats API error — debug banner */}
      {statsError && (
        <div className="px-4 py-2.5 rounded-xl text-[13px] font-medium text-white" style={{ background: '#9B111E' }}>
          Stats API error (HTTP {statsError}) — check browser console &amp; ensure backend is running with the new /api/admin/stats endpoint.
        </div>
      )}

      <div className="bi-scope bi-canvas p-4 space-y-4">
        {/* Stat cards — always render, animate in */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
          {statCards.map(({ key, ...card }) => (
            <StatCard key={key} {...card} loading={loading} />
          ))}
        </div>

        {/* Secondary info pills */}
        <motion.div
          className="grid grid-cols-3 gap-3"
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
        >
          <InfoPill
            icon={FolderOpen}
            value={loading ? null : s.totalCategories}
            label={lang === 'fr' ? 'Catégories' : 'Categories'}
            color="#A66999"
          />
          <InfoPill
            icon={Clock}
            value={loading ? null : s.recentUploads}
            label={lang === 'fr' ? 'Docs ajoutés (30j)' : 'Docs added (30d)'}
            color="#8AD4EB"
          />
          <InfoPill
            icon={TrendingUp}
            value={loading ? null : s.activeUsers + s.pendingAccounts}
            label={lang === 'fr' ? 'Total inscrits' : 'Total registrations'}
            color="#01B8AA"
          />
        </motion.div>

        {/* Charts */}
        <motion.div
          className="grid lg:grid-cols-2 gap-3"
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.24 }}
        >
          <ChartCard title={lang === 'fr' ? 'Documents ajoutés (6 derniers mois)' : 'Documents added (last 6 months)'} loading={chartsLoading}>
            {charts?.uploadsByMonth?.length > 0 && (
              <AreaChart data={charts.uploadsByMonth} color="#01B8AA" />
            )}
          </ChartCard>

          <ChartCard title={lang === 'fr' ? 'Utilisateurs par rôle' : 'Users by role'} loading={chartsLoading}>
            {charts?.usersByRole?.length > 0 ? (
              <DonutChart data={charts.usersByRole} />
            ) : (
              <p className="text-[13px] text-center py-4" style={{ color: 'var(--bi-text-muted)' }}>
                {lang === 'fr' ? 'Aucun utilisateur actif.' : 'No active users yet.'}
              </p>
            )}
          </ChartCard>
        </motion.div>

        {charts?.docsByCategory?.length > 0 && (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.28 }}
          >
            <ChartCard title={lang === 'fr' ? 'Documents par catégorie (top 6)' : 'Documents by category (top 6)'} loading={chartsLoading}>
              <HorizBarChart data={charts.docsByCategory} color="#374649" />
            </ChartCard>
          </motion.div>
        )}
      </div>

      {/* Quick actions */}
      <div>
        <h3 className="flex items-center gap-2.5 text-[15px] font-bold text-[var(--color-ink)] mb-4 tracking-tight">
          <span className="w-1 h-4 rounded-full bg-[var(--color-primary)] shrink-0" />
          {t('dashboardQuickActions')}
        </h3>
        <div className="grid sm:grid-cols-2 gap-3">
          {quickActions.map(({ key, to, badge, ...rest }) => (
            <div key={key} className="relative">
              <Link to={to} className="block">
                <QuickActionCard {...rest} />
              </Link>
              {badge && (
                <span
                  className="absolute -top-1.5 -right-1.5 min-w-[20px] h-5 px-1.5 rounded-full text-[11px] font-bold text-white flex items-center justify-center pointer-events-none"
                  style={{ background: '#9B111E', boxShadow: '0 1px 4px rgba(155,17,30,0.4)', zIndex: 1 }}
                >
                  {badge > 99 ? '99+' : badge}
                </span>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function InfoPill({ icon: Icon, value, label, color }) {
  return (
    <div className="bi-visual flex items-center gap-3 px-4 py-3">
      <div className="w-8 h-8 flex items-center justify-center shrink-0" style={{ background: `${color}18`, borderRadius: 3 }}>
        <Icon className="w-4 h-4" style={{ color }} />
      </div>
      <div className="min-w-0">
        <div className="text-[18px] font-semibold leading-none tracking-tight tabular-nums" style={{ color: 'var(--bi-text)' }}>
          {value === null ? (
            <span className="inline-block w-8 h-4" style={{ borderRadius: 2, background: 'var(--bi-grid-line)', verticalAlign: 'middle' }} />
          ) : value}
        </div>
        <div className="text-[11px] mt-0.5 truncate" style={{ color: 'var(--bi-text-muted)' }}>{label}</div>
      </div>
    </div>
  );
}

export default function Dashboard() {
  const { user } = useAuth();
  const { t, lang } = useLanguage();

  if (user?.role === 'ADMINISTRATEUR') {
    return <AdminDashboard t={t} lang={lang} />;
  }

  return <UserDashboard />;
}
