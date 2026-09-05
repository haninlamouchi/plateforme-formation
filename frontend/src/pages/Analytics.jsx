import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  BarChart3, GraduationCap, CheckCircle2, Award, MessageCircle, Download, AlertTriangle,
} from 'lucide-react';
import StatCard from '../components/dashboard/StatCard';
import ChartCard from '../components/charts/ChartCard';
import AreaChart from '../components/charts/AreaChart';
import HorizBarChart from '../components/charts/HorizBarChart';
import DonutChart from '../components/charts/DonutChart';
import { useLanguage } from '../context/LanguageContext';
import { fetchAnalyticsSummary, fetchAnalyticsCharts } from '../services/analyticsService';

const MODE_META = {
  documents: { label: 'Documents', color: '#01B8AA' },
  general: { label: 'Général', color: '#A66999' },
};

// Fluent Design's own status colors (Power BI conditional-formatting default green/amber/red) —
// this triad is a recognizable Power BI convention in its own right, distinct from the report's
// qualitative data palette used everywhere else on this page.
const NIVEAU_COLOR = { EXCELLENT: '#107C10', BON: '#FFB900', A_REVOIR: '#D13438' };

function toAreaData(series) {
  return (series ?? []).map(d => ({ month: d.day, count: d.count }));
}

export default function Analytics() {
  const { lang } = useLanguage();
  const [summary, setSummary] = useState(null);
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [summaryError, setSummaryError] = useState(false);
  const [charts, setCharts] = useState(null);
  const [chartsLoading, setChartsLoading] = useState(true);
  const [chartsError, setChartsError] = useState(false);

  // A swallowed fetch failure used to leave `summary`/`charts` at null forever, which the ?? fallback
  // below then rendered as an all-zero dashboard — indistinguishable from "genuinely no activity this
  // month". Tracking the error explicitly lets the page say so instead of silently lying with zeros.
  useEffect(() => {
    fetchAnalyticsSummary().then(setSummary).catch(() => setSummaryError(true)).finally(() => setSummaryLoading(false));
    fetchAnalyticsCharts().then(setCharts).catch(() => setChartsError(true)).finally(() => setChartsLoading(false));
  }, []);

  const s = summary ?? {
    formationsGeneratedThisMonth: 0, validationRatePct: 0, avgQualityScore: 0,
    chatbotQuestionsThisMonth: 0, exportsThisMonth: 0, pdfExports: 0, pptxExports: 0,
  };

  const statCards = [
    {
      key: 'generations', icon: GraduationCap, value: s.formationsGeneratedThisMonth,
      label: lang === 'fr' ? 'Formations générées (ce mois)' : 'Formations generated (this month)', color: '#01B8AA', delay: 0.03,
    },
    {
      key: 'validation', icon: CheckCircle2, value: Math.round(s.validationRatePct), suffix: '%',
      label: lang === 'fr' ? 'Taux de validation' : 'Validation rate', color: '#5F6B6D', delay: 0.06,
    },
    {
      key: 'quality', icon: Award, value: Math.round(s.avgQualityScore),
      label: lang === 'fr' ? 'Score qualité moyen' : 'Average quality score', color: '#F2C80F', delay: 0.09,
    },
    {
      key: 'chatbot', icon: MessageCircle, value: s.chatbotQuestionsThisMonth,
      label: lang === 'fr' ? 'Questions chatbot (ce mois)' : 'Chatbot questions (this month)', color: '#8AD4EB', delay: 0.12,
    },
    {
      key: 'exports', icon: Download, value: s.exportsThisMonth,
      label: lang === 'fr' ? 'Exports (ce mois)' : 'Exports (this month)', color: '#A66999', delay: 0.15,
    },
  ];

  const c = charts ?? { activityTimeline: null, topDocuments: [], chatbotModeSplit: [], formationsNeedingAttention: [] };

  return (
    <div className="max-w-[1200px] mx-auto space-y-5">
      <motion.div
        className="flex items-center gap-3"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}
      >
        <div className="w-11 h-11 rounded-2xl flex items-center justify-center shrink-0 bg-[var(--color-primary)]/10">
          <BarChart3 className="w-5 h-5 text-[var(--color-primary)]" />
        </div>
        <div>
          <h1 className="text-[20px] font-extrabold text-[var(--color-ink)] tracking-tight">
            {lang === 'fr' ? "Analytique d'usage" : 'Usage analytics'}
          </h1>
          <p className="text-[12.5px] text-[var(--color-ink-muted)]">
            {lang === 'fr' ? 'Comment la plateforme est réellement utilisée.' : 'How the platform is actually being used.'}
          </p>
        </div>
      </motion.div>

      {(summaryError || chartsError) && (
        <div
          className="flex items-center gap-2 rounded-xl px-4 py-2.5 text-[13px] font-medium"
          style={{ background: 'rgba(209,52,56,0.1)', color: NIVEAU_COLOR.A_REVOIR }}
        >
          <AlertTriangle className="w-4 h-4 shrink-0" />
          {lang === 'fr'
            ? "Certaines données n'ont pas pu être chargées — les chiffres ci-dessous peuvent être incomplets."
            : 'Some data failed to load — the figures below may be incomplete.'}
        </div>
      )}

      <div className="bi-scope bi-canvas p-4 space-y-4">
        <div className="grid grid-cols-2 lg:grid-cols-5 gap-3">
          {statCards.map(({ key, ...card }) => (
            <StatCard key={key} {...card} loading={summaryLoading} />
          ))}
        </div>

        <div className="grid lg:grid-cols-3 gap-3">
          <ChartCard title={lang === 'fr' ? 'Formations générées (30j)' : 'Formations generated (30d)'} loading={chartsLoading}>
            {c.activityTimeline && <AreaChart data={toAreaData(c.activityTimeline.generations)} color="#01B8AA" />}
          </ChartCard>
          <ChartCard title={lang === 'fr' ? 'Exports (30j)' : 'Exports (30d)'} loading={chartsLoading}>
            {c.activityTimeline && <AreaChart data={toAreaData(c.activityTimeline.exports)} color="#A66999" />}
          </ChartCard>
          <ChartCard title={lang === 'fr' ? 'Questions chatbot (30j)' : 'Chatbot questions (30d)'} loading={chartsLoading}>
            {c.activityTimeline && <AreaChart data={toAreaData(c.activityTimeline.chatbotQuestions)} color="#8AD4EB" />}
          </ChartCard>
        </div>

        <div className="grid lg:grid-cols-2 gap-3">
          <ChartCard title={lang === 'fr' ? 'Documents les plus utilisés' : 'Most-used documents'} loading={chartsLoading}>
            {c.topDocuments?.length > 0 ? (
              <HorizBarChart data={c.topDocuments.map(d => ({ name: d.titre, count: d.usageCount }))} color="#374649" />
            ) : (
              <p className="text-[13px] text-center py-4" style={{ color: 'var(--bi-text-muted)' }}>
                {lang === 'fr' ? 'Aucune donnée pour le moment.' : 'No data yet.'}
              </p>
            )}
          </ChartCard>

          <ChartCard title={lang === 'fr' ? 'Répartition des réponses chatbot' : 'Chatbot answer breakdown'} loading={chartsLoading}>
            {c.chatbotModeSplit?.length > 0 ? (
              <DonutChart
                data={c.chatbotModeSplit}
                keyField="mode"
                meta={MODE_META}
                centerLabel={lang === 'fr' ? 'questions' : 'questions'}
              />
            ) : (
              <p className="text-[13px] text-center py-4" style={{ color: 'var(--bi-text-muted)' }}>
                {lang === 'fr' ? 'Aucune question posée pour le moment.' : 'No questions asked yet.'}
              </p>
            )}
          </ChartCard>
        </div>

        <ChartCard title={lang === 'fr' ? 'Formations à améliorer' : 'Formations needing attention'} loading={chartsLoading}>
          {c.formationsNeedingAttention?.length > 0 ? (
            <div className="flex flex-col divide-y" style={{ borderColor: 'var(--bi-grid-line)' }}>
              {c.formationsNeedingAttention.map(f => (
                <Link
                  key={f.id}
                  to={`/formations/${f.id}`}
                  className="flex items-center gap-3 py-2.5 first:pt-0 last:pb-0 hover:opacity-80 transition-opacity"
                >
                  <AlertTriangle className="w-4 h-4 shrink-0" style={{ color: NIVEAU_COLOR[f.qualiteNiveau] ?? NIVEAU_COLOR.A_REVOIR }} />
                  <span className="text-[13px] truncate flex-1" style={{ color: 'var(--bi-text)' }}>{f.titre}</span>
                  <span
                    className="text-[12px] font-semibold px-2 py-0.5 shrink-0"
                    style={{
                      borderRadius: 2,
                      color: NIVEAU_COLOR[f.qualiteNiveau] ?? NIVEAU_COLOR.A_REVOIR,
                      background: `${NIVEAU_COLOR[f.qualiteNiveau] ?? NIVEAU_COLOR.A_REVOIR}18`,
                    }}
                  >
                    {f.qualiteScore}/100
                  </span>
                </Link>
              ))}
            </div>
          ) : (
            <p className="text-[13px] text-center py-4" style={{ color: 'var(--bi-text-muted)' }}>
              {lang === 'fr' ? 'Aucun brouillon à améliorer — tout va bien.' : 'No drafts needing attention — all good.'}
            </p>
          )}
        </ChartCard>
      </div>
    </div>
  );
}
