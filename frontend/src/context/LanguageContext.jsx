import { createContext, useContext, useState, useCallback } from 'react';

const LanguageContext = createContext(null);
const STORAGE_KEY = 'pf_lang';

const translations = {
  fr: {
    // Common
    academicPlatform: 'Plateforme academique',
    back: 'Retour',
    email: 'Email',
    password: 'Mot de passe',
    loading: 'Chargement...',
    cancel: 'Annuler',
    save: 'Enregistrer',

    // Login
    loginTitle: 'Bon retour',
    loginSubtitle: 'Connectez-vous pour acceder a vos documents et programmes de formation.',
    loginRememberMe: 'Se souvenir de moi',
    loginForgotPassword: 'Mot de passe oublie ?',
    loginSubmit: 'Se connecter',
    loginSubmitting: 'Connexion...',
    loginNoAccount: 'Pas encore de compte ?',
    loginRequestAccess: 'Demander un acces',
    loginError: 'Impossible de se connecter.',

    // Register
    registerTitle: 'Creer un compte',
    registerSubtitle: 'Remplissez vos informations pour demander un acces.',
    registerPhoto: 'Photo',
    registerUploading: 'Telechargement...',
    registerChoosePhoto: 'Choisir une photo (optionnel)',
    registerFullName: 'Nom complet',
    registerPassword: 'Mot de passe',
    registerRole: 'Role',
    registerRolePlaceholder: 'Selectionnez un role',
    registerCoordinator: 'Resp. Pedagogique',
    registerSubject: 'Matiere d\'enseignement',
    registerSubjectPlaceholder: 'Entrez votre matiere',
    registerDepartment: 'Departement / Filiere',
    registerDepartmentPlaceholder: 'Entrez votre departement ou filiere',
    registerPhone: 'Telephone (optionnel)',
    registerDob: 'Date de naissance (optionnel)',
    registerSubmit: 'Creer mon compte',
    registerSubmitting: 'Creation...',
    registerSuccess: 'Votre demande a ete envoyee.',
    registerError: 'Impossible de creer le compte.',
    registerHasAccount: 'Deja un compte ?',
    registerSignIn: 'Se connecter',

    // Forgot Password
    forgotTitle: 'Reinitialiser le mot de passe',
    forgotSubtitle: 'Entrez votre email et nous vous enverrons un lien pour reinitialiser votre mot de passe.',
    forgotSubmit: 'Envoyer le lien',
    forgotSubmitting: 'Envoi...',
    forgotSuccess: 'Si un compte existe avec cet email, un lien de reinitialisation a ete envoye. Verifiez votre boite de reception.',
    forgotBackToLogin: 'Retour a la connexion',
    forgotRemembered: 'Vous vous souvenez de votre mot de passe ?',
    forgotSignIn: 'Se connecter',
    forgotError: 'Une erreur s\'est produite. Reessayez.',

    // Reset Password
    resetTitle: 'Nouveau mot de passe',
    resetSubtitle: 'Choisissez un nouveau mot de passe pour votre compte.',
    resetNewPassword: 'Nouveau mot de passe',
    resetNewPasswordPlaceholder: '8 caracteres minimum',
    resetConfirmPassword: 'Confirmer le mot de passe',
    resetConfirmPlaceholder: 'Repetez votre nouveau mot de passe',
    resetSubmit: 'Reinitialiser',
    resetSubmitting: 'Enregistrement...',
    resetSuccess: 'Votre mot de passe a ete reinitialise. Redirection vers la connexion...',
    resetInvalidTitle: 'Lien invalide',
    resetInvalidSubtitle: 'Ce lien de reinitialisation est manquant ou malforme.',
    resetNewLink: 'Demander un nouveau lien',
    resetPasswordMismatch: 'Les mots de passe ne correspondent pas.',
    resetError: 'Ce lien de reinitialisation est invalide ou a expire.',

    // Dashboard
    dashboardGreetingMorning: 'Bonjour',
    dashboardGreetingAfternoon: 'Bon apres-midi',
    dashboardGreetingEvening: 'Bonsoir',
    dashboardWelcomeSubtitle: 'Bienvenue dans votre espace personnel.',
    dashboardNavDashboard: 'Tableau de bord',
    dashboardNavFormations: 'Formations',
    dashboardNavDocuments: 'Documents',
    dashboardNavChat: 'Messagerie',
    dashboardNavUsers: 'Utilisateurs',
    dashboardNavProfile: 'Mon profil',
    dashboardNavValidations: 'Validations',
    dashboardNavCategories: 'Catégories',
    dashboardNavAnalytics: 'Analytique',
    dashboardNavAuditLog: "Journal d'activité",
    dashboardStatFormations: 'Formations',
    dashboardStatDocuments: 'Documents',
    dashboardStatCompletion: 'Comptes en attente',
    dashboardStatTime: 'Utilisateurs actifs',
    dashboardQuickActions: 'Actions rapides',
    dashboardActionValidate: 'Valider les comptes',
    dashboardActionValidateDesc: 'Approuver ou refuser les nouvelles inscriptions',
    dashboardActionFormations: 'Mes formations',
    dashboardActionFormationsDesc: 'Consulter et gerer vos formations en cours',
    dashboardActionDocuments: 'Documents',
    dashboardActionDocumentsDesc: 'Acceder a vos supports de cours et documents',
    dashboardActionCollab: 'Collaborateurs',
    dashboardActionCollabDesc: 'Voir les membres de votre departement',
    dashboardRoleAdmin: 'Administrateur',
    dashboardRoleResp: 'Resp. Pedagogique',
    // User dashboard (non-admin)
    userDashSpaceTitle: 'Mon espace',
    userDashEditProfile: 'Mon profil',
    userDashMyFormations: 'Mes formations',
    userDashMyFormationsDesc: 'Accedez aux formations auxquelles vous participez ou que vous animez.',
    userDashMyDocs: 'Mes documents',
    userDashMyDocsDesc: 'Consultez et partagez vos supports de cours et ressources pedagogiques.',
    userDashChat: 'Messagerie',
    userDashChatDesc: 'Discutez en temps reel avec les autres responsables pedagogiques.',
    userDashColleagues: 'Mes collaborateurs',
    userDashColleaguesDesc: 'Retrouvez les membres de votre equipe et de votre departement.',
    userDashMyProfile: 'Mon profil',
    userDashMyProfileDesc: 'Mettez a jour vos informations et preferences personnelles.',

    // Admin
    adminTitle: 'Administration',
    adminBadge: 'en attente',
    adminHeaderTitle: 'Validations des comptes',
    adminHeaderSubtitle: 'Approuvez ou refusez les demandes d\'inscription des responsables pedagogiques.',
    adminTableUser: 'Utilisateur',
    adminTableRole: 'Role',
    adminTableDate: 'Demande le',
    adminTableActions: 'Actions',
    adminApprove: 'Approuver',
    adminReject: 'Refuser',
    adminLoading: 'Chargement des comptes en attente...',
    adminEmptyTitle: 'Tout est a jour',
    adminEmptyText: 'Aucun compte n\'attend d\'approbation.',
    adminRoleResp: 'Resp. Pedago.',
    adminRoleAdmin: 'Admin',
    adminLoadError: 'Impossible de charger les comptes en attente.',
    adminActionError: 'Impossible de traiter la demande.',

    // Settings
    settingsLangLabel: 'Langue',
    settingsThemeLabel: 'Theme',
  },
  en: {
    // Common
    academicPlatform: 'Academic platform',
    back: 'Back',
    email: 'Email',
    password: 'Password',
    loading: 'Loading...',
    cancel: 'Cancel',
    save: 'Save',

    // Login
    loginTitle: 'Welcome back',
    loginSubtitle: 'Sign in to access your documents and training programs.',
    loginRememberMe: 'Remember me',
    loginForgotPassword: 'Forgot password?',
    loginSubmit: 'Sign in',
    loginSubmitting: 'Signing in...',
    loginNoAccount: 'No account yet?',
    loginRequestAccess: 'Request access',
    loginError: 'Unable to sign in.',

    // Register
    registerTitle: 'Create an account',
    registerSubtitle: 'Fill in your details to request access.',
    registerPhoto: 'Photo',
    registerUploading: 'Uploading...',
    registerChoosePhoto: 'Choose a photo (optional)',
    registerFullName: 'Full name',
    registerPassword: 'Password',
    registerRole: 'Role',
    registerRolePlaceholder: 'Select a role',
    registerCoordinator: 'Academic coordinator',
    registerSubject: 'Teaching subject',
    registerSubjectPlaceholder: 'Enter your subject',
    registerDepartment: 'Department / track',
    registerDepartmentPlaceholder: 'Enter your department or track',
    registerPhone: 'Phone (optional)',
    registerDob: 'Date of birth (optional)',
    registerSubmit: 'Create my account',
    registerSubmitting: 'Creating...',
    registerSuccess: 'Your request has been sent.',
    registerError: 'Unable to create the account.',
    registerHasAccount: 'Already have an account?',
    registerSignIn: 'Sign in',

    // Forgot Password
    forgotTitle: 'Reset password',
    forgotSubtitle: 'Enter your email and we\'ll send you a link to reset your password.',
    forgotSubmit: 'Send reset link',
    forgotSubmitting: 'Sending...',
    forgotSuccess: 'If an account exists with that email, a reset link has been sent. Check your inbox and follow the instructions.',
    forgotBackToLogin: 'Back to sign in',
    forgotRemembered: 'Remembered your password?',
    forgotSignIn: 'Sign in',
    forgotError: 'Something went wrong. Please try again.',

    // Reset Password
    resetTitle: 'New password',
    resetSubtitle: 'Choose a new password for your account.',
    resetNewPassword: 'New password',
    resetNewPasswordPlaceholder: '8 characters minimum',
    resetConfirmPassword: 'Confirm password',
    resetConfirmPlaceholder: 'Repeat your new password',
    resetSubmit: 'Reset password',
    resetSubmitting: 'Saving...',
    resetSuccess: 'Your password has been reset. Redirecting you to sign in...',
    resetInvalidTitle: 'Invalid link',
    resetInvalidSubtitle: 'This password reset link is missing or malformed.',
    resetNewLink: 'Request a new link',
    resetPasswordMismatch: 'Passwords do not match.',
    resetError: 'This reset link is invalid or has expired.',

    // Dashboard
    dashboardGreetingMorning: 'Good morning',
    dashboardGreetingAfternoon: 'Good afternoon',
    dashboardGreetingEvening: 'Good evening',
    dashboardWelcomeSubtitle: 'Welcome to your personal space.',
    dashboardNavDashboard: 'Dashboard',
    dashboardNavFormations: 'Trainings',
    dashboardNavDocuments: 'Documents',
    dashboardNavChat: 'Chat',
    dashboardNavUsers: 'Users',
    dashboardNavProfile: 'My profile',
    dashboardNavValidations: 'Validations',
    dashboardNavCategories: 'Categories',
    dashboardNavAnalytics: 'Analytics',
    dashboardNavAuditLog: 'Audit log',
    dashboardStatFormations: 'Trainings',
    dashboardStatDocuments: 'Documents',
    dashboardStatCompletion: 'Pending accounts',
    dashboardStatTime: 'Active users',
    dashboardQuickActions: 'Quick actions',
    dashboardActionValidate: 'Validate accounts',
    dashboardActionValidateDesc: 'Approve or reject new registrations',
    dashboardActionFormations: 'My trainings',
    dashboardActionFormationsDesc: 'View and manage your ongoing trainings',
    dashboardActionDocuments: 'Documents',
    dashboardActionDocumentsDesc: 'Access your course materials and documents',
    dashboardActionCollab: 'Colleagues',
    dashboardActionCollabDesc: 'View members of your department',
    dashboardRoleAdmin: 'Administrator',
    dashboardRoleResp: 'Academic coordinator',
    // User dashboard (non-admin)
    userDashSpaceTitle: 'My space',
    userDashEditProfile: 'My profile',
    userDashMyFormations: 'My trainings',
    userDashMyFormationsDesc: 'Access the courses and programs you are part of or leading.',
    userDashMyDocs: 'My documents',
    userDashMyDocsDesc: 'View and share your course materials and teaching resources.',
    userDashChat: 'Chat',
    userDashChatDesc: 'Chat in real time with other academic coordinators.',
    userDashColleagues: 'My colleagues',
    userDashColleaguesDesc: 'Find members of your team and department.',
    userDashMyProfile: 'My profile',
    userDashMyProfileDesc: 'Update your personal information and preferences.',

    // Admin
    adminTitle: 'Administration',
    adminBadge: 'pending',
    adminHeaderTitle: 'Account validations',
    adminHeaderSubtitle: 'Approve or reject registration requests from academic coordinators.',
    adminTableUser: 'User',
    adminTableRole: 'Role',
    adminTableDate: 'Requested on',
    adminTableActions: 'Actions',
    adminApprove: 'Approve',
    adminReject: 'Reject',
    adminLoading: 'Loading pending accounts...',
    adminEmptyTitle: 'All caught up',
    adminEmptyText: 'No accounts are waiting for approval.',
    adminRoleResp: 'Coordinator',
    adminRoleAdmin: 'Admin',
    adminLoadError: 'Unable to load pending accounts.',
    adminActionError: 'Unable to process the request.',

    // Settings
    settingsLangLabel: 'Language',
    settingsThemeLabel: 'Theme',
  },
};

export function LanguageProvider({ children }) {
  const [lang, setLang] = useState(() => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'fr' || saved === 'en') return saved;
    } catch {
      // localStorage unavailable (private mode, disabled storage): fall through to browser locale.
    }
    return navigator.language.startsWith('fr') ? 'fr' : 'en';
  });

  const toggleLang = useCallback(() => {
    setLang((prev) => {
      const next = prev === 'fr' ? 'en' : 'fr';
      localStorage.setItem(STORAGE_KEY, next);
      return next;
    });
  }, []);

  const t = useCallback((key) => translations[lang]?.[key] ?? key, [lang]);

  return (
    <LanguageContext.Provider value={{ lang, toggleLang, t }}>
      {children}
    </LanguageContext.Provider>
  );
}

export function useLanguage() {
  const context = useContext(LanguageContext);
  if (!context) throw new Error('useLanguage must be used within LanguageProvider');
  return context;
}
