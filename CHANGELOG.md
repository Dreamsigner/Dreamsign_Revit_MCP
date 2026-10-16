# Historique des changements

## 0.6.0 — en préparation

### Ajouts
- Interface WPF rouge, noire et blanche, contrôles cohérents, footer visible et mise en page adaptable.
- Installation commune à l’ordinateur, avec serveur détecté automatiquement pour chaque utilisateur.
- Migration explicite d’un ancien manifeste utilisateur, sauvegardé avant retrait.
- Désinstallation bloquée tant que Revit est ouvert : liste des sessions actualisée chaque seconde, rappel d’enregistrer les projets et poursuite explicite après fermeture. Aucune fermeture forcée ; refus immédiat en mode silencieux.

### Changements
- Connexion de l’assistant disponible après sélection du projet et détection du serveur.
- Distinction de l’instance ciblée dans le panneau ; un même fichier dans une autre instance ne devient pas implicitement autorisé.
- Journaux personnels du bridge, séparés par processus, hors du répertoire des binaires.

### Migration
La portée de l’installation passe d’un utilisateur à l’ordinateur. Une
autorisation administrateur est nécessaire pour installer ; voir le
[guide de migration](docs/MACHINE-INSTALLATION.md). Ce paquet ne remplace pas
automatiquement les installations 0.5 des autres comptes.
