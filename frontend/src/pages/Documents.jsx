import { useState, useEffect, useCallback, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  FileText, Upload, Search, X, Trash2, Eye, Pencil, GraduationCap,
  Check, Calendar, HardDrive, User, Sparkles, ChevronLeft, ChevronRight, AlertTriangle, ChevronDown,
} from 'lucide-react';
import { getAllDocuments, uploadDocument, updateDocument, deleteDocument, getDocumentCompetences } from '../services/documentService';
import { getAllCategories, suggestCategoryForDocument } from '../services/categorieService';
import { useAuth } from '../context/AuthContext';
import { useLanguage } from '../context/LanguageContext';
import CompetencesModal from '../components/CompetencesModal';
import './Documents.css';

const PAGE_SIZE = 20;
const COLORS = ['#9B111E','#A67C1B','#1E8E5A','#2563EB','#7C3AED','#DB2777','#0891B2','#374151'];

function colorForName(name) {
  if (!name) return COLORS[0];
  let h = 0;
  for (let i = 0; i < name.length; i++) h = name.charCodeAt(i) + ((h << 5) - h);
  return COLORS[Math.abs(h) % COLORS.length];
}

function formatSize(bytes) {
  if (!bytes) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export default function Documents() {
  const { user } = useAuth();
  const { lang } = useLanguage();
  const isAdmin = user?.role === 'ADMINISTRATEUR';

  const [docs, setDocs] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [catFilter, setCatFilter] = useState('');
  const [catOpen, setCatOpen] = useState(false);
  const [catQuery, setCatQuery] = useState('');
  const catRef = useRef();
  const [toast, setToast] = useState(null);
  const [confirmDoc, setConfirmDoc] = useState(null); // doc awaiting delete confirmation
  const [deleting, setDeleting] = useState(null);

  // Upload modal
  const [uploadOpen, setUploadOpen] = useState(false);
  const [uploadForm, setUploadForm] = useState({ titre: '', categorieId: '', typeDocument: '', langue: '' });
  const [uploadFiles, setUploadFiles] = useState([]); // batch when length > 1
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState('');
  const [suggestingCat, setSuggestingCat] = useState(false);
  const [aiCatBadge, setAiCatBadge] = useState(null);
  const [batchStatus, setBatchStatus] = useState({}); // index -> 'pending' | 'success' | 'duplicate' | 'error'
  const fileRef = useRef();
  const uploadFile = uploadFiles.length === 1 ? uploadFiles[0] : null;

  // Edit modal
  const [editDoc, setEditDoc] = useState(null);
  const [editForm, setEditForm] = useState({});
  const [editSaving, setEditSaving] = useState(false);

  // Competences popup
  const [competencesDoc, setCompetencesDoc] = useState(null);
  const [loadingCompetencesId, setLoadingCompetencesId] = useState(null);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  const showToast = (msg, type = 'success') => {
    setToast({ msg, type });
    setTimeout(() => setToast(null), 3000);
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const params = { page, pageSize: PAGE_SIZE };
      if (catFilter) params.categorieId = catFilter;
      if (search) params.search = search;
      const [result, catsData] = await Promise.all([getAllDocuments(params), getAllCategories()]);
      const items = result.items ?? [];
      setDocs(items);
      setTotal(result.total ?? 0);
      setCategories(catsData);
    } catch {
      showToast(lang === 'fr' ? 'Erreur de chargement.' : 'Load error.', 'error');
    } finally {
      setLoading(false);
    }
  }, [catFilter, search, lang, page]);

  useEffect(() => {
    const t = setTimeout(() => load(), search ? 350 : 0);
    return () => clearTimeout(t);
  }, [load, search]);

  // Close category dropdown on outside click
  useEffect(() => {
    function onClickOutside(e) {
      if (catRef.current && !catRef.current.contains(e.target)) {
        setCatOpen(false);
        setCatQuery('');
      }
    }
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, []);

  const filteredCategories = categories.filter(c =>
    c.nom.toLowerCase().includes(catQuery.toLowerCase()));
  const selectedCatNom = categories.find(c => String(c.id) === catFilter)?.nom;

  function openUpload() {
    setUploadForm({ titre: '', categorieId: '', typeDocument: '', langue: '' });
    setUploadFiles([]);
    setUploadError('');
    setAiCatBadge(null);
    setBatchStatus({});
    setUploadOpen(true);
  }

  async function handleAiSuggestCategory(file) {
    setSuggestingCat(true);
    setAiCatBadge(null);
    try {
      const result = await suggestCategoryForDocument(file);
      if (result?.categoryId) {
        setUploadForm(f => ({ ...f, categorieId: String(result.categoryId) }));
        setAiCatBadge({ nom: result.categoryNom, isNew: result.isNew });
        if (result.isNew) {
          const updated = await getAllCategories();
          setCategories(updated);
        }
      }
    } catch {
      showToast(lang === 'fr' ? 'Erreur IA. Réessayez.' : 'AI error. Try again.', 'error');
    } finally {
      setSuggestingCat(false);
    }
  }

  function pickPdfFiles(fileList) {
    const files = Array.from(fileList || []);
    const pdfs = files.filter(f => f.type === 'application/pdf' || f.name.toLowerCase().endsWith('.pdf'));
    if (pdfs.length < files.length) {
      showToast(lang === 'fr' ? 'Seuls les fichiers PDF sont acceptés.' : 'Only PDF files are accepted.', 'error');
    }
    return pdfs;
  }

  function handleFileSelect(e) {
    const pdfs = pickPdfFiles(e.target.files);
    if (pdfs.length === 0) return;
    setUploadFiles(pdfs);
    setBatchStatus({});
    if (pdfs.length === 1 && !uploadForm.titre) {
      setUploadForm(f => ({ ...f, titre: pdfs[0].name.replace(/\.pdf$/i, '') }));
    }
  }

  function handleDrop(e) {
    e.preventDefault();
    const pdfs = pickPdfFiles(e.dataTransfer.files);
    if (pdfs.length === 0) return;
    setUploadFiles(pdfs);
    setBatchStatus({});
    if (pdfs.length === 1 && !uploadForm.titre) {
      setUploadForm(f => ({ ...f, titre: pdfs[0].name.replace(/\.pdf$/i, '') }));
    }
  }

  async function handleUpload(e) {
    e.preventDefault();
    if (uploadFiles.length === 0) {
      setUploadError(lang === 'fr' ? 'Veuillez choisir un ou plusieurs fichiers PDF.' : 'Please select one or more PDF files.');
      return;
    }
    setUploadError('');
    setUploading(true);

    if (uploadFiles.length === 1) {
      try {
        await uploadDocument({
          file: uploadFiles[0],
          titre: uploadForm.titre,
          categorieId: uploadForm.categorieId || undefined,
          typeDocument: uploadForm.typeDocument || undefined,
          langue: uploadForm.langue || undefined,
        });
        showToast(lang === 'fr' ? 'Document ajouté.' : 'Document added.');
        setUploadOpen(false);
        load();
      } catch (err) {
        setUploadError(err.response?.data?.message || (lang === 'fr' ? 'Erreur lors du téléversement.' : 'Upload failed.'));
      } finally {
        setUploading(false);
      }
      return;
    }

    // Batch mode: one title per file (its filename), shared category/type/langue.
    // Uploaded sequentially — indexing is triggered per document server-side and
    // concurrent uploads would just queue up anyway.
    let successCount = 0, duplicateCount = 0, errorCount = 0;
    for (let i = 0; i < uploadFiles.length; i++) {
      setBatchStatus(prev => ({ ...prev, [i]: 'pending' }));
      try {
        await uploadDocument({
          file: uploadFiles[i],
          titre: uploadFiles[i].name.replace(/\.pdf$/i, ''),
          categorieId: uploadForm.categorieId || undefined,
          typeDocument: uploadForm.typeDocument || undefined,
          langue: uploadForm.langue || undefined,
        });
        successCount++;
        setBatchStatus(prev => ({ ...prev, [i]: 'success' }));
      } catch (err) {
        if (err.response?.status === 409) {
          duplicateCount++;
          setBatchStatus(prev => ({ ...prev, [i]: 'duplicate' }));
        } else {
          errorCount++;
          setBatchStatus(prev => ({ ...prev, [i]: 'error' }));
        }
      }
    }

    setUploading(false);
    load();
    const parts = [];
    if (successCount) parts.push(lang === 'fr' ? `${successCount} ajouté(s)` : `${successCount} added`);
    if (duplicateCount) parts.push(lang === 'fr' ? `${duplicateCount} déjà existant(s)` : `${duplicateCount} already existed`);
    if (errorCount) parts.push(lang === 'fr' ? `${errorCount} en échec` : `${errorCount} failed`);
    showToast(parts.join(' · '), errorCount > 0 && successCount === 0 ? 'error' : 'success');
    if (errorCount === 0) setUploadOpen(false);
  }

  function openEdit(doc) {
    setEditDoc(doc);
    setEditForm({
      titre: doc.titre,
      categorieId: doc.categorieId != null ? String(doc.categorieId) : '',
      typeDocument: doc.typeDocument ?? '',
      langue: doc.langue ?? '',
    });
  }

  async function handleEditSave(e) {
    e.preventDefault();
    setEditSaving(true);
    try {
      await updateDocument(editDoc.id, {
        titre: editForm.titre,
        categorieId: editForm.categorieId ? Number(editForm.categorieId) : null,
        typeDocument: editForm.typeDocument || null,
        langue: editForm.langue || null,
      });
      showToast(lang === 'fr' ? 'Document mis à jour.' : 'Document updated.');
      setEditDoc(null);
      load();
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setEditSaving(false);
    }
  }

  async function doDelete(doc) {
    setDeleting(doc.id);
    try {
      await deleteDocument(doc.id);
      showToast(lang === 'fr' ? 'Document supprimé.' : 'Document deleted.');
      load();
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setDeleting(null);
    }
  }

  async function handleShowCompetences(doc) {
    setLoadingCompetencesId(doc.id);
    try {
      const data = await getDocumentCompetences(doc.id);
      if (!data.ready) {
        showToast(
          lang === 'fr' ? "Ce document n'est pas encore indexé." : 'This document is not indexed yet.',
          'error'
        );
        return;
      }
      setCompetencesDoc({ titre: doc.titre, competences: data.competences });
    } catch (err) {
      showToast(err.response?.data?.message || (lang === 'fr' ? 'Erreur.' : 'Error.'), 'error');
    } finally {
      setLoadingCompetencesId(null);
    }
  }

  return (
    <div className="docs">
      {/* Toast */}
      <AnimatePresence>
        {toast && (
          <motion.div className={`docs-toast docs-toast--${toast.type}`}
            initial={{ opacity: 0, y: -14 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -14 }}>
            {toast.msg}
          </motion.div>
        )}
      </AnimatePresence>

      {/* Competences popup */}
      <AnimatePresence>
        {competencesDoc && (
          <CompetencesModal
            title={competencesDoc.titre}
            competences={competencesDoc.competences}
            onClose={() => setCompetencesDoc(null)}
            lang={lang}
          />
        )}
      </AnimatePresence>

      {/* Delete confirmation bar */}
      <AnimatePresence>
        {confirmDoc && (
          <motion.div className="docs-confirm-bar"
            initial={{ opacity: 0, y: 16 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: 16 }}>
            <AlertTriangle className="w-4 h-4 shrink-0" />
            <span className="docs-confirm-bar__text">
              {lang === 'fr'
                ? `Supprimer "${confirmDoc.titre}" ?`
                : `Delete "${confirmDoc.titre}"?`}
            </span>
            <button className="docs-confirm-bar__btn docs-confirm-bar__btn--danger"
              disabled={deleting === confirmDoc.id}
              onClick={() => { doDelete(confirmDoc); setConfirmDoc(null); }}>
              {deleting === confirmDoc.id
                ? <div className="docs-spinner docs-spinner--sm" />
                : (lang === 'fr' ? 'Supprimer' : 'Delete')}
            </button>
            <button className="docs-confirm-bar__btn" onClick={() => setConfirmDoc(null)}>
              {lang === 'fr' ? 'Annuler' : 'Cancel'}
            </button>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Upload modal */}
      <AnimatePresence>
        {uploadOpen && (
          <motion.div className="docs-overlay" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => setUploadOpen(false)}>
            <motion.div className="docs-modal"
              initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 16 }}
              transition={{ type: 'spring', stiffness: 320, damping: 28 }}
              onClick={e => e.stopPropagation()}>

              <div className="docs-modal__header">
                <h2 className="docs-modal__title">{lang === 'fr' ? 'Ajouter un document' : 'Add a document'}</h2>
                <button className="docs-modal__close" onClick={() => setUploadOpen(false)}><X className="w-5 h-5" /></button>
              </div>

              {uploadError && <div className="docs-modal__error">{uploadError}</div>}

              <form onSubmit={handleUpload} className="docs-modal__form">
                <div className={`docs-dropzone ${uploadFiles.length ? 'docs-dropzone--filled' : ''}`}
                  onClick={() => fileRef.current?.click()}
                  onDragOver={e => e.preventDefault()}
                  onDrop={handleDrop}>
                  <input ref={fileRef} type="file" accept=".pdf,application/pdf" multiple style={{ display: 'none' }} onChange={handleFileSelect} />
                  {uploadFile ? (
                    <>
                      <div className="docs-dropzone__icon docs-dropzone__icon--filled">
                        <FileText className="w-6 h-6 text-white" />
                      </div>
                      <p className="docs-dropzone__name">{uploadFile.name}</p>
                      <p className="docs-dropzone__size">{formatSize(uploadFile.size)}</p>
                    </>
                  ) : uploadFiles.length > 1 ? (
                    <>
                      <div className="docs-dropzone__icon docs-dropzone__icon--filled">
                        <FileText className="w-6 h-6 text-white" />
                      </div>
                      <p className="docs-dropzone__name">
                        {lang === 'fr' ? `${uploadFiles.length} fichiers sélectionnés` : `${uploadFiles.length} files selected`}
                      </p>
                      <p className="docs-dropzone__size">
                        {lang === 'fr' ? 'Cliquez pour changer la sélection' : 'Click to change the selection'}
                      </p>
                    </>
                  ) : (
                    <>
                      <Upload className="docs-dropzone__upload-icon" />
                      <p className="docs-dropzone__hint">
                        {lang === 'fr' ? 'Glissez un ou plusieurs PDF ici ou cliquez pour choisir' : 'Drop one or more PDFs here or click to browse'}
                      </p>
                      <p className="docs-dropzone__limit">PDF · max 20 MB</p>
                    </>
                  )}
                </div>

                {uploadFiles.length > 1 && (
                  <ul className="docs-batch-list">
                    {uploadFiles.map((f, i) => (
                      <li key={`${f.name}-${i}`} className={`docs-batch-list__item docs-batch-list__item--${batchStatus[i] || 'idle'}`}>
                        <span className="docs-batch-list__name">{f.name}</span>
                        {batchStatus[i] === 'pending' && <div className="docs-spinner docs-spinner--sm" />}
                        {batchStatus[i] === 'success' && <Check className="w-3.5 h-3.5" />}
                        {batchStatus[i] === 'duplicate' && <span className="docs-batch-list__note">{lang === 'fr' ? 'déjà existant' : 'duplicate'}</span>}
                        {batchStatus[i] === 'error' && <AlertTriangle className="w-3.5 h-3.5" />}
                      </li>
                    ))}
                  </ul>
                )}

                {uploadFiles.length <= 1 && (
                  <div className="docs-field">
                    <label>{lang === 'fr' ? 'Titre *' : 'Title *'}</label>
                    <input className="docs-input" required value={uploadForm.titre}
                      onChange={e => setUploadForm(f => ({ ...f, titre: e.target.value }))}
                      placeholder={lang === 'fr' ? 'Titre du document' : 'Document title'} />
                  </div>
                )}

                <div className="docs-form__row">
                  <div className="docs-field">
                    <label className="docs-field__label-row">
                      <span>{lang === 'fr' ? 'Catégorie' : 'Category'}</span>
                      {uploadFile && (
                        <button type="button" className="docs-ai-btn" onClick={() => handleAiSuggestCategory(uploadFile)} disabled={suggestingCat}>
                          {suggestingCat ? <div className="docs-spinner docs-spinner--sm" /> : <Sparkles className="w-3 h-3" />}
                          IA
                        </button>
                      )}
                    </label>
                    <select className="docs-input" value={uploadForm.categorieId}
                      onChange={e => { setUploadForm(f => ({ ...f, categorieId: e.target.value })); setAiCatBadge(null); }}>
                      <option value="">{lang === 'fr' ? '— Aucune —' : '— None —'}</option>
                      {categories.map(c => <option key={c.id} value={c.id}>{c.nom}</option>)}
                    </select>
                    {aiCatBadge && (
                      <span className={`docs-ai-badge ${aiCatBadge.isNew ? 'docs-ai-badge--new' : ''}`}>
                        {aiCatBadge.isNew
                          ? (lang === 'fr' ? `✨ Nouvelle catégorie créée : ${aiCatBadge.nom}` : `✨ New category created: ${aiCatBadge.nom}`)
                          : (lang === 'fr' ? `✨ Suggérée : ${aiCatBadge.nom}` : `✨ Suggested: ${aiCatBadge.nom}`)}
                      </span>
                    )}
                  </div>
                  <div className="docs-field">
                    <label>{lang === 'fr' ? 'Langue' : 'Language'}</label>
                    <select className="docs-input" value={uploadForm.langue}
                      onChange={e => setUploadForm(f => ({ ...f, langue: e.target.value }))}>
                      <option value="">—</option>
                      <option value="fr">Français</option>
                      <option value="en">English</option>
                      <option value="ar">العربية</option>
                    </select>
                  </div>
                </div>

                <div className="docs-modal__actions">
                  <button type="button" className="docs-btn docs-btn--ghost" onClick={() => setUploadOpen(false)}>
                    {lang === 'fr' ? 'Annuler' : 'Cancel'}
                  </button>
                  <button type="submit" className="docs-btn docs-btn--primary" disabled={uploading}>
                    {uploading ? <div className="docs-spinner" /> : <Upload className="w-4 h-4" />}
                    {uploadFiles.length > 1
                      ? (lang === 'fr' ? `Téléverser (${uploadFiles.length})` : `Upload (${uploadFiles.length})`)
                      : (lang === 'fr' ? 'Téléverser' : 'Upload')}
                  </button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Edit modal */}
      <AnimatePresence>
        {editDoc && (
          <motion.div className="docs-overlay" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }}
            onClick={() => setEditDoc(null)}>
            <motion.div className="docs-modal"
              initial={{ opacity: 0, scale: 0.95, y: 16 }} animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 16 }}
              transition={{ type: 'spring', stiffness: 320, damping: 28 }}
              onClick={e => e.stopPropagation()}>

              <div className="docs-modal__header">
                <h2 className="docs-modal__title">{lang === 'fr' ? 'Modifier le document' : 'Edit document'}</h2>
                <button className="docs-modal__close" onClick={() => setEditDoc(null)}><X className="w-5 h-5" /></button>
              </div>

              <form onSubmit={handleEditSave} className="docs-modal__form">
                <div className="docs-field">
                  <label>{lang === 'fr' ? 'Titre *' : 'Title *'}</label>
                  <input className="docs-input" required value={editForm.titre}
                    onChange={e => setEditForm(f => ({ ...f, titre: e.target.value }))} />
                </div>
                <div className="docs-form__row">
                  <div className="docs-field">
                    <label>{lang === 'fr' ? 'Catégorie' : 'Category'}</label>
                    <select className="docs-input" value={editForm.categorieId}
                      onChange={e => setEditForm(f => ({ ...f, categorieId: e.target.value }))}>
                      <option value="">{lang === 'fr' ? '— Aucune —' : '— None —'}</option>
                      {categories.map(c => <option key={c.id} value={String(c.id)}>{c.nom}</option>)}
                    </select>
                  </div>
                  <div className="docs-field">
                    <label>{lang === 'fr' ? 'Type' : 'Type'}</label>
                    <input className="docs-input" value={editForm.typeDocument}
                      onChange={e => setEditForm(f => ({ ...f, typeDocument: e.target.value }))}
                      placeholder={lang === 'fr' ? 'ex: Support de cours' : 'e.g. Course material'} />
                  </div>
                </div>
                <div className="docs-field">
                  <label>{lang === 'fr' ? 'Langue' : 'Language'}</label>
                  <select className="docs-input" value={editForm.langue}
                    onChange={e => setEditForm(f => ({ ...f, langue: e.target.value }))}>
                    <option value="">—</option>
                    <option value="fr">Français</option>
                    <option value="en">English</option>
                    <option value="ar">العربية</option>
                  </select>
                </div>
                <div className="docs-modal__actions">
                  <button type="button" className="docs-btn docs-btn--ghost" onClick={() => setEditDoc(null)}>
                    {lang === 'fr' ? 'Annuler' : 'Cancel'}
                  </button>
                  <button type="submit" className="docs-btn docs-btn--primary" disabled={editSaving}>
                    {editSaving ? <div className="docs-spinner" /> : <Check className="w-4 h-4" />}
                    {lang === 'fr' ? 'Enregistrer' : 'Save'}
                  </button>
                </div>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Page header */}
      <motion.div className="docs-header"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22 }}>
        <div className="docs-header__info">
          <FileText className="docs-header__icon" />
          <div>
            <h1 className="docs-header__title">{lang === 'fr' ? 'Documents' : 'Documents'}</h1>
            <p className="docs-header__sub">
              {total} {lang === 'fr' ? 'document(s)' : 'document(s)'}
            </p>
          </div>
        </div>
        <button className="docs-btn docs-btn--primary" onClick={openUpload}>
          <Upload className="w-4 h-4" />
          {lang === 'fr' ? 'Ajouter un document' : 'Add document'}
        </button>
      </motion.div>

      {/* Filters */}
      <motion.div className="docs-filters"
        initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
        transition={{ type: 'spring', stiffness: 260, damping: 22, delay: 0.06 }}>
        <div className="docs-search">
          <Search className="docs-search__icon" />
          <input className="docs-search__input"
            placeholder={lang === 'fr' ? 'Rechercher un document...' : 'Search documents...'}
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }} />
          {search && <button className="docs-search__clear" onClick={() => { setSearch(''); setPage(1); }}><X className="w-4 h-4" /></button>}
        </div>
        <div className="docs-cat-select-wrap" ref={catRef}>
          <button type="button" className="docs-cat-select"
            onClick={() => setCatOpen(o => !o)}>
            <span className={selectedCatNom ? '' : 'docs-cat-select__placeholder'}>
              {selectedCatNom || (lang === 'fr' ? 'Toutes les catégories' : 'All categories')}
            </span>
            <ChevronDown className={`docs-cat-select__icon ${catOpen ? 'docs-cat-select__icon--up' : ''}`} />
          </button>

          <AnimatePresence>
            {catOpen && (
              <motion.div className="docs-cat-dropdown"
                initial={{ opacity: 0, y: -6 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: -6 }}
                transition={{ duration: 0.15 }}>
                <div className="docs-cat-dropdown__search">
                  <Search className="docs-cat-dropdown__search-icon" />
                  <input autoFocus className="docs-cat-dropdown__search-input"
                    placeholder={lang === 'fr' ? 'Rechercher une catégorie...' : 'Search categories...'}
                    value={catQuery}
                    onChange={e => setCatQuery(e.target.value)} />
                </div>
                <div className="docs-cat-dropdown__list">
                  <button type="button"
                    className={`docs-cat-dropdown__item ${!catFilter ? 'docs-cat-dropdown__item--active' : ''}`}
                    onClick={() => { setCatFilter(''); setCatOpen(false); setCatQuery(''); setPage(1); }}>
                    {lang === 'fr' ? 'Toutes les catégories' : 'All categories'}
                  </button>
                  {filteredCategories.length === 0 ? (
                    <p className="docs-cat-dropdown__empty">
                      {lang === 'fr' ? 'Aucune catégorie trouvée.' : 'No categories found.'}
                    </p>
                  ) : filteredCategories.map(c => (
                    <button key={c.id} type="button"
                      className={`docs-cat-dropdown__item ${catFilter === String(c.id) ? 'docs-cat-dropdown__item--active' : ''}`}
                      onClick={() => { setCatFilter(String(c.id)); setCatOpen(false); setCatQuery(''); setPage(1); }}>
                      <span className="docs-cat-dropdown__dot" style={{ background: colorForName(c.nom) }} />
                      {c.nom}
                    </button>
                  ))}
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </motion.div>

      {/* Documents grid */}
      {loading ? (
        <div className="docs-loading"><div className="docs-spinner docs-spinner--lg" /></div>
      ) : docs.length === 0 ? (
        <motion.div className="docs-empty" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <FileText className="docs-empty__icon" />
          <p>{search || catFilter
            ? (lang === 'fr' ? 'Aucun document trouvé.' : 'No documents found.')
            : (lang === 'fr' ? 'Aucun document. Ajoutez-en un !' : 'No documents yet. Add one!')}</p>
        </motion.div>
      ) : (
        <motion.div className="docs-grid"
          initial={{ opacity: 0 }} animate={{ opacity: 1 }} transition={{ delay: 0.08 }}>
          {docs.map((doc, i) => {
            const catColor = doc.categorieNom ? colorForName(doc.categorieNom) : '#6B7280';
            const mine = doc.uploadedBy === Number(user?.id);
            return (
              <motion.div key={doc.id} className="docs-card"
                initial={{ opacity: 0, y: 12 }} animate={{ opacity: 1, y: 0 }}
                transition={{ delay: i * 0.03, type: 'spring', stiffness: 280, damping: 24 }}>

                <div className="docs-card__top">
                  <a href={doc.fileUrl} target="_blank" rel="noreferrer" className="docs-card__pdf-icon" title={lang === 'fr' ? 'Ouvrir le PDF' : 'Open PDF'}>
                    <FileText className="w-8 h-8 text-white" />
                    <span className="docs-card__pdf-label">PDF</span>
                  </a>
                  <div className="docs-card__actions">
                    <a href={doc.fileUrl} target="_blank" rel="noreferrer"
                      className="docs-icon-btn" title={lang === 'fr' ? 'Voir' : 'View'}>
                      <Eye className="w-3.5 h-3.5" />
                    </a>
                    <button className="docs-icon-btn" onClick={() => handleShowCompetences(doc)}
                      disabled={loadingCompetencesId === doc.id}
                      title={lang === 'fr' ? 'Compétences' : 'Competences'}>
                      {loadingCompetencesId === doc.id
                        ? <div className="docs-spinner docs-spinner--sm" />
                        : <GraduationCap className="w-3.5 h-3.5" />}
                    </button>
                    {(isAdmin || mine) && (
                      <>
                        <button className="docs-icon-btn" onClick={() => openEdit(doc)} title={lang === 'fr' ? 'Modifier' : 'Edit'}>
                          <Pencil className="w-3.5 h-3.5" />
                        </button>
                        <button className="docs-icon-btn docs-icon-btn--danger"
                          onClick={() => setConfirmDoc(doc)}
                          disabled={deleting === doc.id}
                          title={lang === 'fr' ? 'Supprimer' : 'Delete'}>
                          {deleting === doc.id ? <div className="docs-spinner docs-spinner--sm" /> : <Trash2 className="w-3.5 h-3.5" />}
                        </button>
                      </>
                    )}
                  </div>
                </div>

                <h3 className="docs-card__title" title={doc.titre}>{doc.titre}</h3>

                <div className="docs-card__cat-row">
                  {doc.categorieNom && (
                    <span className="docs-card__cat" style={{ background: catColor + '18', color: catColor }}>
                      {doc.categorieNom}
                    </span>
                  )}
                </div>

                <div className="docs-card__meta">
                  <span className="docs-card__meta-item">
                    <User className="w-3 h-3" />
                    {doc.uploaderNom}
                  </span>
                  <span className="docs-card__meta-item">
                    <Calendar className="w-3 h-3" />
                    {new Date(doc.dateAjout).toLocaleDateString(lang === 'fr' ? 'fr-FR' : 'en-US', { day: '2-digit', month: 'short', year: 'numeric' })}
                  </span>
                  <span className="docs-card__meta-item">
                    <HardDrive className="w-3 h-3" />
                    {formatSize(doc.tailleFichier)}
                  </span>
                </div>
              </motion.div>
            );
          })}
        </motion.div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <motion.div className="docs-pagination" initial={{ opacity: 0 }} animate={{ opacity: 1 }}>
          <button className="docs-pagination__btn" disabled={page === 1} onClick={() => setPage(p => p - 1)}>
            <ChevronLeft className="w-4 h-4" />
          </button>
          <span className="docs-pagination__label">
            {lang === 'fr' ? `Page ${page} / ${totalPages}` : `Page ${page} of ${totalPages}`}
          </span>
          <button className="docs-pagination__btn" disabled={page === totalPages} onClick={() => setPage(p => p + 1)}>
            <ChevronRight className="w-4 h-4" />
          </button>
        </motion.div>
      )}
    </div>
  );
}
