using System.Collections.ObjectModel;

namespace WinForge.Models
{
    public static class DebloatDatabase
    {
        public static ObservableCollection<AppEntry> GetAllApps()
        {
            return new ObservableCollection<AppEntry>
            {
                // ══════════════════════════════
                // XBOX
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Xbox & Gaming", IsCategory = true, Risk = RiskLevel.Safe,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Xbox App", PackageName = "Microsoft.XboxApp", Risk = RiskLevel.Safe, Description = "Application Xbox principale" },
                        new AppEntry { Name = "Xbox Game Bar", PackageName = "Microsoft.XboxGamingOverlay", Risk = RiskLevel.Safe, Description = "Overlay de jeu (Win+G)" },
                        new AppEntry { Name = "Xbox Game Overlay", PackageName = "Microsoft.XboxGameOverlay", Risk = RiskLevel.Safe, Description = "Superposition de jeu Xbox" },
                        new AppEntry { Name = "Xbox Identity Provider", PackageName = "Microsoft.XboxIdentityProvider", Risk = RiskLevel.Medium, Description = "Authentification Xbox Live" },
                        new AppEntry { Name = "Xbox Speech To Text", PackageName = "Microsoft.XboxSpeechToTextOverlay", Risk = RiskLevel.Safe, Description = "Reconnaissance vocale Xbox" },
                        new AppEntry { Name = "Xbox TCUI", PackageName = "Microsoft.Xbox.TCUI", Risk = RiskLevel.Medium, Description = "Interface utilisateur Xbox" },
                    }
                },

                // ══════════════════════════════
                // MICROSOFT OFFICE / 365
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Microsoft Office & 365", IsCategory = true, Risk = RiskLevel.Safe,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Office Hub", PackageName = "Microsoft.MicrosoftOfficeHub", Risk = RiskLevel.Safe, Description = "Hub Office 365" },
                        new AppEntry { Name = "OneNote", PackageName = "Microsoft.Office.OneNote", Risk = RiskLevel.Safe, Description = "Bloc-notes numérique" },
                        new AppEntry { Name = "Solitaire Collection", PackageName = "Microsoft.MicrosoftSolitaireCollection", Risk = RiskLevel.Safe, Description = "Jeux Solitaire Microsoft" },
                        new AppEntry { Name = "Sticky Notes", PackageName = "Microsoft.MicrosoftStickyNotes", Risk = RiskLevel.Safe, Description = "Notes autocollantes" },
                        new AppEntry { Name = "To Do", PackageName = "Microsoft.Todos", Risk = RiskLevel.Safe, Description = "Application de tâches" },
                        new AppEntry { Name = "Teams (personnel)", PackageName = "MicrosoftTeams", Risk = RiskLevel.Safe, Description = "Teams version grand public" },
                    }
                },

                // ══════════════════════════════
                // CORTANA & RECHERCHE
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Cortana & Recherche", IsCategory = true, Risk = RiskLevel.Medium,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Cortana", PackageName = "Microsoft.549981C3F5F10", Risk = RiskLevel.Medium, Description = "Assistant vocal Microsoft" },
                        new AppEntry { Name = "Bing Search", PackageName = "Microsoft.BingSearch", Risk = RiskLevel.Safe, Description = "Intégration Bing dans la recherche" },
                        new AppEntry { Name = "Bing News", PackageName = "Microsoft.BingNews", Risk = RiskLevel.Safe, Description = "Actualités Bing" },
                        new AppEntry { Name = "Bing Weather", PackageName = "Microsoft.BingWeather", Risk = RiskLevel.Safe, Description = "Météo Bing" },
                        new AppEntry { Name = "Bing Finance", PackageName = "Microsoft.BingFinance", Risk = RiskLevel.Safe, Description = "Finance Bing" },
                    }
                },

                // ══════════════════════════════
                // TÉLÉMÉTRIE & DIAGNOSTICS
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Télémétrie & Diagnostics", IsCategory = true, Risk = RiskLevel.Medium,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Feedback Hub", PackageName = "Microsoft.WindowsFeedbackHub", Risk = RiskLevel.Safe, Description = "Envoi de retours à Microsoft" },
                        new AppEntry { Name = "DiagTrack (Télémétrie)", PackageName = "DiagTrack", Risk = RiskLevel.Medium, Description = "Service de collecte de données" },
                        new AppEntry { Name = "Customer Experience", PackageName = "SqmClient", Risk = RiskLevel.Medium, Description = "Programme amélioration expérience" },
                        new AppEntry { Name = "Error Reporting", PackageName = "WerSvc", Risk = RiskLevel.Medium, Description = "Rapport d'erreurs Windows" },
                    }
                },

                // ══════════════════════════════
                // APPLICATIONS MULTIMÉDIA
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Multimédia", IsCategory = true, Risk = RiskLevel.Safe,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Groove Music", PackageName = "Microsoft.ZuneMusic", Risk = RiskLevel.Safe, Description = "Lecteur de musique Microsoft" },
                        new AppEntry { Name = "Movies & TV", PackageName = "Microsoft.ZuneVideo", Risk = RiskLevel.Safe, Description = "Lecteur vidéo Microsoft" },
                        new AppEntry { Name = "Mixed Reality Portal", PackageName = "Microsoft.MixedReality.Portal", Risk = RiskLevel.Safe, Description = "Portail réalité mixte" },
                        new AppEntry { Name = "3D Viewer", PackageName = "Microsoft.Microsoft3DViewer", Risk = RiskLevel.Safe, Description = "Visionneuse 3D" },
                        new AppEntry { Name = "Paint 3D", PackageName = "Microsoft.MSPaint", Risk = RiskLevel.Safe, Description = "Paint 3D (pas Paint classique)" },
                        new AppEntry { Name = "Skype", PackageName = "Microsoft.SkypeApp", Risk = RiskLevel.Safe, Description = "Application Skype" },
                    }
                },

                // ══════════════════════════════
                // SYSTEM & BLOATWARE
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Bloatware Système", IsCategory = true, Risk = RiskLevel.Medium,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Get Help", PackageName = "Microsoft.GetHelp", Risk = RiskLevel.Safe, Description = "Application d'aide Microsoft" },
                        new AppEntry { Name = "Tips (Astuces)", PackageName = "Microsoft.Getstarted", Risk = RiskLevel.Safe, Description = "Conseils pour débutants" },
                        new AppEntry { Name = "Maps", PackageName = "Microsoft.WindowsMaps", Risk = RiskLevel.Safe, Description = "Cartes Windows" },
                        new AppEntry { Name = "People", PackageName = "Microsoft.People", Risk = RiskLevel.Safe, Description = "Application Contacts" },
                        new AppEntry { Name = "Mail & Calendrier", PackageName = "microsoft.windowscommunicationsapps", Risk = RiskLevel.Safe, Description = "Client mail Windows" },
                        new AppEntry { Name = "Phone Link", PackageName = "Microsoft.YourPhone", Risk = RiskLevel.Safe, Description = "Liaison avec téléphone Android" },
                        new AppEntry { Name = "Quick Assist", PackageName = "MicrosoftCorporationII.QuickAssist", Risk = RiskLevel.Safe, Description = "Assistance à distance Microsoft" },
                        new AppEntry { Name = "Wallet", PackageName = "Microsoft.Wallet", Risk = RiskLevel.Safe, Description = "Portefeuille numérique" },
                        new AppEntry { Name = "WebExperience (Widgets)", PackageName = "MicrosoftWindows.Client.WebExperience", Risk = RiskLevel.Medium, Description = "Widgets de la barre des tâches" },
                    }
                },

                // ══════════════════════════════
                // COMPOSANTS SYSTÈME (DANGER)
                // ══════════════════════════════
                new AppEntry
                {
                    Name = "Composants Système (⚠️ Avancé)", IsCategory = true, Risk = RiskLevel.Danger,
                    Children = new ObservableCollection<AppEntry>
                    {
                        new AppEntry { Name = "Windows Security (Defender UI)", PackageName = "Microsoft.SecHealthUI", Risk = RiskLevel.Danger, Description = "⚠️ Interface Defender — ne pas supprimer" },
                        new AppEntry { Name = "App Installer", PackageName = "Microsoft.DesktopAppInstaller", Risk = RiskLevel.Danger, Description = "⚠️ Winget dépend de ce composant" },
                        new AppEntry { Name = "Store Purchase Service", PackageName = "Microsoft.StorePurchaseApp", Risk = RiskLevel.Medium, Description = "Achats sur le Microsoft Store" },
                        new AppEntry { Name = "Microsoft Store", PackageName = "Microsoft.WindowsStore", Risk = RiskLevel.Danger, Description = "⚠️ Suppression déconseillée" },
                    }
                },
            };
        }
    }
}