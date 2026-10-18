# Contribuer

Prérequis : Windows et le SDK .NET indiqué par `global.json`.
Les tests hors Revit n’exigent pas de DLL Autodesk. La compilation du complément
exige une installation locale de Revit 2027.

## Vérifier une modification

Depuis la racine du dépôt :

```powershell
dotnet restore tests/Dreamsign.Tests/Dreamsign.Tests.csproj --locked-mode
dotnet test tests/Dreamsign.Tests/Dreamsign.Tests.csproj -c Release --no-restore -p:TreatWarningsAsErrors=true
$sources = git ls-files '*.cs'
dotnet format whitespace --folder --include $sources --verify-no-changes
```

Pour appliquer le formatage, retirer `--verify-no-changes`. Les règles sont
dans `.editorconfig`. Les contrôles de documentation et de dépendances entre
projets font partie des tests.

Une correction de comportement doit avoir un test qui échoue avant la correction.
Ne pas mélanger une remise en forme mécanique et un changement fonctionnel.
Les tests natifs nécessitent un projet dédié et une activation explicite :
voir [DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Proposer une PR

Décrire le problème, le comportement obtenu et les contrôles exécutés. Mentionner
les limites qui affectent la revue. Joindre une capture si l’interface change.
Éviter les récits de session, les promesses non vérifiées et les listes de fichiers
qui répètent simplement le diff.

Les dépendances sont verrouillées. Une mise à jour doit inclure les verrous
modifiés et les tests correspondants ; les mises à jour proposées automatiquement
ne sont pas fusionnées automatiquement. Les contributions sont sous licence MIT.
