using Podify.ViewModels;

namespace Podify.Pages;

public partial class SubscriptionsPage : ContentPage
{
    private const double CardSlotWidth = 172; // 160 card + 12 spacing
    private const double HorizontalPadding = 40; // 20 each side

    private readonly SubscriptionsViewModel _vm;

    public SubscriptionsPage(SubscriptionsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        _ = _vm.AutoRefreshOnAppearAsync();
    }

    private void OnCollectionSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not VisualElement ve || AllShowsLayout is null) return;
        var available = ve.Width - HorizontalPadding;
        if (available <= 0) return;
        var span = Math.Max(2, (int)Math.Floor(available / CardSlotWidth));
        if (AllShowsLayout.Span != span) AllShowsLayout.Span = span;
    }
}
