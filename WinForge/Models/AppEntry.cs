using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinForge.Models
{
    public enum RiskLevel { Safe, Medium, Danger }

    public class AppEntry : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string PackageName { get; set; } = "";
        public RiskLevel Risk { get; set; } = RiskLevel.Safe;
        public ObservableCollection<AppEntry> Children { get; set; } = new();
        public bool IsCategory { get; set; } = false;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    // Propager aux enfants si c'est une catégorie
                    if (IsCategory)
                        foreach (var child in Children)
                            child.IsSelected = value;
                }
            }
        }

        public string RiskColor => Risk switch
        {
            RiskLevel.Safe => "#4CAF50",
            RiskLevel.Medium => "#FF9800",
            RiskLevel.Danger => "#F44336",
            _ => "#4CAF50"
        };

        public string RiskLabel => Risk switch
        {
            RiskLevel.Safe => "🟢",
            RiskLevel.Medium => "🟠",
            RiskLevel.Danger => "🔴",
            _ => "🟢"
        };

        public string RiskTooltip => Risk switch
        {
            RiskLevel.Safe => "Sûr — Suppression sans risque",
            RiskLevel.Medium => "Moyen — Peut affecter certaines fonctionnalités",
            RiskLevel.Danger => "Risqué — Peut déstabiliser le système",
            _ => ""
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}