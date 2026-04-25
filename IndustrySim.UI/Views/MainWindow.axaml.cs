using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace IndustrySim.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Snapshot of which catalog item was selected when the pointer went down.
        // Null means the clicked row was not yet selected, so we should select (expand) it.
        // Non-null means it was already selected, so Tapped should deselect (collapse) it.
        private object? _catalogRowToCollapse;

        private void CatalogGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>();
            _catalogRowToCollapse = row?.IsSelected == true ? row.DataContext : null;
        }

        private void CatalogGrid_Tapped(object? sender, TappedEventArgs e)
        {
            if (sender is not DataGrid grid || _catalogRowToCollapse == null) return;
            if (grid.SelectedItem == _catalogRowToCollapse)
                grid.SelectedItem = null;
            _catalogRowToCollapse = null;
        }
    }
}
