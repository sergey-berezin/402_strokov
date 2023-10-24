using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
//using ViewModel;
using ImageRecognizerNamespace;
using System.Threading;
using System.Threading.Tasks;

namespace View
{
    //public List<DetectedImageView> DetectedImages { get; private set; }

    public partial class MainWindow : Window
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
            startCommand = new AsyncCommandStart(async _ =>
            {
                //for (int i = 1; i <= 5; i++)
                //{
                //    label.Content = i;
                //    await Task.Delay(1000);
                //}
                if (FIleNames == null)
                    FIleNames = GetFileNames();

                if (FIleNames == null)
                    return;

                foreach(string s in FIleNames)
                    await ImageRecognizer.FindAsync(s);
            });
            DataContext = this;
        }
        private void CanLoadCommandHandler(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !m_bRunning;
        }

        private void btnOpenFiles_Click(object sender, RoutedEventArgs e)
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
