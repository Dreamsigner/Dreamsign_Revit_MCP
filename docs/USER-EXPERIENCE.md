# Décision historique : panneau Revit et installateur (0.5)

La version 0.5 a introduit le panneau **Compléments → Dreamsign MCP** et un
installateur EXE par utilisateur. Le panneau rend explicites trois opérations :
autoriser le projet actif enregistré, choisir l’assistant, puis configurer la connexion.

Le choix du projet n’est jamais déduit d’un changement d’onglet. La configuration
partage le même verrou d’état que le serveur, sauvegarde les fichiers modifiés
et préserve les autres entrées MCP. Le statut distingue complément chargé,
projet ciblé et dernier contact ; il ne prétend pas prouver une connexion permanente.

L’installation par utilisateur a ensuite été remplacée par l’installation commune
de 0.6, avec paramètres individuels. Pour le fonctionnement actuel, consulter le
[README](../README.md), le [guide de migration](MACHINE-INSTALLATION.md) et les
[résultats historiques 0.5](VALIDATION-0.5.md).
