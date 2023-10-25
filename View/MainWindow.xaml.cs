using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ViewModel;
using System.Threading;

namespace View
{
    public partial class MainWindow : Window//, IUIServices
    {
        private readonly ICommand startCommand;
        public ICommand StartCommand => startCommand;
        string[]? FIleNames;
        static CancellationTokenSource cts = new CancellationTokenSource();

        bool m_bRunning = false;
        //List<CustomImageView> Images = new List<CustomImageView>();
        public MainWindow()
        {
            InitializeComponent();
            //DataContext = new ViewModelClass();
        }
        private void CanLoadCommandHandler(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !m_bRunning;
        }

        private void OpenFilesDialog(object sender, RoutedEventArgs e)
        {
            FIleNames = GetFileNames();
        }

        private string[]? GetFileNames()
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
