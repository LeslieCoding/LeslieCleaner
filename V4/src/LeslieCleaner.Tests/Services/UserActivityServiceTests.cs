using LeslieCleaner.Core.Abstractions;
using LeslieCleaner.Infrastructure;
using LeslieCleaner.Infrastructure.Services;
using LeslieCleaner.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace LeslieCleaner.Tests.Services;

public sealed class UserActivityServiceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    public void Dispose() => _workspace.Dispose();

    [Fact]
    public void Infrastructure_RegistersDashboardUserActivityDependencies()
    {
        var services = new ServiceCollection();

        services.AddLeslieCleanerInfrastructure();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IFavoriteService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IRecentService));
    }

    [Fact]
    public void Favorites_ToggleRemovesOnlyRequestedTitleAndPersistsAtomically()
    {
        string store = Path.Combine(_workspace.RootDirectory, "activity", "favorites.json");
        var service = new FavoriteService(store);
        service.ToggleFavorite("Alpha");
        service.ToggleFavorite("Beta");
        service.ToggleFavorite("Gamma");

        service.ToggleFavorite("beta");

        IReadOnlyList<FavoriteItem> favorites = service.GetFavorites();
        Assert.Equal(["Alpha", "Gamma"], favorites.Select(static item => item.Title));
        Assert.False(service.IsFavorite("BETA"));
        Assert.False(File.Exists(store + ".tmp"));

        var reloaded = new FavoriteService(store);
        Assert.Equal(["Alpha", "Gamma"], reloaded.GetFavorites().Select(static item => item.Title));
    }

    [Fact]
    public void RecentActivity_DeduplicatesAndRetainsNewestFifty()
    {
        string store = Path.Combine(_workspace.RootDirectory, "activity", "recent.json");
        var service = new RecentService(store);
        for (int index = 0; index < 55; index++)
        {
            service.Record($"Item{index:D2}", $"icon-{index:D2}");
        }

        IReadOnlyList<RecentItem> recent = service.GetRecent();
        Assert.Equal(50, recent.Count);
        Assert.Equal("Item54", recent[0].Title);
        Assert.DoesNotContain(recent, static item => item.Title == "Item00");

        service.Record("item10", "updated-icon");
        recent = service.GetRecent();
        Assert.Equal(50, recent.Count);
        Assert.Equal("item10", recent[0].Title);
        Assert.Equal("updated-icon", recent[0].Icon);
        Assert.Single(recent, static item => item.Title.Equals("Item10", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(store + ".tmp"));

        var reloaded = new RecentService(store);
        Assert.Equal(50, reloaded.GetRecent().Count);
        Assert.Single(reloaded.GetRecent(), static item =>
            item.Title.Equals("Item10", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DamagedStores_DoNotPreventServiceStartup()
    {
        string favorites = _workspace.CreateFile("damaged/favorites.json", "not-json");
        string recent = _workspace.CreateFile("damaged/recent.json", "not-json");

        Assert.Empty(new FavoriteService(favorites).GetFavorites());
        Assert.Empty(new RecentService(recent).GetRecent());
    }
}
