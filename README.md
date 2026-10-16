# Dreamsign Revit MCP

[![Tests](https://github.com/Dreamsigner/Dreamsign_Revit_MCP/actions/workflows/test.yml/badge.svg)](https://github.com/Dreamsigner/Dreamsign_Revit_MCP/actions/workflows/test.yml)
[![Licence MIT](https://img.shields.io/badge/Licence-MIT-blue.svg)](LICENSE)

**Pilotez un projet Revit avec votre assistant IA, depuis une connexion locale contrôlée.**

Dreamsign Revit MCP permet d’explorer un modèle, de créer des éléments et d’automatiser des tâches BIM dans **Revit 2027 sur Windows**. Il s’adresse aux architectes, équipes BIM et développeurs utilisant un assistant compatible MCP.

## Version en préparation

Cette branche prépare la **version 0.6**, avec une nouvelle interface et une installation commune à l’ordinateur. Son installateur n’est pas encore publié : voir le [rapport de validation](docs/VALIDATION-0.6.md).

La dernière version publiée reste **[la version 0.5.0](https://github.com/Dreamsigner/Dreamsign_Revit_MCP/releases/tag/v0.5.0)**, qui s’installe pour un seul utilisateur. Les étapes suivantes concernent la version 0.6.

## Installer la version 0.6

1. Lancez **Dreamsign_MCP_Setup_0.6.0.exe** et autorisez l’installation avec un compte administrateur. Le serveur et le complément sont installés une seule fois pour tous les utilisateurs du poste.
2. Ouvrez ensuite Revit 2027 et votre assistant dans votre session Windows habituelle, sans droits administrateur.
3. Si Revit était déjà ouvert, le nouveau complément sera disponible lors du prochain lancement de l’instance concernée.
4. Le complément n’étant pas encore signé, Revit peut demander une confirmation de chargement. Vérifiez le nom **Dreamsign Revit MCP** et le chemin `Program Files\DreamsignRevitMCP\0.6.0\addin\Dreamsign.Revit.dll`, puis choisissez **Charger une fois** pour le premier essai.

L’installateur contient les composants nécessaires : **aucune compilation n’est demandée aux utilisateurs**. Revit 2027 et votre assistant IA doivent déjà être installés. Le programme ne ferme aucune instance Revit automatiquement.

Chaque utilisateur choisit son projet et configure son assistant séparément. Aucun nom de compte Windows n’est fixé dans le programme ; les paramètres, rapports et autorisations de projet ne sont pas partagés entre les comptes par défaut.

Si une version 0.5 est déjà présente, consultez d’abord le [guide de migration](docs/MACHINE-INSTALLATION.md). Un ancien complément propre à l’utilisateur peut empêcher le chargement de l’installation commune.

## Choisir le projet et connecter l’assistant

Dans Revit :

1. Ouvrez votre projet et enregistrez-le.
2. Allez dans **Compléments → Dreamsign MCP**.
3. Vérifiez le nom du projet actif, puis cliquez sur **Utiliser ce projet**.
4. Choisissez **Codex**, **Claude Desktop** ou **Cursor**, puis le mode d’utilisation et **Configurer la connexion**.
5. Cliquez sur **Terminer**, puis rechargez la connexion MCP dans votre assistant. Commencez par lui demander de lire le document actif et les niveaux.

La fenêtre affiche le projet ciblé, l’état du bridge et le dernier contact reçu. **Diagnostic** aide à repérer un projet non enregistré, une cible différente ou un serveur manquant. **Configuration avancée** permet de copier la configuration pour un autre client compatible MCP. La connexion devient disponible après sélection du projet et détection du serveur installé.

![Nouvelle interface Dreamsign MCP, avec un projet d’exemple](docs/images/control-panel-0.6.png)

La configuration existante de l’assistant est sauvegardée avant modification. Les autres serveurs MCP sont conservés. La présence d’une configuration ne garantit pas que le client est installé ou connecté : rechargez sa connexion après chaque changement.

**Le choix du projet reste explicite.** Activer un autre fichier ne change pas la cible. Le MCP compare titre et chemin complet, refuse les correspondances ambiguës et exige un jeton de session pour les écritures. Vous pouvez conserver plusieurs instances Revit ouvertes.

## Ce que vous pouvez faire

| Besoin | Fonctions |
|---|---|
| Explorer | Lire les niveaux, rechercher des éléments, inspecter types, familles et propriétés |
| Créer | Murs, portes, fenêtres, pièces, étiquettes, quadrillages et composants avec types et hôtes identifiés |
| Automatiser | Régler les allèges, attribuer des repères, étiqueter les pièces et retirer certaines étiquettes |
| Préparer une intervention | Prévisualiser des actions, puis appliquer un plan lié au document et à sa révision |
| Vérifier | Comparer des captures limitées et contrôler les éléments modifiés |

Exemples à adresser à votre assistant :

> « Liste les niveaux et compte les fenêtres du RDC sans modifier le modèle. »
>
> « Prépare le passage des allèges de ces fenêtres à 0,80 m, puis applique le plan si les contrôles réussissent. »
>
> « Étiquette les pièces fermées de cette vue avec le type déjà chargé. »

Le MCP fournit les outils ; l’assistant interprète votre demande. Un croquis ou une photo nécessite une analyse préalable des dimensions, types et hôtes : la reconstruction fidèle n’est pas automatique.

## Modes d’utilisation

| Mode proposé dans Revit | Profil | Usage |
|---|---|---|
| Lecture seule | `inspection` | Consulter et vérifier le modèle |
| Automatisation | `workflow` | Procédures préparées, étiquettes et sauvegarde |
| Modélisation | `modeling` | Créer, modifier et supprimer des éléments |

Deux profils supplémentaires, `diagnostic` et `full`, sont disponibles pour le développement. Le catalogue complet comporte 24 outils.

Les procédures d’allèges, repères et étiquettes préparent un plan ; son application est une étape distincte. Un plan expire après cinq minutes et devient invalide après changement de document, de révision ou de vue. Les lots sont limités à 250 actions. Après un délai dépassé, relisez l’état du modèle avant toute nouvelle écriture : aucune action n’est relancée automatiquement.

Dimensions en mètres, surfaces en m². Les rapports détaillés restent locaux dans `%LOCALAPPDATA%\DreamsignRevitMCP`. Les captures représentent des pages de résultats ; elles ne décrivent pas nécessairement tout le modèle. Les volumes JSON ne mesurent pas les tokens facturés par l’assistant.

## Mise à jour et désinstallation

La désinstallation 0.6 demande d’enregistrer les projets et de fermer les sessions Revit actives. Leur liste est actualisée chaque seconde ; **Continuer** reste désactivé tant qu’une session est ouverte. Vous pouvez annuler à tout moment. Aucune session n’est fermée automatiquement. En mode silencieux, la présence de Revit bloque immédiatement l’opération.

Lancez l’installateur d’une version plus récente pour mettre à jour le programme. Le complément sera chargé au prochain lancement de l’instance Revit concernée.

Pour désinstaller la version commune, relancez l’EXE et choisissez **Désinstaller**, ou utilisez les applications installées de Windows, avec les droits administrateur. Les modèles, rapports et configurations personnelles sont conservés. Chaque utilisateur peut ensuite retirer l’entrée Dreamsign MCP de son assistant ; l’installateur commun ne parcourt pas les profils Windows.

## Développement

Le projet utilise C#/.NET 10, WPF pour son interface et WiX/Burn pour son installateur Windows. La compilation depuis les sources est destinée aux développeurs : voir **[DEVELOPMENT.md](docs/DEVELOPMENT.md)**.

- [Conception de la version 0.6](docs/REFINEMENT-0.6.md)
- [Installation commune et migration](docs/MACHINE-INSTALLATION.md)
- [Architecture et migration C#](docs/CSHARP-MIGRATION.md)
- [Notices des dépendances](THIRD-PARTY-NOTICES.md)

Les tests automatisés hors Revit sont distincts des essais natifs dans l’application. Les essais natifs utilisent exclusivement des projets dédiés préfixés `Dreamsign_MCP_` ; ils ne constituent pas une validation exhaustive de toutes les catégories BIM. Les autres versions de Revit et les modèles cloud ne sont pas validés par cette distribution.

## Licence

Copyright © 2026 Dreamsigner. Code sous [licence MIT](LICENSE) ; dépendances sous leurs licences respectives. Projet indépendant sans affiliation à Autodesk.
