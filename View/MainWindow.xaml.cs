using System.Threading;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ViewModel;

namespace View
{
    public partial class MainWindow : Window, IUIServices
    {
        //private readonly ICommand startCommand;
        //public ICommand StartCommand => startCommand;
        //string[]? FIleNames;

        bool m_bRunning = false;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new ViewModelClass(this);
        }
        public void CanLoadCommandHandler(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !m_bRunning;
        }

        public string[]? OpenFilesDialog()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "image files (*.JPG)|*.jpg|All files (*.*)|*.*";
            openFileDialog.Title = "Выберете файлы";
            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileNames;
            }
            return null;
        }
        private void ShowError()
        {
            MessageBox.Show("Error!");
        }
    }
}
