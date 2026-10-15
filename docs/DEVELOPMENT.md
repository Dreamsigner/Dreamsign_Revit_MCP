# Développement — C# / Revit 2027

## Architecture

```text
Dreamsign.Contracts (net10.0)
  ├─ Dreamsign.Server (net10.0, SDK MCP 2.2.0)
  └─ Dreamsign.Revit (net10.0-windows, API Revit 2027 locale)
Dreamsign.Tests → Server → Contracts
```

`tool-catalog.json` est une ressource embarquée de Contracts : noms, descriptions, schémas et annotations. `ToolCatalog` définit les profils. Serveur et plugin lisent cette même ressource ; une modification du contrat exige des tests et leur recompilation.

Le serveur expose stdio via le SDK MCP officiel. `BridgeRuntime` valide avant accès au pipe, résout la cible par titre et chemin, sérialise les appels et conserve les rapports avant de compacter les résultats. Un verrou de fichier exclut les processus concurrents utilisant le même dossier d’état. Une erreur d’enregistrement après envoi retourne le résultat connu, sans nouvel envoi.

Le plugin utilise `ExternalEvent` pour accéder à l’API Revit sur le thread autorisé. Transactions, contrôles de jeton et vérifications sont conservés. L’adaptateur BIM est lié dans la même DLL ; chargement par réflexion et registre généré séparément sont supprimés. Les noms internes `AstraRevitBridge`, l’identifiant du complément et celui des identités source sont conservés pour la transition.

## Compiler et tester

SDK .NET 10.0.402 ou version acceptée par `global.json` :

```text
dotnet restore tests/Dreamsign.Tests/Dreamsign.Tests.csproj --locked-mode
dotnet test tests/Dreamsign.Tests/Dreamsign.Tests.csproj -c Release --no-restore
dotnet build Dreamsign.Revit.MCP.sln -c Release
```

Seule la solution complète exige Revit 2027. Les tests hors Revit et le serveur n’exigent aucune DLL Autodesk. Les références du plugin sont `Private=false` et `ExternallyResolved=true` : Revit fournit ses dépendances à l’exécution ; MSBuild ne copie pas son environnement applicatif dans le paquet.

Les `packages.lock.json` fixent versions et empreintes. Une mise à jour intentionnelle restaure sans `--locked-mode`, examine les changements puis relance les tests.

La publication autonome utilise séparément `packages.publish.lock.json` : ses identifiants de runtime et l’outil ILLink ajouté par le SDK diffèrent de la compilation ordinaire. Les deux configurations sont verrouillées en CI ; publier ne réécrit pas le verrou des tests.

Les tests couvrent contrats, profils, refus d’arguments, ciblage, résumés d’erreurs, état local, installation, limites de pipe, identité des réponses, délais et échange stdio avec le client MCP officiel. Les pipes simulés emploient des identifiants hors plage des PID Windows pour ne pas contacter Revit.

## Test réel volontaire

Le test crée un niveau temporaire, prévisualise puis applique une modification, vérifie l’invalidation d’une capture et le refus de rejouer le plan, compare les captures puis supprime son niveau. Il ne sauvegarde pas automatiquement. Une erreur de transport peut nécessiter une vérification manuelle des éléments préfixés `migration-test-`.

Préparez un projet dédié **AstraBridge-Test-*** enregistré et actif dans Revit 2027 avec le plugin **0.4** chargé. Configurez-le dans un dossier d’état réservé. Définissez dans l’environnement du terminal :

| Variable | Valeur |
|---|---|
| `DREAMSIGN_RUN_REVIT_TESTS` | `1` |
| `DREAMSIGN_REVIT_TEST_DATA_DIR` | Chemin absolu du dossier d’état configuré pour ce test |
| `DREAMSIGN_TEST_SERVER_EXE` | Chemin absolu de l’exécutable publié ; utilisé pour vérifier toute la chaîne MCP → Revit |

```text
dotnet test tests/Dreamsign.Tests/Dreamsign.Tests.csproj -c Release --filter FullyQualifiedName~LiveRevitTests
```

L’activation ne crée ni ne ferme d’instance. Le titre, le chemin et la version restent contrôlés. `create_test_project` (diagnostic/full) exige un PID explicite, une instance à l’accueil sans document actif et `ASTRA_REVIT_TEST_TEMPLATE` pointant vers un `.rte` existant dans l’environnement de cette instance. Aucun fichier existant n’est écrasé.

## Distribution

Compilez le plugin en Release puis publiez :

```text
dotnet publish src/Dreamsign.Server/Dreamsign.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeRevitAddin=true -o artifacts/win-x64
```

L’exécutable intègre serveur et runtime .NET. Les deux DLL du plugin restent dans `addin` car elles doivent être chargées dans Revit, séparément du processus MCP. L’installateur copie uniquement ces DLL dans un dossier versionné, sauvegarde le manifeste remplacé et écrit le nouveau manifeste atomiquement. Il ne modifie pas les clients MCP.

La CI Windows restaure les dépendances verrouillées, teste hors Revit et publie le **serveur seulement**. Elle ne valide pas le plugin dans Revit ; une distribution complète exige sa compilation locale.

## Données et compatibilité

Le serveur 0.4 accepte le bridge 0.3 pendant la transition. Le nouveau plugin valide aussi les schémas reçus : l’ancien serveur avec le nouveau plugin n’est pas garanti. Mettre à jour les deux pour une installation normale.

`configure` sauvegarde l’état quand le modèle change et réinitialise ses correspondances. Un changement de PID les conserve. L’ancien `project-state.json` peut être utilisé avec `--data-dir` pointant sur son dossier, après sauvegarde et vérification de sa cible. Aucun chemin de modèle n’est compilé dans le programme.

Le pipe conserve le préfixe `AstraRevitBridge-2027-`, les identifiants de requêtes et les limites 256 KiB / 1 MiB. Après `SAVE_FAILED`, `changes_applied` indique les modifications en mémoire lorsque leur décompte est connu ; il ne signifie pas qu’elles sont enregistrées sur disque.

Paramètres du plugin conservés : `ASTRA_REVIT_FAMILY_ROOT`, `ASTRA_REVIT_TEST_TEMPLATE`. Paramètre serveur conservé : `ASTRA_MCP_PROFILE` ; ajout de `DREAMSIGN_MCP_DATA_DIR`. Exports, rapports et journaux restent locaux et exclus de Git.
