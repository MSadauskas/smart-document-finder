namespace SmartDocumentFinder.UI.Views

open Avalonia.Controls
open Avalonia.Input
open Avalonia.Markup.Xaml
open Avalonia.Media
open SmartDocumentFinder.UI.ViewModels

type MainWindow() as this =
    inherit Window()
    
    do this.InitializeComponent()
       let vm = MainWindowViewModel()
       this.DataContext <- vm
       vm.Initialize()
    
    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
    
    member this.SearchButton_Click(sender: obj, e: Avalonia.Interactivity.RoutedEventArgs) =
        let vm = this.DataContext :?> MainWindowViewModel
        vm.Search()
    
    member this.ScanButton_Click(sender: obj, e: Avalonia.Interactivity.RoutedEventArgs) =
        let vm = this.DataContext :?> MainWindowViewModel
        vm.ScanFolder()
    
    member this.SearchTextBox_KeyDown(sender: obj, e: KeyEventArgs) =
        if e.Key = Key.Enter then
            let vm = this.DataContext :?> MainWindowViewModel
            vm.Search()
    
    member this.OpenDocumentButton_Click(sender: obj, e: Avalonia.Interactivity.RoutedEventArgs) =
        let button = sender :?> Avalonia.Controls.Button
        let searchResult = button.Tag :?> SearchResultViewModel
        searchResult.OpenDocument()
    
    member this.ShowInFinderButton_Click(sender: obj, e: Avalonia.Interactivity.RoutedEventArgs) =
        let button = sender :?> Avalonia.Controls.Button
        let searchResult = button.Tag :?> SearchResultViewModel
        searchResult.ShowInFinder()
    
    member this.PreviewButton_Click(sender: obj, e: Avalonia.Interactivity.RoutedEventArgs) =
        let button = sender :?> Avalonia.Controls.Button
        let searchResult = button.Tag :?> SearchResultViewModel
        this.ShowPreviewWindow(searchResult)
    
    member this.ShowPreviewWindow(searchResult: SearchResultViewModel) =
        let previewWindow = Window()
        previewWindow.Title <- $"Preview: {searchResult.Title}"
        previewWindow.Width <- 800.0
        previewWindow.Height <- 600.0
        previewWindow.Background <- Avalonia.Media.Brushes.DarkSlateGray
        
        let scrollViewer = ScrollViewer()
        scrollViewer.Padding <- Avalonia.Thickness(20.0)
        
        // Normalize line breaks and create proper text formatting
        let normalizedContent = searchResult.Content.Replace("\r\n", "\n").Replace("\r", "\n")
        
        // Try using a TextBox in read-only mode for better line break handling
        let textBox = Avalonia.Controls.TextBox()
        textBox.Text <- normalizedContent
        textBox.TextWrapping <- Avalonia.Media.TextWrapping.Wrap
        textBox.Foreground <- Avalonia.Media.Brushes.White
        textBox.Background <- Avalonia.Media.Brushes.Transparent
        textBox.FontSize <- 14.0
        textBox.LineHeight <- 20.0
        textBox.IsReadOnly <- true
        textBox.BorderThickness <- Avalonia.Thickness(0.0)
        textBox.AcceptsReturn <- true
        textBox.AcceptsTab <- true
        
        scrollViewer.Content <- textBox
        previewWindow.Content <- scrollViewer
        
        previewWindow.Show(this)
    
    member this.BrowseButton_Click(sender: obj, e: Avalonia.Interactivity.RoutedEventArgs) =
        let vm = this.DataContext :?> MainWindowViewModel
        let dialog = Avalonia.Platform.Storage.FolderPickerOpenOptions()
        dialog.Title <- "Select Folder to Index"
        
        this.StorageProvider.OpenFolderPickerAsync(dialog).ContinueWith(fun (task: System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Avalonia.Platform.Storage.IStorageFolder>>) ->
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(fun () ->
                if task.Result.Count > 0 then
                    vm.FolderPath <- task.Result.[0].Path.LocalPath
            ) |> ignore
        ) |> ignore
