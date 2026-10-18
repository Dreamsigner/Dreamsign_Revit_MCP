# Développement

Les commandes de restauration, de tests hors Revit et de formatage sont dans
[CONTRIBUTING.md](../CONTRIBUTING.md). Les responsabilités des projets sont
décrites dans [ARCHITECTURE.md](../ARCHITECTURE.md).

## Compiler le complément

```text
dotnet restore src/Dreamsign.Revit/Dreamsign.Revit.csproj --locked-mode
dotnet build Dreamsign.Revit.MCP.sln -c Release --no-restore
```

Le projet cherche Revit 2027 dans Program Files. Pour un autre emplacement,
ajouter `-p:RevitDirectory=CHEMIN_ABSOLU`. Les DLL Autodesk ne sont pas copiées
dans la distribution. Le serveur et les tests hors Revit restent indépendants
de cette installation.

## Tests natifs

Utiliser exclusivement un projet enregistré préfixé `Dreamsign_MCP_`, avec le
complément 0.6 chargé. Configurer une cible et un dossier d’état réservés au test :

```text
Dreamsign.Revit.Mcp.exe configure --document TITRE --path CHEMIN_ABSOLU_RVT --pid PID --data-dir DOSSIER_TEST
```

Dans le terminal de test, définir `DREAMSIGN_RUN_REVIT_TESTS=1`,
`DREAMSIGN_REVIT_TEST_DATA_DIR` vers ce dossier et `DREAMSIGN_TEST_SERVER_EXE`
vers le serveur publié, puis exécuter :

```text
dotnet test tests/Dreamsign.Tests/Dreamsign.Tests.csproj -c Release --filter FullyQualifiedName~LiveRevitTests
```

Ces tests lisent le document et créent un niveau temporaire, vérifient les plans
et captures, puis suppriment ce seul niveau. Ils ne sauvegardent pas le modèle.
Un échec de transport peut laisser un élément temporaire à vérifier avant de
recommencer. Ils ne créent ni ne ferment d’instance Revit.

## Configuration et compatibilité

`--data-dir` ou `DREAMSIGN_MCP_DATA_DIR` choisit le dossier d’état. Le profil peut
être fixé par `--profile` ou `ASTRA_MCP_PROFILE`. Les variables du complément
`ASTRA_REVIT_FAMILY_ROOT` et `ASTRA_REVIT_TEST_TEMPLATE` sont conservées.

`create_test_project` exige une instance à l’accueil et un PID explicite ;
`ASTRA_REVIT_TEST_TEMPLATE` doit désigner un gabarit RTE existant avant son démarrage.
Un fichier existant n’est jamais écrasé. Le préfixe des pipes et les identifiants
historiques du complément sont des contrats de compatibilité, pas des noms à renommer.

Pour les captures WPF des tests existants, définir `DREAMSIGN_UI_PREVIEW` ou
`DREAMSIGN_GUARD_PREVIEW` vers un PNG. Ces rendus ne prouvent pas le comportement
sur plusieurs écrans physiques ou avec un lecteur d’écran.
