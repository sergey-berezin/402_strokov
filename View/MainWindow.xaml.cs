using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ViewModel;
using ImageRecognizer;
using System.Threading;

namespace View
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    /// 
    //public List<DetectedImageView> DetectedImages { get; private set; }
    struct CustomImageView
    {
        BitmapImage Img;
        string Class;
        double Confidence;
        public CustomImageView(BitmapImage Img, string Class)
        {
            this.Img = Img;
            this.Class = Class;
            Confidence = 0;
        }
    }
    public partial class MainWindow : Window
    {
        static CancellationTokenSource cts = new CancellationTokenSource();

        bool m_bRunning = false;
        List<CustomImageView> Images = new List<CustomImageView>();
        public MainWindow()
        {
            InitializeComponent();
        }
        private void CanLoadCommandHandler(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !m_bRunning; 
        }

        private void btnOpenFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "image files (*.JPG)|*.jpg|All files (*.*)|*.*";
            //openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    //List<ObjectBox> objects;
                    //try
                    //{
                    //    objects = await ImageRecognizer.FindAsync(filename, cts);
                    //}
                    //catch (Exception e)
                    //{
                    //    Console.WriteLine(e.Message);
                    //    return;
                    //}
                    //ImagesList.Items.Add(Path.GetFileName(filename));
                    //ImagesList.Items.Add(new BitmapImage(new Uri(filename)));
                    //ImagesListView.Items.Add(new BitmapImage(new Uri(filename)));
                    Images.Add(new CustomImageView(new BitmapImage(new Uri(filename)), filename));
                }
                ImagesListView.ItemsSource = Images;

            }
        }
    }
}
