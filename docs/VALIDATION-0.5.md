# Validation de la version 0.5.0

Contrôles réalisés le 15 octobre 2026 sur Windows, avec Revit 2027.2.

| Contrôle | Résultat |
|---|---|
| Compilation Release, projets C# et plugin Revit | Réussie, sans avertissement |
| Tests hors Revit | 49 réussis ; les 2 tests natifs sont volontairement exclus de ce parcours |
| Tests natifs, copie RVT dédiée avec préfixe `Dreamsign_MCP_` | 2 réussis |
| Lecture MCP réelle et enregistrement du bouton Revit | Version 0.5.0, projet et chemin attendus, `ui_available=true` |
| Transactions natives | Création temporaire, prévisualisation, application unique, invalidation de capture et suppression de l’élément créé |
| Fenêtre WPF | Rendu inspecté et callbacks testés ; sélection uniquement après clic et client/profil choisis respectés |
| Préservation des autres instances | Les deux processus Revit existants sont restés ouverts |
| Construction du bundle EXE et du MSI | Réussie ; avertissements ICE91 attendus pour les répertoires utilisateur, installation machine explicitement refusée |
| Première installation, réparation, désinstallation silencieuses | Réussies, conservation des données de projet |
| Réinstallation du bundle après désinstallation | À confirmer : erreur Windows Installer 2203 / chemin introuvable dans le cache utilisateur depuis l’environnement d’exécution automatisé |
| Installation directe du même MSI depuis le dossier de travail | Réussie ; le même MSI copié dans le profil utilisateur reproduit l’échec d’accès |

La réinstallation avec l’EXE lancé normalement depuis l’Explorateur doit être
confirmée avant publication de l’installateur. Ce résultat ne doit pas être
présenté comme validé tant que ce contrôle reste ouvert.

Le complément et l’installateur ne sont pas signés. Le chargement du complément
dans Revit a nécessité la confirmation de sécurité de l’utilisateur. Le clic
sur le ruban et la connexion réelle depuis la fenêtre Revit restent des
contrôles manuels ; les callbacks et écritures de configuration sont testés
séparément. Aucun modèle utilisateur n’a été enregistré par ces tests.

Les journaux détaillés, copies de modèles et configurations du poste restent
locaux et exclus du dépôt. Les tests ne mesurent pas la consommation de tokens
du client IA.
