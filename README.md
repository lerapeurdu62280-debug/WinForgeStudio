# WinForge Studio

Application Windows (WinUI 3 / .NET 8) pour construire des images ISO Windows personnalisées, de bout en bout : debloat, optimisation registre, injection de pilotes, autounattend et export bootable.

![Plateforme](https://img.shields.io/badge/plateforme-Windows%2010%2F11-0078D4)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![UI](https://img.shields.io/badge/UI-WinUI%203-5C2D91)
![Statut](https://img.shields.io/badge/statut-valid%C3%A9%20en%20VM-2EA44F)

## Pourquoi

Reproduire un déploiement Windows propre — sans bloatware, avec les bons réglages et les bons pilotes — à la main, ISO après ISO, c'est répétitif et sujet à l'erreur. WinForge Studio industrialise ce travail dans une seule application : on configure une fois les modules souhaités, et le pipeline complet (montage, modification, autounattend, export) s'exécute automatiquement jusqu'à une ISO bootable prête à installer.

## Fonctionnalités

| Module | Ce qu'il fait |
|---|---|
| **Debloat** | Supprime les applications provisionnées (Xbox, Bing, Feedback Hub, etc.) directement dans l'image WIM montée. |
| **Optimisation** | Applique de vraies clés de registre en offline : télémétrie, Cortana, services superflus, effets visuels, hibernation. |
| **Injection** | Ajoute pilotes et mises à jour dans l'image via DISM. |
| **Autounattend** | Génère un `autounattend.xml` complet : compte local, mot de passe, bypass des prérequis système (TPM/Secure Boot/RAM), etc. |
| **Éditions** | Détecte et propose le choix de l'édition Windows présente dans l'ISO source. |
| **Applications** | Installeurs `.exe`/`.msi` personnalisés, copiés dans l'ISO et proposés au premier démarrage par **WinForge Assistant**. |
| **Export ISO** | Construit une image ISO bootable via `oscdimg` (Windows ADK). |
| **Profils** | Sauvegarde/recharge une configuration complète (`.wfp`). |

## Comment ça marche

```
ISO source  →  montage WIM  →  Debloat + Optimisation + Injection
                                        ↓
                          Génération autounattend.xml
                                        ↓
                    Démontage + commit  →  Export ISO bootable (oscdimg)
```

Toute la configuration (apps cochées, options d'autounattend, chemins de pilotes…) est centralisée dans un état applicatif unique, restauré à chaque changement de page — aucun réglage ne se perd en naviguant entre les modules avant de lancer la construction.

## Prérequis

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows ADK](https://learn.microsoft.com/windows-hardware/get-started/adk-install) (fournit `oscdimg`, requis à l'export)
- Droits administrateur (montage d'ISO, opérations DISM)

## Build

```bash
dotnet build WinForgeStudio.slnx -p:Platform=x64
```

Le projet cible `net8.0-windows10.0.19041.0` et supporte les plateformes **x86**, **x64** et **ARM64**.

## Structure du projet

```
WinForge/
├── Services/       logique métier
│   ├── IsoService, DismService          montage/démontage ISO et WIM
│   ├── AutounattendService              génération de l'autounattend.xml
│   ├── OptimisationService              clés de registre offline
│   ├── IsoBuilderService                export ISO bootable (oscdimg)
│   ├── AdkDetectionService              détection du Windows ADK
│   └── ProfileService                   sauvegarde/chargement de profils
├── Views/          pages de l'interface (Debloat, Optimisation, Injection,
│                   Éditions, Applications, Autounattend, Export ISO)
└── Models/         état applicatif centralisé (AppState)
```

## Statut

Pipeline complet fonctionnel, validé de bout en bout par une installation réelle en machine virtuelle (VirtualBox et Hyper-V) jusqu'au bureau Windows.

## Licence

Aucune licence définie pour le moment.
