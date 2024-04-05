using System.Windows.Controls;

namespace CampaignLogger {
    /// <summary>
    /// Interaction logic for TopicInfoControl.xaml
    /// </summary>
    public partial class TopicInfoControl : UserControl {
        MainWindow window;

        public TopicInfoControl(MainWindow window) {
            this.window = window;
            InitializeComponent();
        }

        private void relation_list_changed(object sender, SelectionChangedEventArgs e) {
            StateReference selected = this.relation_list.SelectedValue as StateReference;
            if (selected is null) {
                return;
            }
            this.relation_list.UnselectAll();
            this.window.select_entry(selected.type, selected.name);
        }
    }
}
