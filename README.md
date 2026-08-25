# WinForge Studio

Application Windows (WinUI 3, .NET 8) pour construire des images ISO Windows personnalisées : debloat des apps provisionnées, optimisation registre offline, injection de pilotes/mises à jour, génération d'un fichier `autounattend.xml`, et export en ISO bootable.

## Fonctionnalités

- **Debloat** : suppression d'applications provisionnées (Xbox, Bing, Feedback Hub, etc.) directement dans l'image WIM montée.
- **Optimisation** : application de vraies clés de registre offline (désactivation télémétrie, Cortana, services superflus, réglages de performance, hibernation).
- **Injection** : ajout de pilotes et de mises à jour dans l'image via DISM.
- **Autounattend** : génération d'un fichier `autounattend.xml` (compte local, mot de passe, bypass des prérequis système, etc.).
- **Sélecteur d'édition** : détection et choix de l'édition Windows présente dans l'ISO source.
- **Export ISO** : construction d'une image ISO bootable via `oscdimg` (Windows ADK).
- **Profils** : sauvegarde/chargement de la configuration (`.wfp`).

## Prérequis

- Windows 10/11
- .NET 8 SDK
- Windows ADK (pour `oscdimg`, requis à l'export)
- Droits administrateur (montage d'ISO, opérations DISM)

## Build

```
dotnet build WinForgeStudio.slnx -p:Platform=x64
```

Le projet cible `net8.0-windows10.0.19041.0` et supporte les plateformes x86, x64 et ARM64.

## Structure

- `WinForge/Services/` — logique métier (`IsoService`, `DismService`, `AutounattendService`, `IsoBuilderService`, `AdkDetectionService`, `OptimisationService`, `ProfileService`)
- `WinForge/Views/` — pages de l'interface (Debloat, Optimisation, Injection, Autounattend, Export ISO)
- `WinForge/Models/` — état applicatif centralisé (`AppState`)

## Statut

Pipeline complet fonctionnel, validé de bout en bout par une installation réelle en machine virtuelle (VirtualBox et Hyper-V) jusqu'au bureau Windows.

## Licence

Aucune licence définie pour le moment.
