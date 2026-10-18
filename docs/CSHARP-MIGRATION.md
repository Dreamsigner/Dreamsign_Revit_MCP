# Décision historique : migration C# (0.4)

Le serveur et les outils d’installation ont été migrés en C# pour partager les
contrats avec le complément Revit et utiliser le SDK MCP officiel. Revit 2027
impose .NET 10 ; cette base commune limite les divergences de catalogue et de
validation entre les deux processus.

Les cinq profils et les 24 outils publiés ont été conservés. Le catalogue JSON
est devenu une ressource embarquée, commune au serveur et au complément.
Le protocole des pipes et les identifiants existants ont été conservés pour
permettre la transition. Les opérations BIM restent exécutées sur le thread
Revit par `ExternalEvent`.

La migration a été vérifiée par les tests de contrats, de transport et d’échange
stdio, puis par deux essais natifs sur un projet dédié. Ces résultats historiques
ne remplacent pas la qualification de chaque nouvelle distribution.

Références courantes : [architecture](../ARCHITECTURE.md),
[développement](DEVELOPMENT.md) et [qualification 0.6](VALIDATION-0.6.md).
