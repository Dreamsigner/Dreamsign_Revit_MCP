# Validation de la préparation 0.6

Contrôles principaux réalisés le 16 octobre 2026 ; validation finale de la release publique le 18 octobre 2026. Le cycle automatisé de maintenance a été exécuté sur le poste avec autorisation ; la version 0.6 reste installée à son issue. Les vérifications complémentaires du 18 octobre sont détaillées en fin de rapport ; les observations du 16 octobre sont conservées pour leur contexte.

| Contrôle | Résultat |
|---|---|
| Compilation Release contre Revit 2027 local | Réussie, sans avertissement |
| Tests hors Revit | 63 réussis, dont configuration, migration, états du panneau, contrôle de désinstallation et emplacement commun Revit 2027 |
| Tests natifs Revit 0.6 | 2 réussis le 18 octobre sur Dreamsign_MCP_Release_20261018, avec serveur installé 0.6, état isolé et complément 0.6 réellement chargé |
| Chargement natif du complément | Confirmé dans Revit 2027.2 : DLL chargée 0.6.0.0, démarrage API_SUCCESS, AddInLoadFailureMessage=NoError et bouton DreamsignMCPControl enregistré |
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
| Migration réelle de plusieurs comptes | Déclarée réussie par le mainteneur le 18 octobre ; non rejouée par l’agent et sans journaux supplémentaires fournis |

## Vérification du contenu et du chargement

La vérification dans Revit a détecté deux défauts que le premier cycle de
maintenance ne révélait pas : le paquet étiqueté 0.6 contenait encore les
binaires 0.5 du dossier de publication, et le manifeste commun était placé
sous ProgramData, emplacement ignoré par Revit 2027. Les premiers résultats
de réparation/désinstallation et les comparaisons d’empreintes étaient valides
pour ce paquet, mais ne prouvaient pas que le complément 0.6 était chargé.

Le serveur et le complément ont été reconstruits et republiés ; les quatre
binaires Dreamsign portent maintenant la version 0.6.0.0. La construction du
MSI refuse désormais toute version divergente ou tout fichier manquant. Le
rejet a été vérifié pour les quatre anciens fichiers et pour un payload 0.6
présenté à une construction exigeant 0.5 ; le paquet correct compile sans
avertissement. Le cycle de maintenance a aussi été rejoué après cette correction.

Le MSI et la migration utilisent maintenant
`%ProgramFiles%\Autodesk\Revit\Addins\2027`. Le composant du manifeste a un
nouveau GUID, puisque son emplacement change. La recherche de conflits est
vérifiée dans le MSI final : absence acceptée et manifeste étranger refusé
sur une fixture isolée. Le changement d’emplacement est confirmé par le
[SDK officiel Revit 2027](https://blog.autodesk.io/revit-2027-sdk-net-10-api-changes-and-additions/#add-in-installation-folder-changes)
et par le refus explicite inscrit dans le journal de démarrage de Revit.

Le diagnostic `ping_revit` rapporte aussi la version réellement chargée,
l’enregistrement du panneau et `Application.AllUsersAddinsLocation`.
Le paquet corrigé a ensuite passé le cycle complet de maintenance et le
chargement réel dans Revit 2027.2, après confirmation manuelle du complément
non signé. Le journal indique `API_SUCCESS`, la version d’assembly 0.6.0.0,
`AddInLoadFailureMessage: NoError` et l’enregistrement du bouton
`DreamsignMCPControl`. Les modules chargés confirment les mêmes chemins et
versions. Un appel direct au bridge à l’accueil retourne `WRONG_DOCUMENT`,
refus attendu en l’absence de projet actif : le bridge répond sans ouvrir ni
modifier de modèle. Les deux tests natifs sur un modèle ont ensuite été exécutés et réussis le 18 octobre sur un projet dédié, sans modèle utilisateur.

Les preuves du cycle final, les versions et les empreintes des cinq fichiers
installés ainsi que le journal de chargement restent privés sous
`.local/revit2027-lifecycle-20261016-133200`. L’ancien manifeste ProgramData
est absent ; les 38 fichiers personnels contrôlés restent inchangés.

La migration testée sauvegarde le manifeste reconnu, refuse un manifeste
étranger ou une installation commune incomplète et conserve l’état de projet et
les fichiers clients. Le serveur commun est prioritaire sur un ancien reçu
personnel ; une installation personnelle valide reste utilisable en repli.

Les captures proviennent du vrai panneau WPF exécuté dans le processus de test,
pas d’une maquette HTML. Les contrôles du panneau ont ensuite été complétés le 18 octobre par un parcours de focus, un audit des noms accessibles et des rendus à 96/144/192 dpi. Une session complète avec lecteur d’écran et un écran physique à haute densité ne sont pas certifiés par ces essais. Les essais sur plusieurs comptes Windows sont déclarés réussis par le mainteneur le 18 octobre. Aucune instance Revit n’a été
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

Au 16 octobre, le parcours interactif EXE → MSI → fenêtre → annulation/reprise restait à tester : le succès du cycle silencieux ne le validait pas. Ce contrôle a été exécuté le 18 octobre, avec les résultats détaillés ci-dessous.

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

Critère fixé le 16 octobre pour une release stable : vérifier le cycle complet, les conflits de manifestes, le rollback et la séparation des données entre comptes. Le cycle interactif a depuis été exécuté et les essais multi-comptes ont été déclarés réussis par le mainteneur. La portée de chaque preuve reste distinguée dans ce rapport.

## Préparation de publication du 18 octobre 2026

- Les tests hors Revit ont été rejoués sur le code fusionné : 63 réussis, aucun échec. Les deux tests natifs ont ensuite été activés séparément et réussis : 65 réussites au total.
- L’EXE publié est celui du cycle de maintenance et du chargement Revit validés le 16 octobre. Son SHA-256 est `0a608971719cb3bb646eda0e4b6f259b6e742897b41e39ee1c827a98c15866dc` (30 099 843 octets).
- Sa source de construction est le commit `e4e989f5e91e8eef1d9302f502b25223b8fcaab1`. La comparaison avec le code fusionné `16b5b94da4c8f880233e73d2d1baa74f9dbbd612` ne montre que des modifications de README et de rapport de validation, aucune modification applicative. La préparation de publication ne change également que la documentation.
- Le fichier `provenance.json` joint à la release identifie le commit de publication, la source de construction, l’empreinte et les contrôles réalisés. Aucun journal privé ni modèle n’est joint.
- Le tag a été créé pendant la préparation initiale en préversion ; il reste immuable. La qualification interactive complémentaire permet la publication normale de 0.6.0 sans changer le binaire testé. Elle ne prouve pas rétrospectivement la résolution de l’erreur 2203 de 0.5.

### Complément de validation native et multi-comptes

Le 18 octobre, le mainteneur a confirmé la réussite des essais sur plusieurs comptes Windows. Cette déclaration est distinguée des essais exécutés par l’agent ; aucun journal de ces essais multi-comptes n’a été fourni.

Une nouvelle instance Revit a ouvert un projet enregistré `Dreamsign_MCP_Release_20261018`, créé depuis le gabarit métrique architectural. Les deux tests `LiveRevitTests` passent : lecture du document et des niveaux par le serveur MCP installé ; création d’un niveau temporaire, prévisualisation et application, invalidation de capture, refus de rejouer le plan, comparaison des captures et suppression du seul niveau créé. L’état MCP est isolé du ciblage habituel. Le test ne sauvegarde pas les changements et aucune instance existante n’a été fermée. Les résultats TRX restent privés dans `.local/release-0.6-validation`.


### Qualification interactive finale du 18 octobre

Le véritable EXE a été exécuté avec son interface complète (`WixBundleUILevel=4`). Revit de test actif apparaît dans la fenêtre de garde ; Continuer est désactivé. L’annulation au clavier a été vérifiée, avec conservation des empreintes du serveur, du manifeste et des configurations personnelles contrôlées. La reprise après fermeture de la seule instance de test a abouti : l’action `WaitForRevitExit` renvoie 1 et le MSI puis le bundle terminent avec le code 0, sans redémarrage.

Un cycle interactif supplémentaire sans Revit actif a vérifié le retrait effectif du serveur et du manifeste, puis une réinstallation interactive avec consentement à la licence. Le pilote termine sur PASS ; les fichiers contrôlés sont restaurés à l’identique. Les cinq binaires installés ont ensuite été comparés au payload validé : tous correspondent. Leurs versions Dreamsign sont 0.6.0.0. Les captures et journaux restent privés dans `.local/release-0.6-validation/interactive`.

Le pilotage a nécessité des reprises : attente d’initialisation de l’installateur, deux fenêtres de maintenance annulées, confirmation française d’enregistrement du projet de test et récupération silencieuse de la même 0.6 entre deux phases. Ces incidents du pilote ne sont pas comptés comme des réussites ; les résultats sont établis par les phases effectivement terminées, leurs journaux MSI/EXE, les captures et les empreintes finales. Il ne s’agit pas d’un unique parcours automatisé sans interruption.

Le panneau WPF a aussi été contrôlé hors Revit avec des actions simulées sans écriture de configuration : parcours de focus entre les commandes principales, noms accessibles non vides, rendus à 96, 144 et 192 dpi et taille minimale 600 × 600. Ces rendus sont inspectés visuellement. Ils ne remplacent pas un essai sur écran physique à plusieurs DPI ni l’écoute d’un lecteur d’écran. Aucune prise en charge exhaustive de ces environnements n’est annoncée.
