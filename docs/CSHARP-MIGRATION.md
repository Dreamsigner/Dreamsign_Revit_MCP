# Migration C# — 0.4.0

## Objectif et périmètre accepté

Remplacer le serveur JavaScript et les scripts PowerShell par une distribution C#/.NET 10 pour Revit 2027, avec les 24 outils, les cinq profils et les mêmes protections de ciblage. L’architecture à quatre projets reprend le choix approuvé dans la discussion de migration.

| Projet | Responsabilité | Dépendance |
|---|---|---|
| Contracts | Catalogue, schémas, messages du pipe et profils | .NET 10 |
| Server | SDK MCP officiel, ciblage, rapports et installation | Contracts |
| Revit | ExternalEvent, transactions et opérations BIM | Contracts, API Revit 2027 |
| Tests | Contrats, transport simulé et intégration stdio | Contracts, Server |

## Ordre et commandes

1. Contrats partagés : préserver les schémas et tester les refus avant transport.
2. Serveur C# : découverte, pipe, gestion de l’état, rapports et protocole SDK.
3. Plugin : compilation MSBuild standard et partage direct du catalogue.
4. Installation/diagnostic C#, documentation et CI ; comparer avec le contrat 0.3.
5. Vérification réelle en lecture seule avec le bridge existant si une instance ciblée est disponible. Aucun remplacement de manifeste sans commande explicite.

```text
dotnet build Dreamsign.Revit.MCP.sln -c Release
dotnet test tests/Dreamsign.Tests/Dreamsign.Tests.csproj -c Release
dotnet run --project src/Dreamsign.Server -- --help
```

## Critères de validation

- Catalogue et validation identiques à 0.3 ; ajout explicite du jeton obligatoire au workflow d’écriture.
- Refus des chemins/titres divergents, correspondances multiples, anciens protocoles incompatibles et outils hors profil.
- Pas de reprise automatique d’écriture après timeout ; request_id et limites de transport conservés.
- Sérialisation des opérations et sauvegarde atomique de l’état local ; rapport complet conservé avant réponse compacte.
- Snapshots et plans Revit conservés : expiration, invalidation, rollback et usage unique.
- Tests .NET réussis, compilation contre les DLL installées et artefact publié ne dépendant ni de Node.js ni de PowerShell.
- Une compilation ou un test simulé ne prouve pas une validation dans Revit. Rapporter distinctement les validations réelles possibles.

## Limites de migration

L’installation actuellement chargée dans Revit reste en place. Le nouveau serveur accepte le bridge 0.3 pendant la transition. Le nouveau plugin annonce 0.4 et garde le protocole des pipes 2027. Une nouvelle validation du plugin chargé exige un redémarrage utilisateur de la seule instance ciblée.

Les modèles, identifiants privés, logs, DLL Autodesk et caches NuGet ne sont pas publiés. Les noms d’outils et champs restent compatibles. JSON reste un format de données ; le catalogue embarqué remplace sa génération JavaScript. La licence reste MIT ; le SDK MCP conserve sa licence et ses notices propres.

## Validation de la migration

- Comparaison sémantique des 24 définitions avec la version 0.3 : identiques, sauf le jeton désormais obligatoire pour `run_workflow`.
- Release : compilation des quatre projets sans avertissement ; 40 tests hors Revit réussis. Le test réel volontaire reste désactivé et ne compte pas comme une réussite.
- Distribution Windows autonome produite avec le runtime .NET ; seuls `Dreamsign.Revit.dll` et `Dreamsign.Contracts.dll` sont placés dans `addin`. Aucun script JavaScript/PowerShell n'est requis.
- L’exécutable autonome a également réussi l’initialisation, le catalogue et les appels stdio avec le client MCP officiel.
- Plugin actuellement installé conservé. Aucune instance Revit n’était ouverte au contrôle de disponibilité : pas de validation réelle du plugin 0.4 dans cette session.
- La revue a ajouté des contrôles de réponse JSON nulle, un verrou entre processus pour l’état local et la conservation des correspondances lors d’un simple changement de PID.

Les téléchargements superflus des runtimes web/UI pendant la publication ont été supprimés via le paramètre MSBuild partagé `DisableTransitiveFrameworkReferenceDownloads`. Le serveur utilise uniquement le runtime de base.

## Sources vérifiées

- SDK officiel MCP C# 2.2.0 : https://github.com/modelcontextprotocol/csharp-sdk/tree/v2.2.0
- Gestionnaires tools/list et tools/call : https://github.com/modelcontextprotocol/csharp-sdk/blob/v2.2.0/src/ModelContextProtocol/McpServerBuilderExtensions.cs
- Revit 2027 / .NET 10 : https://blog.autodesk.io/revit-2027-sdk-net-10-api-changes-and-additions/
