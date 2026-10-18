# Sécurité

## Signaler une vulnérabilité

Utiliser le [signalement privé GitHub](https://github.com/Dreamsigner/Dreamsign_Revit_MCP/security/advisories/new).
Indiquer la version, le composant, un scénario reproductible et l’impact attendu.
Ne pas joindre de modèle client, configuration d’assistant, chemin privé ou jeton
sans les anonymiser. Ne pas publier un exploit dans une issue publique.

## Frontières de confiance

Le serveur et Revit s’exécutent dans la session de l’utilisateur. Le pipe utilise
`CurrentUserOnly` : il sépare les comptes Windows, mais ne constitue pas une
sandbox entre applications du même compte. Utiliser uniquement des assistants
et extensions de confiance. Le profil `inspection` est le point de départ
pour les usages en lecture seule.

Les arguments sont validés à l’entrée du serveur et du complément. Le ciblage
compare titre et chemin complet ; les écritures demandent un jeton de document.
Ces contrôles limitent les erreurs de cible et les requêtes mal formées. Ils
ne garantissent pas la pertinence d’une opération demandée à l’assistant.

## Données et installation

Les configurations, rapports et autorisations de projet sont locaux et propres
à chaque utilisateur. Ils peuvent contenir des noms et propriétés de modèles.
Les configurations d’assistants sont sauvegardées avant modification.

L’installateur commun exige les droits administrateur ; l’utilisation habituelle
ne doit pas les exiger. Les binaires distribués ne sont pas encore signés.
Vérifier leur provenance et leur empreinte SHA-256 dans la release.

Les dépendances sont verrouillées et auditées en CI. Aucune prise en charge
de toutes les versions historiques n’est garantie ; consulter les notes de
release avant une mise à jour ou un retour arrière.
