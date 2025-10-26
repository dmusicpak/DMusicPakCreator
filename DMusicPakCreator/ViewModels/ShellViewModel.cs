using CommunityToolkit.Mvvm.ComponentModel;

using DMusicPakCreator.Contracts.Services;
using DMusicPakCreator.Helpers;
using DMusicPakCreator.Views;

using Microsoft.UI.Xaml.Navigation;

namespace DMusicPakCreator.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{
    [ObservableProperty]
    private bool isBackEnabled;

    [ObservableProperty]
    private object? selected;
    
    private string? title = "";

    public INavigationService NavigationService
    {
        get;
    }

    public INavigationViewService NavigationViewService
    {
        get;
    }

    public ShellViewModel(INavigationService navigationService, INavigationViewService navigationViewService)
    {
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
    }

    public void SetTitle(string InTitle)
    {
        title = InTitle;
    }
    
    public string GetTitle()
    {
        return title.Length != 0 ? title : "AppDisplayName".GetLocalized();
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;
        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }
}
