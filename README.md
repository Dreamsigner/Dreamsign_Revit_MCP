# Dreamsign Revit MCP

Un bridge MCP local pour piloter **Revit 2027 sous Windows** depuis un client compatible MCP. Serveur Node.js sans dépendances npm ; add-in C# utilisant l’API Revit sur son thread principal via `ExternalEvent`.

Le cœur est en version **0.3.0 / v5**. Le transport conserve le nom `AstraRevitBridge` pour rester compatible avec l’add-in initial.

## Fonctions

- Inspection des niveaux, types, éléments, vues, sélection et propriétés géométriques.
- Création de murs, portes et fenêtres hébergées, pièces, étiquettes, quadrillages et composants ; types et hôtes explicitement fournis.
- Filtres par catégorie, niveau, famille/type, boîte spatiale et valeurs dimensionnelles.
- Lots atomiques ou indépendants, identités `source_id` persistées dans le RVT et identifiants `UniqueId`.
- Snapshots bornés, comparaison de pages et invalidation après modification du document.
- Prévisualisation annulée, plans à usage unique et procédures d’allèges, repères et étiquettes de pièces.
- Rapports détaillés locaux et réponses compactes au client MCP.

## Prérequis

- Revit 2027 installé et licencié. Les autres versions ne sont pas validées.
- Node.js 22 ou supérieur dans le `PATH`.
- PowerShell 7.6 avec ses assemblies Roslyn et le runtime .NET 10. La compilation utilise les DLL Revit installées ; aucune DLL Autodesk n’est distribuée.
- Un client MCP acceptant un serveur stdio.

## Installation

Conserver le dépôt dans un dossier stable et accessible en écriture. Depuis PowerShell 7.6, dans ce dossier :

```powershell
node generate-contract.mjs
node test.mjs
node features.test.mjs
./build.ps1
./install.ps1
Copy-Item project-state.example.json project-state.json
```

Si Revit est installé ailleurs : `./build.ps1 -RevitDirectory 'D:\Applications\Revit 2027'`.

L’installateur écrit seulement son manifeste dans le dossier utilisateur Revit 2027. Il refuse de remplacer un manifeste d’identité différente. Il ne ferme aucune application. Si un autre exemplaire de ce bridge est déjà installé, vérifier le chemin du manifeste avant d’installer cette copie.

Enregistrer le travail et redémarrer **uniquement l’instance Revit concernée** pour charger le nouvel add-in. Le code chargé dans une instance existante n’est pas remplacé par la simple compilation.

## Choisir le document cible

Éditer `project-state.json` : renseigner le titre exact du document Revit et le chemin absolu du RVT. `pid: null` active la découverte parmi les pipes du bridge ; un PID peut servir d’indication. Un titre identique avec un chemin différent est refusé ; plusieurs correspondances sont également refusées. Le document cible doit être actif dans l’instance.

Ne pas publier ce fichier : il contient l’état du projet et peut contenir des chemins et identifiants privés.

## Connecter un client MCP

Exemple de configuration Codex, avec des chemins à adapter :

```toml
[mcp_servers.dreamsign_revit]
command = 'C:\Program Files\nodejs\node.exe'
args = ['C:\Tools\Dreamsign_Revit_MCP\server.mjs']

[mcp_servers.dreamsign_revit.env]
ASTRA_MCP_PROFILE = 'modeling'
```

Pour d’autres clients, utiliser le même programme et les mêmes arguments dans leur configuration stdio. Recharger la connexion après configuration ou mise à jour du catalogue.

| Profil | Usage |
|---|---|
| `inspection` | Lectures et snapshots |
| `workflow` (défaut) | Lectures, sauvegarde et procédures métier |
| `modeling` | Lectures et création/modification/suppression |
| `diagnostic` | Lectures et création d’un projet de test |
| `full` | Tous les outils, dont lots bruts et prévisualisation générique |

Commencer par `discover_instances`, `get_active_document`, puis une lecture bornée. Les écritures utilisent le `document_token` retourné pour le document ouvert.

## Ressources locales optionnelles

Les variables suivantes sont lues **dans le processus Revit** : les définir avant de démarrer Revit, pas uniquement dans le client MCP.

- `ASTRA_REVIT_TEST_TEMPLATE` : chemin d’un gabarit architectural `.rte`, requis pour `create_test_project`.
- `ASTRA_REVIT_FAMILY_ROOT` : racine autorisée pour `load_family`. Valeur par défaut : bibliothèque française standard de Revit 2027 sous `%PROGRAMDATA%`.

Les projets de test sont créés dans `%LOCALAPPDATA%\DreamsignRevitMCP\test-projects`, uniquement depuis l’accueil sans document actif et sans écraser un fichier. Les exports de vues vont dans `%LOCALAPPDATA%\DreamsignRevitMCP\exports`.

## Validation

`node test.mjs` et `node features.test.mjs` sont hors Revit et ne modifient aucun modèle. La CI exécute ces tests et génère le registre de commandes.

`node live-features.test.mjs` est un **test avec écritures réelles** réservé à un nouveau projet `AstraBridge-Test-*` autorisé, issu d’un gabarit architectural contenant les types requis. Il crée des éléments temporaires, applique des modifications, les nettoie et sauvegarde. Consulter son résultat avant toute relance après erreur.

Le bridge local d’origine a été testé dans Revit 2027.2. La copie portable publiée est compilée et testée hors Revit ; ces contrôles ne constituent pas une nouvelle validation de modélisation dans Revit.

## Limites et sécurité

- Pipe Windows réservé au compte courant, aucune écoute HTTP et aucun exécuteur de code arbitraire.
- Maximum 250 actions par lot et 100 éléments par page. Unités métriques explicites dans les champs `_m`.
- Une suppression qui entraînerait des dépendances supplémentaires est annulée.
- Les erreurs et avertissements Revit font annuler l’action concernée. Les garanties de transaction ne couvrent pas les sorties de fichiers ni une sauvegarde échouée.
- Après timeout, le résultat peut être inconnu : lire le modèle avant de réessayer une écriture.
- Snapshots et plans expirent après cinq minutes. Les différences concernent les pages capturées, pas nécessairement tout le projet.
- Les descriptions simplifiées de mobilier ne remplacent pas des familles BIM détaillées.
- Les journaux, rapports, modèles, sauvegardes, exports et configurations privées sont exclus du dépôt.

Les deux anciennes commandes de mise en page propres au projet initial ont été retirées de cette distribution. Les détails des autres fonctions sont dans [FEATURES-0.3.md](FEATURES-0.3.md).

## Licence

Copyright © 2026 Dreamsigner. Distribué sous [licence MIT](LICENSE).
