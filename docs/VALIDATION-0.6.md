# Validation de la préparation 0.6

Contrôles réalisés le 16 octobre 2026. Cette branche prépare une PR et aucune
release 0.6 n’est publiée. Le cycle automatisé de maintenance a été exécuté
sur le poste avec autorisation ; la version 0.6 reste installée à son issue.

| Contrôle | Résultat |
|---|---|
| Compilation Release contre Revit 2027 local | Réussie, sans avertissement |
| Tests hors Revit | 62 réussis, dont configuration, migration, états du panneau et contrôle de désinstallation |
| Tests natifs Revit 0.6 | Non exécutés : les 2 tests natifs restent volontairement exclus |
| Rendu WPF | Inspecté : taille normale, taille minimale 600 × 600 et liste d’assistants ; palette rouge/noir/blanc et gris neutres |
| États du panneau | Connexion désactivée avant sélection ou sans serveur ; clic explicite, profil choisi, liste déroulante et configuration avancée vérifiés |
| Construction EXE/MSI | Réussie, sans avertissement ni suppression de contrôles MSI |
| Fenêtre de désinstallation | Rendu WPF inspecté sous .NET 10 et sous l’hôte .NET Framework 4.8 ; timer testé, blocage de Continuer, nouvelle session au clic et erreur de détection vérifiés |
| Action du MSI seule, sans installation | DLL DTF embarquée chargée dans CLR v4 : succès sans Revit, refus MSI 1603 avec un processus factice nommé Revit, succès après retrait de la fixture ; le contrôle ne ferme pas ce processus |
| Inspection du MSI construit | ALLUSERS=1, registre HKLM, manifeste commun et chemin DLL formaté ; aucune action du serveur élevée |
| Garde-fou de manifeste préexistant | Action AppSearch et conditions MSI vérifiées : racine système réelle détectée, copie à racine temporaire avec manifeste présent refusé et préservé, absence acceptée et racine inconnue refusée ; aucune écriture du registre de test |
| Image administrative du MSI | Extraction réussie dans le dossier de contrôle ; aucun changement des manifestes actifs ou des processus Revit observés |
| Installation/réparation/désinstallation commune sur le poste | Cycle réel via l’EXE en mode silencieux réussi : réparation 0, désinstallation 0, réinstallation 0 ; aucun redémarrage |
| Refus de désinstallation réelle avec Revit actif | Processus factice isolé nommé Revit : échec attendu 1603, installation et manifeste préservés, processus laissé ouvert par le contrôle |
| État après réinstallation | 38 fichiers personnels inchangés, 5 binaires identiques au payload publié localement, manifeste commun restauré à l’identique |
| Migration réelle de plusieurs comptes | Non exécutée ; tests sur fixtures isolées seulement |

La migration testée sauvegarde le manifeste reconnu, refuse un manifeste
étranger ou une installation commune incomplète et conserve l’état de projet et
les fichiers clients. Le serveur commun est prioritaire sur un ancien reçu
personnel ; une installation personnelle valide reste utilisable en repli.

Les captures proviennent du vrai panneau WPF exécuté dans le processus de test,
pas d’une maquette HTML. Le clic sur le ruban Revit, le parcours clavier complet,
les lecteurs d’écran, la haute résolution et le fonctionnement avec plusieurs
sessions Windows restent à vérifier manuellement. Aucune instance Revit n’a été
fermée ou redémarrée et aucun modèle utilisateur n’a été modifié.

Le contrôle de désinstallation précède `InstallValidate` (séquence 1399, puis
1400) et `InstallInitialize` (1500), vérifié dans le MSI final. La DLL DTF est
native x64 et embarque sa configuration CLR v4. Le test d’inversion du blocage
fait échouer le test WPF, puis les 62 tests repassent après restauration.
L’appel isolé de l’action MSI ne lance pas les séquences d’installation ou de
suppression. Un second contrôle a ensuite exécuté les vraies opérations via
l’EXE avec une seule autorisation administrateur. La réparation, le refus de
désinstallation avec un processus factice, la désinstallation et la
réinstallation ont été vérifiés par codes de sortie, registre, manifestes et
empreintes des fichiers. Aucun computer use n’a été utilisé pour ce cycle.
Les journaux complets restent privés sous `.local/lifecycle-20261016-130218`.

Le parcours interactif EXE → MSI → fenêtre → annulation/reprise, ainsi que les
sessions de plusieurs utilisateurs réels, restent à tester sur un poste isolé
avant publication. Le succès du cycle silencieux ne valide pas ces parcours.

Un premier contrôle GitHub a exposé une course dans l’ancien test de transport :
son budget de 400 ms pouvait expirer avant connexion, alors que le test attendait
un résultat inconnu après envoi. Le pair de test démarre désormais son attente
directement et déclenche l’annulation après réception de la requête. Les
assertions d’identité, de taille, de réponse incomplète et de résultat inconnu
restent présentes ; aucun test n’a été désactivé. Le transport du produit n’a
pas été modifié par cette correction de fixture.

Le nouveau paquet est d’une portée différente de 0.5 : son extraction ne prouve
pas que l’erreur 2203 du parcours automatisé est corrigée. Conserver les limites
du [rapport 0.5](VALIDATION-0.5.md). Les binaires restent non signés.

Avant une release : tester le cycle complet sur un poste isolé avec un compte
administrateur et deux utilisateurs ordinaires, y compris un ancien manifeste
utilisateur, un manifeste commun étranger et le rollback. Vérifier que chacun
garde ses propres projets, réglages et journaux.
