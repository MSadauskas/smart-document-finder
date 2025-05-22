namespace SmartDocumentFinder.UI

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Data.Core
open Avalonia.Data.Core.Plugins
open Avalonia.Markup.Xaml
open SmartDocumentFinder.UI.ViewModels
open SmartDocumentFinder.UI.Views

type App() =
    inherit Application()

    override this.Initialize() =
            AvaloniaXamlLoader.Load(this)

    override this.OnFrameworkInitializationCompleted() =

        // Line below is needed to remove Avalonia data validation.
        // Without this line you will get duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0)

        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
             desktop.MainWindow <- MainWindow(DataContext = MainWindowViewModel())
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
