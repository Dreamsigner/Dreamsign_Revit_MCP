# Qualification 0.6

Essais réalisés les 16 et 18 octobre 2026 sous Windows, avec Revit 2027.2.
Ce rapport qualifie le produit 0.6 ; il ne constitue pas une garantie sur toutes
les catégories BIM, toutes les configurations Windows ou les versions suivantes.

| Contrôle | Résultat |
|---|---|
| Compilation Release contre l’API Revit locale | Réussie sans avertissement |
| Tests hors Revit | 63 réussis sur le code 0.6 |
| Tests natifs sur projet dédié | 2 réussis : lecture via le serveur MCP publié ; modification, capture, plan à usage unique et suppression du niveau temporaire |
| Chargement du complément | Revit 2027.2, assembly 0.6.0.0, démarrage sans erreur et bouton enregistré |
| Maintenance silencieuse | Réparation, refus de désinstallation avec processus de test actif, désinstallation et réinstallation réussis |
| Maintenance interactive | Revit réel listé, Continuer bloqué, annulation au clavier, reprise après fermeture, désinstallation et réinstallation réussies ; MSI/EXE terminés avec le code 0 |
| Conservation des fichiers | 38 fichiers personnels inchangés lors du cycle silencieux ; serveur, manifeste et configurations contrôlés inchangés/restaurés lors du cycle interactif |
| Payload restauré | Cinq binaires comparés au payload validé ; versions Dreamsign 0.6.0.0 |
| Séparation entre comptes | Essais déclarés réussis par le mainteneur ; pas de journaux joints pour ces essais |
| Interface WPF | Rendus inspectés à taille normale et minimale ; focus entre commandes principales, noms accessibles et rendus à 96/144/192 dpi vérifiés hors Revit |

## Provenance du paquet qualifié

EXE : `Dreamsign_MCP_Setup_0.6.0.exe`, 30 099 843 octets.
SHA-256 : `0a608971719cb3bb646eda0e4b6f259b6e742897b41e39ee1c827a98c15866dc`.
Commit de construction : `e4e989f5e91e8eef1d9302f502b25223b8fcaab1`.
Le code applicatif et l’installateur sont identiques dans le tag `v0.6.0` ; seuls
des documents ont changé pendant sa préparation. Les fichiers effectivement
disponibles sont ceux des [Releases](https://github.com/Dreamsigner/Dreamsign_Revit_MCP/releases).

Les essais natifs utilisent uniquement `Dreamsign_MCP_Release_20261018`. Aucun
modèle utilisateur n’a été modifié. Les journaux et résultats TRX sont conservés
localement ; ils ne sont pas distribués avec les sources.

## Limites

Les binaires ne sont pas signés. Les autres versions de Revit et les modèles
cloud ne sont pas validés. Les contrôles WPF ne certifient ni une session complète
avec lecteur d’écran ni une configuration physique à plusieurs DPI.
Le succès de la maintenance 0.6 ne prouve pas la résolution de l’erreur 2203
du parcours automatisé 0.5, documentée dans le [rapport historique](VALIDATION-0.5.md).
