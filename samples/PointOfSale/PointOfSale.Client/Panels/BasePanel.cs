using Majorsilence.Forms;
using PointOfSale.Client.Services;

namespace PointOfSale.Client.Panels;

public abstract class BasePanel(ApiClient api, SessionState session) : Panel
{
    protected ApiClient Api { get; } = api;
    protected SessionState Session { get; } = session;

    /// <summary>Called every time this panel becomes the visible content panel — refetch here.</summary>
    public virtual void LoadPanel() { }

    /// <summary>Called when navigating away — release nothing you want to keep cached.</summary>
    public virtual void UnloadPanel() { }
}
