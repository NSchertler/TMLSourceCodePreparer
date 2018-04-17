using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TML
{    
    public partial class MainWindow : Window
    {
        /// <summary>
        /// The view model that controls the application
        /// </summary>
        MainViewModel vm;

        /// <summary>
        /// We use one transformer object throughout the runtime of the application.
        /// </summary>
        FolderTransformer transformer = new FolderTransformer();

        public MainWindow()
        {
            InitializeComponent();
            vm = (MainViewModel)DataContext;
            
            vm.BackupFolder = transformer.BackupFolder;            
        }

        /// <summary>
        /// Opens a folder dialog and calls the callback function with the result.
        /// </summary>
        /// <param name="folderAction">This action is called with the picked folder when the dialog is closed. If the
        /// folder dialog is canceled, this callback is not called.</param>
        private void ChooseFolder(Action<string> folderAction)
        {
            using (var dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                dialog.EnsurePathExists = true;
                var dialogResult = dialog.ShowDialog(this);
                if (dialogResult == CommonFileDialogResult.Ok)
                    folderAction(dialog.FileName);
            }
        }

        private void ChooseSourceFolder(object sender, RoutedEventArgs e)
        {
            ChooseFolder(folder => vm.SourceFolder = folder);
        }

        private void ChooseTargetFolder(object sender, RoutedEventArgs e)
        {
            ChooseFolder(folder => vm.TargetFolder = folder);            
        }

        private void Transform(object sender, RoutedEventArgs e)
        {
            try
            {
                transformer.TransformFolder(vm.SourceFolder, vm.OutputType, vm.Filter, vm.TargetFolder, vm.UpToTask, vm.OnlyTransformedFiles, vm.UseSpecialSolution);
                MessageBox.Show("Folder transformation complete", "Folder Transformation", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch(Exception x)
            {
                MessageBox.Show("There was an error transforming the specified folder: " + x.Message, "Folder Transformation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GotoBackupFolder(object sender, RoutedEventArgs e)
        {
            Process.Start(vm.BackupFolder);
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        string backupFolder;
        public string BackupFolder
        {
            get => backupFolder;
            set => SetField(ref backupFolder, value);
        }

        string sourceFolder;
        public string SourceFolder
        {
            get => sourceFolder;
            set => SetField(ref sourceFolder, value);
        }

        TMLOutputType outputType;
        public TMLOutputType OutputType
        {
            get => outputType;
            set
            {
                SetField(ref outputType, value);
                OnPropertyChanged("TargetFolderActive");
            }
        }

        public string Filter
        {
            get
            {
                return Properties.Settings.Default.Filter;
            }
            set
            {
                Properties.Settings.Default.Filter = value;
                Properties.Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        string targetFolder;
        public string TargetFolder
        {
            get => targetFolder;
            set => SetField(ref targetFolder, value);
        }

        public bool TargetFolderActive
        {
            get => OutputType == TMLOutputType.Solution || OutputType == TMLOutputType.StudentVersion;
        }

        HierarchicalNumber? upToTask;
        public HierarchicalNumber? UpToTask
        {
            get => upToTask;
            set => SetField(ref upToTask, value);
        }

        bool onlyTransformedFiles;
        public bool OnlyTransformedFiles
        {
            get => onlyTransformedFiles;
            set => SetField(ref onlyTransformedFiles, value);
        }

        bool useSpecialSolution;
        public bool UseSpecialSolution
        {
            get => useSpecialSolution;
            set => SetField(ref useSpecialSolution, value);
        }
    }
}
