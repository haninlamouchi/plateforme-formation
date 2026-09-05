namespace PlateformeFormation.Api.Models;

public enum RoleUtilisateur { ADMINISTRATEUR, RESPONSABLE_PEDAGOGIQUE }
public enum StatutCompte { EN_ATTENTE, VALIDE, REFUSE }
public enum StatutTraitement { EN_ATTENTE, EN_COURS, DISPONIBLE, ERREUR }
public enum OrigineCategorie { MANUELLE, IA }
public enum StatutFormation { BROUILLON, VALIDEE }
public enum Emetteur { UTILISATEUR, ASSISTANT }
public enum DecisionDoublon { CONSERVER_LES_DEUX, REMPLACER, ANNULER, EN_ATTENTE }
public enum FormatExport { PDF, WORD, PPTX }