# Développement — C# / Revit 2027

## Architecture

```text
Dreamsign.Contracts (net10.0)
  ├─ Dreamsign.Server (net10.0, SDK MCP 2.2.0)
  └─ Dreamsign.Desktop (net10.0-windows, WPF)
Dreamsign.Revit → Desktop + Contracts + API Revit 2027 locale
Dreamsign.Tests → Server + Desktop → Contracts
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

Le test crée un niveau temporaire, prévisualise puis applique une modification, vérifie l’invalidation d’une capture et le refus de rejouer le plan, compare les captures puis supprime son niveau. Il ne sauvegarde pas automatiquement. Une erreur de transport peut nécessiter une vérification manuelle des éléments préfixés `Dreamsign_MCP_`.

Préparez un projet dédié **Dreamsign_MCP_*** enregistré et actif dans Revit 2027 avec le plugin **0.6** chargé. Configurez-le dans un dossier d’état réservé. Définissez dans l’environnement du terminal :

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

L’exécutable intègre serveur et runtime .NET. `addin` contient le plugin, Contracts, Desktop et Tomlyn ; aucune DLL Autodesk n’est distribuée. Pour construire l’installateur complet après publication du serveur :

```text
dotnet build installer/Bundle/Dreamsign.Setup.wixproj -c Release
```

Le résultat est `installer/Bundle/bin/Release/Dreamsign_MCP_Setup_0.6.0.exe`. Le bundle WiX/Burn 6.0.2 contient un MSI `perMachine` : fichiers sous `%ProgramFiles%/DreamsignRevitMCP/0.6.0`, registre HKLM en vue 64 bits et manifeste sous `%ProgramData%/Autodesk/Revit/Addins/2027`. L’action native WiX XmlFile renseigne le chemin absolu de la DLL ; le serveur n’est pas exécuté avec élévation pour enregistrer l’installation. L’installation machine ne lit aucun reçu personnel et ne modifie aucune configuration d’assistant.

Le gestionnaire de redémarrage est désactivé et les redémarrages forcés supprimés. `AllowLoadIntoExistingSession=false` reporte le chargement au prochain lancement volontaire de Revit. Les contrôles MSI restent actifs. L’UpgradeCode machine et celui du bundle sont distincts de la famille 0.5 par utilisateur : pas de suppression automatique des installations privées d’autres comptes. Pour une nouvelle version changeant les dossiers de composants, renouveler leurs GUID et ne pas modifier une release déjà publiée.

La découverte du serveur lit HKLM, puis se replie sur un reçu personnel valide pour la compatibilité. Les paramètres, rapports et autorisations restent sous LocalAppData de chaque utilisateur. Le pipe conserve `CurrentUserOnly` ; les journaux vont dans `logs/bridge-PID.log`. La migration `migrate-user` est explicite et refuse l’exécution élevée. Elle sauvegarde et retire uniquement l’ancien manifeste utilisateur reconnu, après contrôle du manifeste commun ; elle ne touche pas aux projets ni aux clients. Voir [MACHINE-INSTALLATION.md](MACHINE-INSTALLATION.md).

Les commandes avancées `install`, `uninstall` et `setup-*` restent disponibles pour les installations privées et la compatibilité de leurs tests ; elles ne sont pas utilisées par le nouveau MSI commun. `SetupLifecycle` vérifie son propre reçu privé pour une désinstallation, indépendamment de la découverte d’un serveur commun plus récent. L’interface normale est **Compléments → Dreamsign MCP**, à fermer avec **Terminer** avant les appels de l’assistant. Le panneau conserve le verrou d’état partagé et vérifie aussi le PID lorsqu’il a été fixé.

Les paramètres des clients Codex (TOML), Claude Desktop et Cursor (JSON) sont sauvegardés puis mis à jour par C#. Tomlyn conserve les données et commentaires TOML ; les autres serveurs et réglages sont conservés. Une entrée étrangère n’est pas écrasée. Les ACL existantes sont conservées lors du remplacement atomique d’un fichier de configuration.

Sources : [WiX/Burn](https://docs.firegiant.com/wix/tools/burn/), [actions MSI](https://docs.firegiant.com/wix/schema/wxs/customaction/), [Restart Manager](https://learn.microsoft.com/en-us/windows/win32/msi/msirestartmanagercontrol), [Claude Desktop](https://modelcontextprotocol.io/docs/develop/connect-local-servers), [Cursor](https://cursor.com/docs/context/mcp). Conditions des outils WiX : voir `installer/OSMFEULA.txt`. La signature du programme doit utiliser un véritable certificat d’éditeur ; aucune signature n’est simulée.

La CI Windows restaure les dépendances verrouillées, teste hors Revit et publie le **serveur seulement**. Elle ne valide pas le plugin dans Revit ; une distribution complète exige sa compilation locale.

## Données et compatibilité

Le serveur 0.6 accepte les bridges 0.3, 0.4 et 0.5 pendant la transition. Le nouveau plugin valide aussi les schémas reçus : l’ancien serveur avec le nouveau plugin n’est pas garanti. Mettre à jour les deux pour une installation normale.

`configure` sauvegarde l’état quand le modèle change et réinitialise ses correspondances. Un changement de PID les conserve. L’ancien `project-state.json` peut être utilisé avec `--data-dir` pointant sur son dossier, après sauvegarde et vérification de sa cible. Aucun chemin de modèle n’est compilé dans le programme.

Le pipe conserve le préfixe `AstraRevitBridge-2027-`, les identifiants de requêtes et les limites 256 KiB / 1 MiB. Après `SAVE_FAILED`, `changes_applied` indique les modifications en mémoire lorsque leur décompte est connu ; il ne signifie pas qu’elles sont enregistrées sur disque.

Paramètres du plugin conservés : `ASTRA_REVIT_FAMILY_ROOT`, `ASTRA_REVIT_TEST_TEMPLATE`. Paramètre serveur conservé : `ASTRA_MCP_PROFILE` ; ajout de `DREAMSIGN_MCP_DATA_DIR`. Exports, rapports et journaux restent locaux et exclus de Git.
