namespace SmartDocumentFinder.UI.Views

open Avalonia.Controls
open Avalonia.Markup.Xaml
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
