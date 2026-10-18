# Publier une version

Une release associe un tag immuable, un installateur identifié et des résultats
de validation. Une fusion dans `main` ne publie pas de binaire.

## Construire et qualifier

1. Choisir la version et mettre à jour `Directory.Build.props`, les noms de sortie
   de l’installateur et le changelog dans la même PR.
2. Restaurer les dépendances verrouillées, exécuter les tests hors Revit et compiler
   le complément contre l’installation locale Revit prise en charge.
3. Publier le serveur autonome avec le complément, puis construire le bundle :

```text
dotnet publish src/Dreamsign.Server/Dreamsign.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeRevitAddin=true -p:RestoreLockedMode=true -o artifacts/win-x64
dotnet build installer/Bundle/Dreamsign.Setup.wixproj -c Release
```

`ValidatePayload.targets` refuse les versions absentes ou divergentes dans le
payload. Reconstruire seulement le MSI ne renouvelle pas les binaires embarqués.
Le verrou de publication `packages.publish.lock.json` est distinct du verrou
de compilation ; ne pas les remplacer l’un par l’autre.

4. Qualifier l’EXE obtenu : chargement natif, tests sur projet dédié, installation,
   réparation, blocage avec Revit actif, annulation, désinstallation et réinstallation.
   Vérifier la séparation entre comptes et la conservation des données personnelles.
5. Enregistrer le commit source, les résultats, limites, taille et SHA-256 de l’EXE.
   Après une modification de code, reconstruire et requalifier. Une modification
   documentaire seule peut réutiliser un binaire si cette équivalence est vérifiée.

## Distribuer

Créer le tag après revue et CI réussie. Préparer une release en brouillon avec
l’EXE, `SHA256SUMS.txt` et une provenance sans chemins ni journaux privés.
Un envoi interrompu ne constitue pas une publication : vérifier l’état `uploaded`,
la taille et le SHA-256 de chaque asset avant de publier.

Publier en préversion lorsque la qualification nécessaire n’est pas terminée.
Une fois publiée, télécharger l’EXE sans authentification et comparer son SHA-256.
Mettre ensuite à jour le lien utilisateur. Ne pas déplacer un tag public ou
remplacer silencieusement les fichiers d’une release déjà publiée.

La CI hébergée produit un **serveur seul**, pas l’installateur complet : elle ne
dispose pas de l’API Revit. La signature nécessite un véritable certificat
d’éditeur ; sinon indiquer explicitement que les binaires ne sont pas signés.
