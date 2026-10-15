> Archive historique de la version 0.3 : les noms de fichiers et validations ci-dessous concernent cette ancienne version, pas la distribution C# 0.4. Consulter le README et docs/DEVELOPMENT.md pour les instructions actuelles.

# Version 0.3.0 — améliorations issues de la comparaison GitHub

Le manifeste charge AstraRevitBridge.v5.dll. Le registre bin/command-registry.json est généré depuis les définitions du serveur à la compilation et vérifié au démarrage. Il doit rester à côté de la DLL. Les tests réels 0.3 sont dans live-features.test.mjs ; leur résultat reste distinct de la validation 0.2.

## Profils et compatibilité

ASTRA_MCP_PROFILE sélectionne au lancement workflow (défaut), inspection, modeling, diagnostic ou full. tools/list expose seulement ce profil ; les autres outils sont refusés. Recharger la connexion MCP après un changement de profil. Les anciens scripts passant par client.mjs utilisent full et leurs résultats complets habituels. Le profil full est réservé aux scripts et diagnostics, car son catalogue contient toutes les définitions volumineuses.

Le catalogue par défaut passe de 31 232 à 15 563 octets, soit −50,17 %. Inspection : 7 078 octets. Il s’agit de volumes JSON, pas d’économies de tokens natifs mesurées. Les résultats compacts et leurs rapports complets sur disque sont conservés.

## Requêtes ciblées

query_elements accepte active_view, view_id, selected_only, type_id, family, box_min_m, box_max_m et jusqu’à huit prédicats numériques. Les longueurs utilisent des mètres, les surfaces des m². Les champs numériques sont height_m, width_m, sill_m, area_m2 ; les comparaisons gt, gte, lt, lte, eq. Un paramètre absent ne correspond pas au prédicat. Le calcul s’effectue dans Revit avant pagination/projection. Les listes restent limitées à 100 résultats par page.

```json
{"filter":{"category":"Walls","level":"RDC","predicates":[{"field":"height_m","op":"gt","value":3}],"fields":["unique_id","name"],"limit":25}}
```

Les catégories/types et bornes sont contrôlés. Une portée de vue avec types=true est refusée. Le filtre sélection utilise la sélection actuelle de l’instance ciblée.

## Snapshots live et différences

capture_snapshot stocke une page bornée et retourne snapshot_id, révision et couverture. read_snapshot ne la retourne que si le document, la révision et la portée active/sélection restent valides. Durée maximale cinq minutes, huit entrées en RAM ; fermeture du document purge ses captures. DocumentChanged incrémente la révision, y compris pour une modification provenant d’un autre outil ou de l’utilisateur. Un retour en arrière de l’horloge invalide la capture.

diff_snapshots compare une ancienne page au snapshot récent : entered_scope, left_scope et changed. Ces catégories ne prouvent pas une création/suppression d’éléments dans tout le projet ; elles décrivent uniquement les pages capturées. partial indique qu’une pagination n’est pas complète. Même filtre et champs sont requis ; l’identité unique est ajoutée aux captures. Une ancienne capture invalidée ne peut être relue comme actuelle, mais peut servir au diff historique avant son expiration.

## Prévisualisation et application

preview_batch (profil full) exécute la validation dans un groupe de transactions intégralement annulé. Il retourne un plan immuable lié au jeton du document, à sa révision et à la vue active ; les valeurs avant et actions prévues sont dans le rapport complet. Aucun identifiant créé dans cette prévisualisation ne doit être utilisé pour une action ultérieure. Les commandes en cascade dépendant d’identifiants nouvellement créés ne sont pas prises en charge.

apply_plan exige document_token et plan_id. Le plan expire après cinq minutes, est refusé après modification du modèle ou de la vue et est consommé avant exécution. Un échec demande une nouvelle prévisualisation ; aucune relance automatique d’écriture. L’application est atomique et la sauvegarde intervient hors transaction si prévue. Les prévisualisations n’invalident pas les captures puisque leurs transactions sont annulées.

## Procédures métier

- set_window_sills : filtre les fenêtres, prépare les valeurs d’allège en mètres et vérifie leur écriture.
- assign_marks : filtre portes ou fenêtres, ordonne les cibles par UniqueId et prépare prefix + numéro à trois chiffres. Les repères existants sont conservés sauf overwrite=true.
- tag_rooms : prépare les étiquettes de pièces closes au niveau de la vue, avec un type explicitement fourni ; les pièces déjà étiquetées dans cette vue sont ignorées. L’identité source inclut pièce et vue.

Toutes préparent un plan : appeler apply_plan pour l’exécuter. Ce parcours n’ajoute pas de confirmation humaine lorsque l’action est déjà autorisée. Les filtres des procédures désignent toutes les correspondances, maximum 250 ; les options de pagination/projection sont refusées pour éviter une interprétation ambiguë.

```json
{"document_token":"...","filter":{"level":"RDC"},"value_m":0.8,"save":true}
```

Cet exemple est destiné à set_window_sills. Son résultat contient un plan_id à transmettre à apply_plan avec le même document_token. Le résultat compact n’inclut pas la géométrie ou toutes les valeurs intermédiaires ; le rapport les conserve.

## Vérification et limites

La version locale initiale a été validée dans Revit 2027.2. Cette distribution portable doit être validée dans un projet de test après installation ; les résultats et modèles locaux ne sont pas publiés.

features.test.mjs vérifie profils, prédicats, frontières de validation, résumés de plans, blocage d’une ancienne DLL, registre et réduction du catalogue. live-features.test.mjs est limité au projet Dreamsign_MCP_* : géométrie temporaire, filtres, preview annulée, invalidation, diff, refus de replay, allèges, repères, étiquettes de pièces, nettoyage et sauvegarde.

Le scénario réel contient une modification indépendante via une autre commande MCP qui déclenche DocumentChanged ; il ne simule pas un clic manuel dans Revit. Les collisions 3D et conformité réglementaire restent hors de ce lot. Le diff ne couvre que les valeurs capturées, pas tous les paramètres cachés du modèle. La compatibilité des autres versions de Revit n’est pas revendiquée.

La journalisation existante conserve les requêtes, rapports détaillés et avant/après des plans. Elle ne mesure pas encore chaque phase interne d’API ni un coût facturé par action. Les messages de quota et tailles JSON ne sont pas convertis en coûts monétaires.
