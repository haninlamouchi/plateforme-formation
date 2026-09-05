import { useState, useEffect, useCallback, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Tag, Plus, Pencil, Trash2, X, Check, FolderOpen, Sparkles, Search, ArrowDownAZ, BarChart3, AlertTriangle } from 'lucide-react';
import { getAllCategories, createCategorie, updateCategorie, deleteCategorie, suggestCategories } from '../services/categorieService';
import { useLanguage } from '../context/LanguageContext';
import './Categories.css';

const PALETTE = [
  '#9B111E', '#C2410C', '#B45309', '#15803D',
  '#0E7490', '#1D4ED8', '#6D28D9', '#9D174D',
  '#374151', '#4B5563',
];

function colorForName(name) {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = name.charCodeAt(i) + ((h << 5) - h);
  return PALETTE[Math.abs(h) % PALETTE.length];
}

function loadColors() {
  try { return JSON.parse(localStorage.getItem('cat_colors') || '{}'); }
  catch { return {}; }
}

function saveColors(obj) {
  localStorage.setItem('cat_colors', JSON.stringify(obj));
}

function SkeletonCard() {
  return (
    <div className="cat-card cat-card--skeleton">
      <div className="cat-skel cat-skel--icon" />
      <div className="cat-skel cat-skel--title" />
      <div className="cat-skel cat-skel--text" />
      <div className="cat-skel cat-skel--badge" />
    </div>
  );
}

export default function Categories() {
  const { lang } = useLanguage();
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState('name');
  const [modal, setModal] = useState(null);
  const [form, setForm] = useState({ nom: '', description: '', couleur: '' });
  const [saving, setSaving] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [error, setError] = useState('');
  const [toast, setToast] = useState(null);
  const [suggestions, setSuggestions] = useState([]);
  const [loadingSuggestions, setLoadingSuggestions] = useState(false);
  const [catColors, setCatColors] = useState(loadColors);

  const showToast = (msg, type = 'success') => {
    setToast({ msg, type });
    setTimeout(() => setToast(null), 3000);
  };

  const load = useCallback(async () => {
    setLoading(true);
    try { setCategories(await getAllCategories()); }
    catch { showToast(lang === 'fr' ? 'Erreur de chargement.' : 'Load error.', 'error'); }
    finally { setLoading(false); }
  }, [lang]);

  // eslint-disable-next-line react-hooks/set-state-in-effect -- initial data fetch on mount
  useEffect(() => { load(); }, [load]);

  function getCatColor(cat) {
    return catColors[cat.id] || colorForName(cat.nom);
  }

  function updateCatColor(id, color) {
    const next = { ...catColors, [id]: color };
    setCatColors(next);
    saveColors(next);
  }

  const filtered = useMemo(() => {
    const q = search.toLowerCase();
    return categories
      .filter(c => c.nom.toLowerCase().includes(q) || (c.description || '').toLowerCase().includes(q))
      .sort((a, b) =>
        sortBy === 'count' ? b.documentCount - a.documentCount : a.nom.localeCompare(b.nom)
      );
  }, [categories, search, sortBy]);

  function openCreate() {
    setForm({ nom: '', description: '', couleur: PALETTE[0] });
    setError('');
    setSuggestions([]);
    setModal({ mode: 'create' });
  }

  function openEdit(cat) {
    setForm({ nom: cat.nom, description: cat.description ?? '', couleur: getCatColor(cat) });
    setError('');
    setSuggestions([]);
    setModal({ mode: 'edit', cat });
  }

  async function handleSuggest() {
    setLoadingSuggestions(true);
    setSuggestions([]);
    try { setSuggestions(await suggestCategories()); }
    catch { showToast(lang === 'fr' ? 'Erreur IA. Réessayez.' : 'AI error. Try again.', 'error'); }
    finally { setLoadingSuggestions(false); }
  }

  function applySuggestion(s) {
    setForm(f => ({ ...f, nom: s.nom, description: s.description }));
    setSuggestions([]);
  }

  async function handleSave(e) {
    e.preventDefault();
    setError('');
    setSaving(true);
    try {
      if (modal.mode === 'create') {
        const created = await createCategorie({ nom: form.nom, description: form.description || null });
        if (created?.id && form.couleur) updateCatColor(created.id, form.couleur);
        showToast(lang === 'fr' ? 'Catégorie créée.' : 'Category created.');
      } else {
        await updateCategorie(modal.cat.id, { nom: form.nom, description: form.description || null });
        if (form.couleur) updateCatColor(modal.cat.id, form.couleur);
        showToast(lang === 'fr' ? 'Catégorie mise à jour.' : 'Category updated.');
      }
      setModal(null);
      load();
    } catch (err) {
      setError(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'));
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(cat) {
    setDeleting(cat.id);
    try {
      await deleteCategorie(cat.id);
      const next = { ...catColors };
      delete next[cat.id];
      setCatColors(next);
      saveColors(next);
      showToast(lang === 'fr' ? 'Catégorie supprimée.' : 'Category deleted.');
      setConfirmDelete(null);
      load();
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setDeleting(null);
    }
  }

  return (
    <div className="cat">
      <AnimatePresence>
        {toast && (
          <motion.div className={`cat-toast cat-toast--${toast.type}`}
            initial={{ opacity: 0, y: -14, x: '-50%' }}
            animate={{ opacity: 1, y: 0, x: '-50%' }}
            exit={{ opacity: 0, y: -14, x: '-50%' }}>
            {toast.msg}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Modal */}
      <AnimatePresence>
        {modal && (
          <motion.div className="cat-overlay" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => setModal(null)}>
            <motion.div className="cat-modal"
              initial={{ opacity: 0, scale: 0.95, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 20 }}
              transition={{ type: 'spring', stiffness: 320, damping: 28 }}
              onClick={e => e.stopPropagation()}>

              {/* Modal top color bar */}
              <div className="cat-modal__colorbar" style={{ background: form.couleur || 'var(--color-primary)' }} />

              <div className="cat-modal__header">
                <div className="cat-modal__header-left">
                  <div className="cat-modal__icon" style={{ background: `${form.couleur || 'var(--color-primary)'}18`, color: form.couleur || 'var(--color-primary)' }}>
                    <Tag className="w-4 h-4" />
                  </div>
                  <h2 className="cat-modal__title">
                    {modal.mode === 'create'
                      ? (lang === 'fr' ? 'Nouvelle catégorie' : 'New category')
                      : (lang === 'fr' ? 'Modifier la catégorie' : 'Edit category')}
                  </h2>
                </div>
                <button className="cat-modal__close" onClick={() => setModal(null)}>
                  <X className="w-4.5 h-4.5" />
                </button>
              </div>

              {error && (
                <div className="cat-modal__error">
                  <AlertTriangle className="w-4 h-4 shrink-0" />
                  {error}
                </div>
              )}

              <form onSubmit={handleSave} className="cat-modal__form">
                {/* AI Suggestions — create mode only */}
                {modal.mode === 'create' && (
                  <div className="cat-ai">
                    <button type="button" className="cat-ai__btn" onClick={handleSuggest} disabled={loadingSuggestions}>
                      {loadingSuggestions
                        ? <div className="cat-spinner cat-spinner--sm" />
                        : <Sparkles className="w-3.5 h-3.5" />}
                      {lang === 'fr' ? 'Suggestions IA' : 'AI Suggestions'}
                    </button>
                    <AnimatePresence>
                      {suggestions.length > 0 && (
                        <motion.div className="cat-ai__chips"
                          initial={{ opacity: 0, height: 0 }}
                          animate={{ opacity: 1, height: 'auto' }}
                          exit={{ opacity: 0, height: 0 }}>
                          {suggestions.map((s, i) => (
                            <button key={i} type="button" className="cat-ai__chip"
                              onClick={() => applySuggestion(s)} title={s.description}>
                              {s.nom}
                            </button>
                          ))}
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </div>
                )}

                <div className="cat-field">
                  <label>{lang === 'fr' ? 'Nom *' : 'Name *'}</label>
                  <input className="cat-input" required maxLength={150}
                    value={form.nom} onChange={e => setForm(f => ({ ...f, nom: e.target.value }))}
                    placeholder={lang === 'fr' ? 'Ex : Guide pédagogique' : 'e.g. Pedagogical guide'} />
                  <span className="cat-field__count">{form.nom.length}/150</span>
                </div>

                <div className="cat-field">
                  <label>{lang === 'fr' ? 'Description (optionnel)' : 'Description (optional)'}</label>
                  <textarea className="cat-input cat-input--textarea" rows={3} maxLength={1000}
                    value={form.description} onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
                    placeholder={lang === 'fr' ? 'Décrivez cette catégorie...' : 'Describe this category...'} />
                  <span className="cat-field__count">{form.description.length}/1000</span>
                </div>

                {/* Color picker */}
                <div className="cat-field">
                  <label>{lang === 'fr' ? 'Couleur' : 'Color'}</label>
                  <div className="cat-color-picker">
                    {PALETTE.map(c => (
                      <button key={c} type="button"
                        className={`cat-color-swatch ${form.couleur === c ? 'cat-color-swatch--active' : ''}`}
                        style={{ background: c }}
                        onClick={() => setForm(f => ({ ...f, couleur: c }))}>
                        {form.couleur === c && <Check className="w-3 h-3 text-white" />}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Preview */}
                {form.nom && (
                  <div className="cat-preview" style={{ borderColor: `${form.couleur}30`, background: `${form.couleur}08` }}>
                    <div className="cat-card__icon" style={{ background: `${form.couleur}20`, color: form.couleur }}>
                      <Tag className="w-4 h-4" />
                    </div>
                    <div className="cat-preview__text">
                      <span className="cat-preview__label">{lang === 'fr' ? 'Aperçu' : 'Preview'}</span>
                      <span className="cat-preview__name">{form.nom}</span>
                    </div>
                  </div>
                )}

                <div className="cat-modal__actions">
                  <button type="button" className="cat-btn cat-btn--ghost" onClick={() => setModal(null)}>
                    {lang === 'fr' ? 'Annuler' : 'Cancel'}
                  </button>
                  <button type="submit" className="cat-btn cat-btn--primary"
                    disabled={saving} style={{ background: form.couleur || 'var(--color-primary)' }}>
                    {saving ? <div className="cat-spinner" /> : <Check className="w-4 h-4" />}
                    {lang === 'fr' ? 'Enregistrer' : 'Save'}
                  </button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Header */}
      <motion.div className="cat-header"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
        <div className="cat-header__info">
          <div className="cat-header__icon-wrap">
            <FolderOpen className="w-5 h-5" style={{ color: 'var(--color-primary)' }} />
          </div>
          <div>
            <h1 className="cat-header__title">{lang === 'fr' ? 'Catégories' : 'Categories'}</h1>
            <p className="cat-header__sub">
              {loading ? '—' : `${filtered.length} / ${categories.length}`}{' '}
              {lang === 'fr' ? 'catégorie(s)' : 'category(ies)'}
            </p>
          </div>
        </div>
        <button className="cat-btn cat-btn--primary" onClick={openCreate}>
          <Plus className="w-4 h-4" />
          {lang === 'fr' ? 'Nouvelle catégorie' : 'New category'}
        </button>
      </motion.div>

      {/* Toolbar */}
      <motion.div className="cat-toolbar"
        initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.06, type: 'spring', stiffness: 260, damping: 22 }}>
        <div className="cat-search">
          <Search className="cat-search__icon" />
          <input
            className="cat-search__input"
            value={search}
            onChange={e => setSearch(e.target.value)}
            placeholder={lang === 'fr' ? 'Rechercher une catégorie...' : 'Search categories...'}
          />
          {search && (
            <button className="cat-search__clear" onClick={() => setSearch('')}>
              <X className="w-3.5 h-3.5" />
            </button>
          )}
        </div>
        <div className="cat-sort">
          <button
            className={`cat-sort__btn ${sortBy === 'name' ? 'cat-sort__btn--active' : ''}`}
            onClick={() => setSortBy('name')}>
            <ArrowDownAZ className="w-3.5 h-3.5" />
            {lang === 'fr' ? 'A→Z' : 'A→Z'}
          </button>
          <button
            className={`cat-sort__btn ${sortBy === 'count' ? 'cat-sort__btn--active' : ''}`}
            onClick={() => setSortBy('count')}>
            <BarChart3 className="w-3.5 h-3.5" />
            {lang === 'fr' ? 'Docs' : 'Docs'}
          </button>
        </div>
      </motion.div>

      {/* Grid */}
      {loading ? (
        <div className="cat-grid">
          {[...Array(6)].map((_, i) => <SkeletonCard key={i} />)}
        </div>
      ) : filtered.length === 0 ? (
        <motion.div className="cat-empty" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <div className="cat-empty__icon-wrap">
            <FolderOpen className="w-8 h-8" style={{ color: 'var(--color-ink-muted)', opacity: 0.4 }} />
          </div>
          <p className="cat-empty__title">
            {search
              ? (lang === 'fr' ? 'Aucun résultat' : 'No results')
              : (lang === 'fr' ? 'Aucune catégorie' : 'No categories yet')}
          </p>
          <p className="cat-empty__sub">
            {search
              ? (lang === 'fr' ? `Rien ne correspond à « ${search} »` : `Nothing matches "${search}"`)
              : (lang === 'fr' ? 'Créez votre première catégorie.' : 'Create your first category.')}
          </p>
          {!search && (
            <button className="cat-btn cat-btn--primary" onClick={openCreate} style={{ marginTop: 8 }}>
              <Plus className="w-4 h-4" />
              {lang === 'fr' ? 'Nouvelle catégorie' : 'New category'}
            </button>
          )}
        </motion.div>
      ) : (
        <motion.div className="cat-grid"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: 0.08 }}>
          <AnimatePresence>
            {filtered.map((cat, i) => {
              const color = getCatColor(cat);
              const isConfirming = confirmDelete === cat.id;
              const isDeleting = deleting === cat.id;
              const hasDocuments = cat.documentCount > 0;

              return (
                <motion.div key={cat.id} className="cat-card"
                  layout
                  initial={{ opacity: 0, y: 12 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, scale: 0.95 }}
                  transition={{ delay: i * 0.04, type: 'spring', stiffness: 280, damping: 24 }}>

                  {/* Left accent bar */}
                  <div className="cat-card__accent" style={{ background: color }} />

                  <div className="cat-card__body">
                    <div className="cat-card__top">
                      <div className="cat-card__icon" style={{ background: `${color}18`, color }}>
                        <Tag className="w-4 h-4" />
                      </div>
                      <div className="cat-card__actions">
                        <button className="cat-icon-btn" onClick={() => openEdit(cat)}
                          title={lang === 'fr' ? 'Modifier' : 'Edit'}>
                          <Pencil className="w-3.5 h-3.5" />
                        </button>
                        <button
                          className="cat-icon-btn cat-icon-btn--danger"
                          onClick={() => setConfirmDelete(cat.id)}
                          disabled={isDeleting || hasDocuments}
                          title={hasDocuments
                            ? (lang === 'fr' ? `${cat.documentCount} document(s) utilisent cette catégorie` : `${cat.documentCount} document(s) use this category`)
                            : (lang === 'fr' ? 'Supprimer' : 'Delete')}>
                          {isDeleting ? <div className="cat-spinner cat-spinner--sm cat-spinner--dark" /> : <Trash2 className="w-3.5 h-3.5" />}
                        </button>
                      </div>
                    </div>

                    <h3 className="cat-card__name">{cat.nom}</h3>
                    {cat.description && <p className="cat-card__desc">{cat.description}</p>}

                    <div className="cat-card__footer">
                      <span className="cat-card__badge" style={{ color, background: `${color}12`, border: `1px solid ${color}22` }}>
                        {cat.documentCount} {lang === 'fr' ? 'doc.' : 'doc.'}
                      </span>
                      {hasDocuments && (
                        <span className="cat-card__locked" title={lang === 'fr' ? 'Contient des documents' : 'Has documents'}>
                          {lang === 'fr' ? 'En usage' : 'In use'}
                        </span>
                      )}
                    </div>

                    {/* Inline delete confirmation */}
                    <AnimatePresence>
                      {isConfirming && (
                        <motion.div className="cat-card__confirm"
                          initial={{ opacity: 0, height: 0 }}
                          animate={{ opacity: 1, height: 'auto' }}
                          exit={{ opacity: 0, height: 0 }}
                          transition={{ duration: 0.18 }}>
                          <p className="cat-card__confirm-text">
                            {lang === 'fr' ? 'Supprimer cette catégorie ?' : 'Delete this category?'}
                          </p>
                          <div className="cat-card__confirm-actions">
                            <button className="cat-btn cat-btn--ghost cat-btn--sm"
                              onClick={() => setConfirmDelete(null)}>
                              {lang === 'fr' ? 'Annuler' : 'Cancel'}
                            </button>
                            <button className="cat-btn cat-btn--danger cat-btn--sm"
                              onClick={() => handleDelete(cat)} disabled={isDeleting}>
                              {isDeleting ? <div className="cat-spinner cat-spinner--sm" /> : <Trash2 className="w-3 h-3" />}
                              {lang === 'fr' ? 'Supprimer' : 'Delete'}
                            </button>
                          </div>
                        </motion.div>
                      )}
                    </AnimatePresence>
                  </div>
                </motion.div>
              );
            })}
          </AnimatePresence>
        </motion.div>
      )}
    </div>
  );
}
