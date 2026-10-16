# Refonte 0.6 — interface et installation commune

## Contrat de conception

La fenêtre sert à autoriser un projet précis puis à connecter un assistant.
La sélection du projet est l’action principale ; les outils de diagnostic et
la configuration avancée restent secondaires. La fenêtre reste modale et doit
être fermée avant les appels MCP.

- Identité Dreamsign : rouge profond, noir, blanc et gris neutres ; aucun vert ou bleu d’état.
- Hiérarchie inspirée des réglages macOS : titre court, groupes fonctionnels, libellés explicites et peu d’actions concurrentes.
- Typographie Segoe UI Variable avec repli système ; tailles 12–28, espacements sur une grille de 4.
- Arrondis mesurés, bordures fines, ombre légère sur les surfaces principales ; aucun dégradé décoratif.
- États normal, survol, appuyé, désactivé et focus clavier cohérents pour boutons et sélecteurs.
- Les états sont exprimés par du texte, pas seulement par la couleur.
- Le contenu défile aux petites hauteurs ; les actions de fermeture et le résultat restent visibles.
- Conserver les garde-fous de ciblage et les sauvegardes des configurations.

Les références Apple sont des principes de hiérarchie et de clarté, pas une
copie de marque ou de composants propriétaires :
[Settings](https://developer.apple.com/design/human-interface-guidelines/settings),
[Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons).

## Installation

Préparer des binaires communs à l’ordinateur, protégés contre l’écriture par les
utilisateurs ordinaires. L’installation nécessite les droits administrateur ;
l’utilisation courante et la configuration de l’assistant ne doivent pas les
nécessiter. État du projet, correspondances, rapports et configurations restent
séparés par utilisateur. Ne jamais copier une cible de projet d’un compte vers
un autre ni lire les reçus d’un utilisateur depuis une action élevée.

La migration doit tenir compte de la priorité des manifestes Revit utilisateur
sur les manifestes communs du même nom. Sauvegarder les fichiers possédés par le
produit et préserver les compléments étrangers. La version 0.5 reste utilisable
tant que l’administrateur n’a pas choisi de migrer.

Préparer et vérifier les artefacts et une PR. Ne pas activer la nouvelle version
sur ce poste ni fermer une instance Revit. Le problème 2203 décrit dans
[VALIDATION-0.5.md](VALIDATION-0.5.md) reste ouvert.
