using System.Collections.ObjectModel;

namespace lingualink_client.Models
{
    public class MenusBar
    {
        public string Name { get; init; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public string Tooltip { get; set; } = string.Empty;

        public ObservableCollection<MenusBar> Children { get; set; } = [];
    }
}
