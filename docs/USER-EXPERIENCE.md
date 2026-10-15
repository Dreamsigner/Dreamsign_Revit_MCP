# Parcours utilisateur 0.5 — périmètre adopté

Les choix suivants ont été approuvés dans la discussion, puis leur mise en œuvre groupée a été demandée.

| Module | Résultat attendu | Dépendance |
|---|---|---|
| configuration | État partagé, ciblage explicite et configurations des clients sauvegardées | Contracts |
| interface | Fenêtre WPF depuis Compléments : projet, sélection en un clic, état et connexion d’un assistant | configuration |
| installation | Un EXE Windows, installation utilisateur, mise à jour et désinstallation standard | serveur, interface, plugin |
| documentation | Installation prête à l’emploi en premier ; compilation dans la documentation développeur | installation |

Ordre : configuration → interface → installation → tests et publication. Toute la logique applicative reste en C# ; WPF/XAML décrit la fenêtre et WiX décrit le paquet d’installation Windows.

## Contraintes vérifiables

- L’interface cible uniquement le document actif enregistré et non familial. Aucun basculement automatique de cible, aucune fermeture d’instance, aucune sauvegarde automatique de modèle.
- Le même verrou et le même fichier d’état sont utilisés par serveur et interface. Changer de projet sauvegarde les anciennes correspondances.
- La connexion à un assistant est une action explicite ; sauvegarde avant modification, conservation des réglages des autres serveurs, refus d’écraser une entrée étrangère.
- Le statut distingue plugin chargé, projet ciblé et dernier contact. Il ne déduit pas une connexion permanente de la simple présence d’un pipe.
- L’installateur est un bundle WiX/Burn contenant un MSI utilisateur et le serveur autonome. Windows gère ses fichiers et la désinstallation. Les compléments existants et les modèles sont conservés ; le gestionnaire de redémarrage ne ferme pas Revit.
- Les noms générés et projets de test utilisent `Dreamsign_MCP_`. Les fichiers existants ne sont pas renommés. Les identifiants techniques historiques conservés pour compatibilité ne désignent pas des projets de test.
- La documentation s’adresse aux utilisateurs : installer, ouvrir Revit, utiliser Compléments, choisir le projet et connecter l’assistant. La phrase signalée sur Node.js/PowerShell est supprimée.

## Validation

Tests des configurations et restaurations, tests de fenêtre WPF avec rendu, compilation contre Revit 2027, construction et inspection de l’EXE/MSI, installation/réparation/désinstallation contrôlées et tests natifs sur un projet dédié quand disponible. Les preuves locales et fichiers privés restent hors Git.

Sources d’implémentation : documentation Autodesk Revit 2027, Microsoft WPF et Windows Installer, WiX/Burn 6.0.2. Aucun certificat de signature ni identifiant d’éditeur n’est inventé.
