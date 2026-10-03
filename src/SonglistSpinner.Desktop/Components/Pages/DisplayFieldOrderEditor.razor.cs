using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace SonglistSpinner.Components.Pages;

public partial class DisplayFieldOrderEditor : IAsyncDisposable
{
    private ElementReference _container;
    private DotNetObjectReference<DisplayFieldOrderEditor>? _dotNetReference;
    private IJSObjectReference? _module;
    private IJSObjectReference? _sortable;
    private bool _dragUnavailable;
    private string _reorderAnnouncement = "";

    [Parameter, EditorRequired]
    public IReadOnlyList<DisplayField> Items { get; set; } = [];

    [Parameter, EditorRequired]
    public EventCallback<DisplayFieldOrderChange> OrderChanged { get; set; }

    [Parameter, EditorRequired]
    public EventCallback<string> SelectionChanged { get; set; }

    [Parameter, EditorRequired]
    public string HelpId { get; set; } = "";

    [Parameter]
    public RenderFragment? HelpContent { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            _dotNetReference = DotNetObjectReference.Create(this);
            _module = await JS.InvokeAsync<IJSObjectReference>(
                "import",
                "./settings/DisplayFieldOrder.interop.js");
            _sortable = await _module.InvokeAsync<IJSObjectReference>(
                "initialize",
                _container,
                _dotNetReference,
                nameof(OnFieldReordered));
        }
        catch (Exception exception) when (exception is JSException or InvalidOperationException)
        {
            _dragUnavailable = true;
            Logger.LogWarning(exception, "Display-field drag reordering is unavailable");
            StateHasChanged();
        }
    }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private ILogger<DisplayFieldOrderEditor> Logger { get; set; } = default!;

    [JSInvokable]
    public Task OnFieldReordered(string fieldName, int newIndex) =>
        InvokeAsync(() => MoveFieldAsync(fieldName, newIndex));

    private async Task MoveFieldAsync(string fieldName, int newIndex)
    {
        var oldIndex = FindIndex(fieldName);
        if (oldIndex < 0 || newIndex < 0 || newIndex >= Items.Count || oldIndex == newIndex)
        {
            return;
        }

        await OrderChanged.InvokeAsync(new DisplayFieldOrderChange(fieldName, newIndex));

        var label = SettingsOptions.GetSongFieldLabel(fieldName);
        _reorderAnnouncement = $"{label} moved to position {newIndex + 1} of {Items.Count}.";
    }

    private Task ToggleFieldAsync(string fieldName) =>
        SelectionChanged.InvokeAsync(fieldName);

    private int FindIndex(string fieldName)
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (string.Equals(Items[index].Name, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_sortable is not null)
            {
                await _sortable.InvokeVoidAsync("dispose");
                await _sortable.DisposeAsync();
            }

            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException)
        {
            Logger.LogDebug(exception, "Display-field drag cleanup skipped because the page has closed");
        }
        finally
        {
            _dotNetReference?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

public readonly record struct DisplayFieldOrderChange(string FieldName, int NewIndex);
