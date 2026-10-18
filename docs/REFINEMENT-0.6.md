# Décision de conception : interface et installation 0.6

## Interface

La sélection du projet précède la configuration de l’assistant. Diagnostic et
configuration avancée restent secondaires. Les actions de fermeture et les retours
d’état restent visibles lorsque le contenu défile.

La palette rouge, noire, blanche et neutre utilise une ressource WPF partagée.
Les états de focus, de survol et de désactivation sont cohérents ; le texte donne
le sens des états sans dépendre uniquement de la couleur. Segoe UI conserve les
métriques et conventions natives de Windows.

Le panneau est modal : il doit être fermé avant les requêtes MCP pour rendre le
thread Revit disponible. Les tailles minimales et le défilement compensent cette
contrainte sans masquer les actions principales.

## Installation

Les fichiers communs sont protégés dans Program Files ; les autorisations de
projet, rapports et configurations restent séparés par utilisateur. L’élévation
est réservée à la maintenance. Le MSI n’exécute pas le serveur pour modifier
les configurations privées.

Les anciens manifestes reconnus sont sauvegardés avant migration ; les compléments
étrangers ne sont pas écrasés. La désinstallation attend la fermeture de Revit
sans terminer les processus. L’annulation doit conserver l’installation.

Voir le [guide d’installation](MACHINE-INSTALLATION.md), l’[architecture](../ARCHITECTURE.md)
et la [qualification](VALIDATION-0.6.md) pour l’implémentation et les preuves.
